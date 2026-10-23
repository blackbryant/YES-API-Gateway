import { API_BASE_PATH, API_TIMEOUT_MS } from '../constants/app'

/** API 錯誤（對應後端失敗回應 { success:false, code, message }） */
export class ApiError extends Error {
  readonly status: number
  /** 後端錯誤碼（ErrorCodes），畫面文字依此查 i18n */
  readonly code?: string

  constructor(status: number, code?: string, message?: string) {
    super(message ?? code ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

const HTTP_NO_CONTENT = 204

/** 後端統一回應：成功 { success:true, data }；失敗 { success:false, code, message } */
interface ApiEnvelope<T> {
  success: boolean
  data?: T
  code?: string
  message?: string
}

async function request<T>(method: 'GET' | 'POST', path: string, body?: unknown): Promise<T> {
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
      return undefined as T
    }

    const envelope = (await response.json().catch(() => ({ success: false }))) as ApiEnvelope<T>
    if (!response.ok || !envelope.success) {
      throw new ApiError(response.status, envelope.code, envelope.message)
    }

    return envelope.data as T
  } finally {
    clearTimeout(timer)
  }
}

/** 統一 API client（原生 fetch 封裝，憲法三） */
export const apiClient = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
}
