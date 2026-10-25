/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { defineConfig, loadEnv } from 'vite'

export const DEV_SERVER_PORT = 5180
/** Playwright 端對端測試專用的開發伺服器埠（範例資料，與串接後端的 5180 分開） */
export const E2E_SERVER_PORT = 5181
/** 管理介面路徑前綴，與後端 AuthRules.AdminUiPrefix 一致；其餘路徑留給閘道入口 */
export const ADMIN_BASE_PATH = '/admin/'
/** 未設定 VITE_API_PROXY_TARGET 時，/api 轉送的後端位址 */
const DEFAULT_API_PROXY_TARGET = 'http://localhost:5072'
const PREVIEW_SERVER_PORT = 4180
const DATA_SOURCE_IMPL_ALIAS = '~data-source-impl'
/** Playwright 端對端測試的模式（npm run dev:e2e） */
const E2E_MODE = 'e2e'

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')
  // 範例資料（含假登入）只放在 tests/mocks，僅單元測試（vitest）與端對端測試使用，管理介面一律串接後端（research R3）
  const useMockDataSource = mode === E2E_MODE || process.env.VITEST === 'true'

  return {
    base: ADMIN_BASE_PATH,
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        [DATA_SOURCE_IMPL_ALIAS]: fileURLToPath(
          new URL(
            useMockDataSource ? './tests/mocks/mockDataSource.ts' : './src/data/http/httpDataSource.ts',
            import.meta.url,
          ),
        ),
      },
    },
    // dayjs 只在懶載入的服務頁使用，預先打包避免開發伺服器首次載入該頁時重新整理
    optimizeDeps: { include: ['dayjs'] },
    // 固定使用 5180，避免與本機其他 Vite 專案（預設 5173）衝突
    // /api 轉送到後端，與管理介面同源，登入 Cookie（SameSite=Strict、Path=/api）才能正常帶上
    server: {
      port: DEV_SERVER_PORT,
      strictPort: true,
      proxy: { '/api': { target: env.VITE_API_PROXY_TARGET || DEFAULT_API_PROXY_TARGET, changeOrigin: true } },
    },
    preview: { port: PREVIEW_SERVER_PORT, strictPort: true },
    test: {
      environment: 'jsdom',
      setupFiles: ['tests/setup.ts'],
      include: ['tests/unit/**/*.test.{ts,tsx}'],
      css: false,
    },
  }
})
