/** TLS 憑證到期提醒天數（規格 Tls.certExpiryNoticeDays） */
export const CERT_EXPIRY_NOTICE_DAYS = 30

/** 設定版本最多保留數（FR-13-02） */
export const MAX_RETAINED_VERSIONS = 20

/** API 呼叫逾時毫秒數 */
export const API_TIMEOUT_MS = 10000

/** API 基底路徑 */
export const API_BASE_PATH = '/api'

/** mock 資料來源模擬的網路延遲毫秒數 */
export const MOCK_DELAY_MS = 300

/** 帳號名稱最大長度 */
export const USERNAME_MAX_LENGTH = 64

export const ROLES = {
  admin: 'admin',
  user: 'user',
} as const

export type Role = (typeof ROLES)[keyof typeof ROLES]

export const SUPPORTED_LANGUAGES = ['zh-TW', 'en'] as const

export type Language = (typeof SUPPORTED_LANGUAGES)[number]

export const DEFAULT_LANGUAGE: Language = 'zh-TW'

/** 瀏覽器儲存鍵 */
export const STORAGE_KEYS = {
  auth: 'yes.auth',
  ui: 'yes.ui',
  mockScenario: 'yes.mock.scenario',
} as const

/** mock 資料情境：正常、空資料、載入失敗 */
export const MOCK_SCENARIOS = ['normal', 'empty', 'error'] as const

export type MockScenario = (typeof MOCK_SCENARIOS)[number]

/** mock 登入時指定唯讀角色的網址查詢參數 */
export const MOCK_ROLE_QUERY_PARAM = 'role'
