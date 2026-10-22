import { defineConfig, devices } from '@playwright/test'
import { E2E_SERVER_PORT } from './vite.config.ts'

// 端對端測試使用範例資料，另開 5181 埠，不會重用串接後端的開發伺服器（5180）
const BASE_URL = `http://localhost:${E2E_SERVER_PORT}`

export default defineConfig({
  testDir: 'tests/e2e',
  fullyParallel: true,
  // 整體執行上限，避免異常時無限等待
  globalTimeout: 5 * 60 * 1000,
  // 開發伺服器在平行負載下首次編譯頁面模組較慢，放寬等待並限制平行數
  expect: { timeout: 10_000 },
  workers: 4,
  reporter: 'list',
  use: {
    baseURL: BASE_URL,
    locale: 'zh-TW',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
  webServer: {
    command: 'npm run dev:e2e',
    url: BASE_URL,
    reuseExistingServer: true,
  },
})
