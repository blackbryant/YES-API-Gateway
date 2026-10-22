import { API_BASE_PATH, API_TIMEOUT_MS } from '../constants/app'

/** API 錯誤（對應後端失敗回應 { success:false, code, message, details }） */
export class ApiError extends Error {
  readonly status: number
  /** 後端錯誤碼（ErrorCodes），畫面文字依此查 i18n */
  readonly code?: string
  /** 後端 details（例：發佈驗證錯誤清單） */
  readonly details?: unknown

  constructor(status: number, code?: string, message?: string, details?: unknown) {
    super(message ?? code ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = code
    this.details = details
  }
}

const HTTP_NO_CONTENT = 204

type HttpVerb = 'GET' | 'POST' | 'PUT' | 'DELETE'

/** 後端統一回應：成功 { success:true, data }；分頁另含 pageIndex 等；失敗 { success:false, code, message, details } */
interface ApiEnvelope<T> {
  success: boolean
  data?: T
  code?: string
  message?: string
  details?: unknown
  pageIndex?: number
  pageSize?: number
  totalCount?: number
  totalPages?: number
}

/** 分頁結果 */
export interface PagedEnvelope<T> {
  items: T[]
  pageIndex: number
  pageSize: number
  totalCount: number
  totalPages: number
}

async function send<T>(method: HttpVerb, path: string, body?: unknown): Promise<ApiEnvelope<T> | undefined> {
  const controller = new AbortController()
  const timer = setTimeout(() => controller.abort(), API_TIMEOUT_MS)

  try {
    const response = await fetch(`${API_BASE_PATH}${path}`, {
      method,
      credentials: 'include',
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    })

    if (response.status === HTTP_NO_CONTENT) {
      return undefined
    }

    const envelope = (await response.json().catch(() => ({ success: false }))) as ApiEnvelope<T>
    if (!response.ok || !envelope.success) {
      throw new ApiError(response.status, envelope.code, envelope.message, envelope.details)
    }

    return envelope
  } finally {
    clearTimeout(timer)
  }
}

async function request<T>(method: HttpVerb, path: string, body?: unknown): Promise<T> {
  return (await send<T>(method, path, body))?.data as T
}

async function requestPaged<T>(path: string): Promise<PagedEnvelope<T>> {
  const envelope = await send<T[]>('GET', path)
  return {
    items: envelope?.data ?? [],
    pageIndex: envelope?.pageIndex ?? 1,
    pageSize: envelope?.pageSize ?? 0,
    totalCount: envelope?.totalCount ?? 0,
    totalPages: envelope?.totalPages ?? 0,
  }
}

/** 統一 API client（原生 fetch 封裝，憲法三） */
export const apiClient = {
  get: <T>(path: string) => request<T>('GET', path),
  getPaged: <T>(path: string) => requestPaged<T>(path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
  put: <T>(path: string, body?: unknown) => request<T>('PUT', path, body),
  delete: (path: string) => request<void>('DELETE', path),
}
