import type { TFunction } from 'i18next'
import { ApiError } from '../api/apiClient'

/** 依後端錯誤碼取得畫面文字（errors.*）；無對應翻譯時顯示通用失敗訊息 */
export function errorText(t: TFunction, error: unknown): string {
  if (error instanceof ApiError && error.code) {
    const key = `errors.${error.code}`
    const text = t(key)
    if (text !== key) return text
  }

  return t('common.actionFailed')
}
