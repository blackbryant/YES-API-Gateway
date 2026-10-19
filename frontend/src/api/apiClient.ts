import { API_BASE_PATH, API_TIMEOUT_MS } from '../constants/app'

/** API 錯誤（對應 RFC 9457 Problem Details） */
export class ApiError extends Error {
  readonly status: number
  readonly title?: string
  readonly detail?: string

  constructor(status: number, title?: string, detail?: string) {
    super(detail ?? title ?? `HTTP ${status}`)
    this.name = 'ApiError'
    this.status = status
    this.title = title
    this.detail = detail
  }
}

const HTTP_NO_CONTENT = 204

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

    if (!response.ok) {
      const problem = await response.json().catch(() => ({}))
      throw new ApiError(response.status, problem.title, problem.detail)
    }

    if (response.status === HTTP_NO_CONTENT) {
      return undefined as T
    }

    return (await response.json()) as T
  } finally {
    clearTimeout(timer)
  }
}

/** 統一 API client（原生 fetch 封裝，憲法三） */
export const apiClient = {
  get: <T>(path: string) => request<T>('GET', path),
  post: <T>(path: string, body?: unknown) => request<T>('POST', path, body),
}
