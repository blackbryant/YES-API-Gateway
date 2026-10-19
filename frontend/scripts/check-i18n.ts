// 比對 zh-TW 與 en 翻譯鍵值是否一致；不一致時以非 0 結束，供 CI 阻斷（憲法六）
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const LOCALES_DIR = resolve(dirname(fileURLToPath(import.meta.url)), '../src/i18n/locales')
const LANGUAGES = ['zh-TW', 'en']
const NAMESPACES = ['common']

type Json = { [key: string]: Json | string }

function flatten(obj: Json, prefix = ''): string[] {
  return Object.entries(obj).flatMap(([key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key
    return typeof value === 'string' ? [path] : flatten(value, path)
  })
}

let hasError = false

for (const ns of NAMESPACES) {
  const keySets = LANGUAGES.map((lang) => {
    const file = resolve(LOCALES_DIR, lang, `${ns}.json`)
    return new Set(flatten(JSON.parse(readFileSync(file, 'utf8')) as Json))
  })

  LANGUAGES.forEach((lang, i) => {
    keySets.forEach((other, j) => {
      if (i === j) return
      const missing = [...other].filter((key) => !keySets[i].has(key))
      if (missing.length > 0) {
        hasError = true
        console.error(`[${ns}] ${lang} 缺少 ${missing.length} 個鍵（存在於 ${LANGUAGES[j]}）：`)
        missing.forEach((key) => console.error(`  - ${key}`))
      }
    })
  })
}

if (hasError) {
  process.exit(1)
}

console.log(`i18n 檢查通過：${LANGUAGES.join('、')} 鍵值一致`)
