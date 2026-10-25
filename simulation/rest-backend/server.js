// 模擬環境的 REST 測試後端（backend-a / backend-b）。只用 Node.js 內建模組，不需建置映像。
//
// /items 資源（記憶體儲存，重啟後還原）：
//   GET    /items?q=&tag=&minPrice=&maxPrice=&sort=&page=&size=   列表（查詢參數篩選、排序、分頁）
//   GET    /items/{id}                                           單筆
//   POST   /items            { name, price, tags }               新增 → 201
//   PUT    /items/{id}       { name, price, tags }               整筆取代
//   PATCH  /items/{id}       { 任一欄位 }                         部分更新
//   DELETE /items/{id}                                           刪除
//   POST   /reset                                                還原初始資料
// 其他路徑：回顯收到的方法、路徑、查詢參數、標頭與 body。
//
// 路徑只比對結尾的 /items 或 /items/{id}，閘道入口有沒有「移除路徑前綴」都能用；
// 回應中的 request.path 是後端實際收到的路徑，可用來確認前綴是否已移除。
// 每個回應都帶 service（SERVICE_NAME），send-requests.ps1 依此統計 A/B 分配。

const http = require('node:http')

const SERVICE = process.env.SERVICE_NAME || 'backend'
const PORT = Number(process.env.PORT || 8080)
const MAX_PAGE_SIZE = 100
const SORT_FIELDS = ['id', 'name', 'price']

const seed = () => [
  { id: 1, name: 'apple', price: 30, tags: ['fruit', 'red'] },
  { id: 2, name: 'banana', price: 15, tags: ['fruit', 'yellow'] },
  { id: 3, name: 'carrot', price: 20, tags: ['vegetable', 'orange'] },
]
let items = seed()
let nextId = items.length + 1

function send(res, status, payload) {
  const body = JSON.stringify({ service: SERVICE, ...payload }, null, 2)
  res.writeHead(status, { 'Content-Type': 'application/json; charset=utf-8', 'X-Backend': SERVICE })
  res.end(body)
}

function readBody(req) {
  return new Promise((resolve) => {
    const chunks = []
    req.on('data', (c) => chunks.push(c))
    req.on('end', () => {
      const text = Buffer.concat(chunks).toString('utf8')
      if (!text) return resolve({ raw: '', json: undefined })
      try {
        resolve({ raw: text, json: JSON.parse(text) })
      } catch {
        resolve({ raw: text, json: undefined })
      }
    })
  })
}

/** 驗證 item 欄位；partial 為 PATCH（只驗證有帶的欄位） */
function validate(input, partial) {
  if (typeof input !== 'object' || input === null || Array.isArray(input)) return 'body 必須是 JSON 物件'
  if ((!partial || 'name' in input) && (typeof input.name !== 'string' || input.name.trim() === '')) return 'name 必須是非空字串'
  if ((!partial || 'price' in input) && (typeof input.price !== 'number' || input.price < 0)) return 'price 必須是 >= 0 的數字'
  if ('tags' in input && (!Array.isArray(input.tags) || input.tags.some((t) => typeof t !== 'string'))) return 'tags 必須是字串陣列'
  return null
}

function list(query) {
  let result = items
  const q = query.get('q')
  if (q) result = result.filter((i) => i.name.includes(q.toLowerCase()))
  // 可重複的參數：?tag=fruit&tag=red 表示同時具備
  const tags = query.getAll('tag')
  if (tags.length > 0) result = result.filter((i) => tags.every((t) => i.tags.includes(t)))
  const min = Number(query.get('minPrice'))
  const max = Number(query.get('maxPrice'))
  if (query.has('minPrice') && !Number.isNaN(min)) result = result.filter((i) => i.price >= min)
  if (query.has('maxPrice') && !Number.isNaN(max)) result = result.filter((i) => i.price <= max)

  // sort=price 由小到大、sort=-price 由大到小
  const sort = query.get('sort') || 'id'
  const field = sort.replace(/^-/, '')
  if (SORT_FIELDS.includes(field)) {
    const dir = sort.startsWith('-') ? -1 : 1
    result = [...result].sort((a, b) => (a[field] > b[field] ? dir : a[field] < b[field] ? -dir : 0))
  }

  const page = Math.max(1, Number.parseInt(query.get('page') || '1', 10) || 1)
  const size = Math.min(MAX_PAGE_SIZE, Math.max(1, Number.parseInt(query.get('size') || '10', 10) || 10))
  return { data: result.slice((page - 1) * size, page * size), page, size, total: result.length }
}

const server = http.createServer(async (req, res) => {
  const url = new URL(req.url, 'http://placeholder')
  const body = await readBody(req)
  const request = {
    method: req.method,
    path: url.pathname,
    query: Object.fromEntries([...url.searchParams.keys()].map((k) => {
      const values = url.searchParams.getAll(k)
      return [k, values.length > 1 ? values : values[0]]
    })),
    headers: {
      host: req.headers.host,
      'x-forwarded-for': req.headers['x-forwarded-for'],
      'x-forwarded-host': req.headers['x-forwarded-host'],
      'x-forwarded-proto': req.headers['x-forwarded-proto'],
      'content-type': req.headers['content-type'],
    },
    body: body.json ?? (body.raw || undefined),
  }
  console.log(`${req.method} ${req.url} host=${req.headers.host} xff=${req.headers['x-forwarded-for'] ?? '-'}`)

  // POST /reset：還原初始資料，方便重跑測試
  if (req.method === 'POST' && /(?:^|\/)reset\/?$/.test(url.pathname)) {
    items = seed()
    nextId = items.length + 1
    return send(res, 200, { message: '資料已還原', data: items, request })
  }

  const match = url.pathname.match(/(?:^|\/)items(?:\/([^/]+))?\/?$/)
  if (!match) return send(res, 200, { message: '回顯模式：此路徑不是 /items 資源', request })

  const id = match[1] === undefined ? null : Number(match[1])
  if (match[1] !== undefined && !Number.isInteger(id)) return send(res, 400, { error: 'id 必須是整數', request })
  const index = id === null ? -1 : items.findIndex((i) => i.id === id)
  const notFound = () => send(res, 404, { error: `找不到 id=${id}`, request })

  switch (`${req.method} ${id === null ? 'collection' : 'item'}`) {
    case 'GET collection':
      return send(res, 200, { ...list(url.searchParams), request })
    case 'POST collection': {
      const error = validate(body.json, false)
      if (error) return send(res, 400, { error, request })
      const item = { id: nextId++, name: body.json.name.trim().toLowerCase(), price: body.json.price, tags: body.json.tags ?? [] }
      items.push(item)
      res.setHeader('Location', `${url.pathname.replace(/\/$/, '')}/${item.id}`)
      return send(res, 201, { data: item, request })
    }
    case 'GET item':
      return index < 0 ? notFound() : send(res, 200, { data: items[index], request })
    case 'PUT item': {
      if (index < 0) return notFound()
      const error = validate(body.json, false)
      if (error) return send(res, 400, { error, request })
      items[index] = { id, name: body.json.name.trim().toLowerCase(), price: body.json.price, tags: body.json.tags ?? [] }
      return send(res, 200, { data: items[index], request })
    }
    case 'PATCH item': {
      if (index < 0) return notFound()
      const error = validate(body.json, true)
      if (error) return send(res, 400, { error, request })
      const patch = { ...body.json, id }
      if (typeof patch.name === 'string') patch.name = patch.name.trim().toLowerCase()
      items[index] = { ...items[index], ...patch }
      return send(res, 200, { data: items[index], request })
    }
    case 'DELETE item': {
      if (index < 0) return notFound()
      const [removed] = items.splice(index, 1)
      return send(res, 200, { data: removed, request })
    }
    default:
      res.setHeader('Allow', id === null ? 'GET, POST' : 'GET, PUT, PATCH, DELETE')
      return send(res, 405, { error: `${req.method} 不適用於此資源`, request })
  }
})

server.listen(PORT, () => console.log(`${SERVICE} listening on :${PORT}`))
