/** TLS 憑證到期提醒天數（規格 Tls.certExpiryNoticeDays） */
export const CERT_EXPIRY_NOTICE_DAYS = 30

/** 設定版本最多保留數（FR-13-02） */
export const MAX_RETAINED_VERSIONS = 20

/** API 呼叫逾時毫秒數 */
export const API_TIMEOUT_MS = 10000

/** API 基底路徑 */
export const API_BASE_PATH = '/api/v1'

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
  chunkReloadAt: 'yes.chunkReloadAt',
} as const

/** 動態載入失敗後自動重新整理的最短間隔，避免檔案真的不存在時無限重新整理 */
export const CHUNK_RELOAD_COOLDOWN_MS = 10000


/** 系統標誌圖檔（public/yes-icon.svg），需帶上部署基底路徑 */
export const APP_ICON_URL = `${import.meta.env.BASE_URL}yes-icon.svg`
