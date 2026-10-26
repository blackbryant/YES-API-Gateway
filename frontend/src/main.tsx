import '@ant-design/v5-patch-for-react-19'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { CHUNK_RELOAD_COOLDOWN_MS, STORAGE_KEYS } from './constants/app'
import './i18n'
import './index.css'

// 重新部署後舊頁面仍引用舊檔名的 chunk，載入失敗時重新整理一次以取得新版 index.html
window.addEventListener('vite:preloadError', (event) => {
  try {
    const lastReloadAt = Number(sessionStorage.getItem(STORAGE_KEYS.chunkReloadAt))
    if (Date.now() - lastReloadAt < CHUNK_RELOAD_COOLDOWN_MS) return
    sessionStorage.setItem(STORAGE_KEYS.chunkReloadAt, String(Date.now()))
  } catch {
    // 無法使用 sessionStorage 時不自動重新整理，避免無限循環
    return
  }
  event.preventDefault()
  window.location.reload()
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
