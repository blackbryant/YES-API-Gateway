import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { DEFAULT_LANGUAGE, STORAGE_KEYS, type Language } from '../constants/app'
import i18n from '../i18n'

interface UiState {
  language: Language
  setLanguage: (language: Language) => void
}

/** 介面偏好（語系），持久化於 localStorage */
export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      language: (i18n.language as Language) ?? DEFAULT_LANGUAGE,
      setLanguage: (language) => {
        void i18n.changeLanguage(language)
        set({ language })
      },
    }),
    { name: STORAGE_KEYS.ui },
  ),
)
