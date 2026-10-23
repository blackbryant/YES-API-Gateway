import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import { DEFAULT_LANGUAGE, STORAGE_KEYS, SUPPORTED_LANGUAGES, type Language } from '../constants/app'
import en from './locales/en/common.json'
import zhTW from './locales/zh-TW/common.json'

/** 讀取 uiStore 持久化的語系；讀不到時使用預設語系 */
function readStoredLanguage(): Language {
  try {
    const raw = localStorage.getItem(STORAGE_KEYS.ui)
    const lang = raw ? JSON.parse(raw)?.state?.language : undefined
    return SUPPORTED_LANGUAGES.includes(lang) ? lang : DEFAULT_LANGUAGE
  } catch {
    return DEFAULT_LANGUAGE
  }
}

void i18n.use(initReactI18next).init({
  resources: {
    'zh-TW': { common: zhTW },
    en: { common: en },
  },
  lng: readStoredLanguage(),
  fallbackLng: DEFAULT_LANGUAGE,
  defaultNS: 'common',
  ns: ['common'],
  interpolation: { escapeValue: false },
  // 開發模式缺少翻譯鍵時警告（憲法六）
  saveMissing: import.meta.env.DEV,
  missingKeyHandler: (lngs, ns, key) => {
    console.warn(`[i18n] 缺少翻譯鍵：${ns}:${key}（${lngs.join(', ')}）`)
  },
})

export default i18n
