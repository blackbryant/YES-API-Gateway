/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { defineConfig, loadEnv } from 'vite'

export const DEV_SERVER_PORT = 5180
const PREVIEW_SERVER_PORT = 4180
const DATA_SOURCE_IMPL_ALIAS = '~data-source-impl'
/** 以範例資料建置正式最佳化版本的模式，僅供本機 Lighthouse 量測 */
const MOCK_PREVIEW_MODE = 'mock-preview'

// https://vite.dev/config/
export default defineConfig(({ command, mode }) => {
  const env = loadEnv(mode, process.cwd(), 'VITE_')

  // 假登入只存在於 mock 資料來源，不得進入正式環境（research R3）。
  // 唯一例外是明確指定的 mock-preview 模式，僅供本機效能量測，不得部署。
  if (command === 'build' && env.VITE_DATA_SOURCE !== 'api' && mode !== MOCK_PREVIEW_MODE) {
    throw new Error(
      `正式建置必須使用 VITE_DATA_SOURCE=api（目前為 "${env.VITE_DATA_SOURCE ?? ''}"）：mock 資料來源含假登入，不得進入正式環境。`,
    )
  }

  return {
    plugins: [react(), tailwindcss()],
    resolve: {
      alias: {
        // 依 VITE_DATA_SOURCE 於建置時選擇資料來源實作；正式建置不會打包 mock 與假登入（research R3）
        [DATA_SOURCE_IMPL_ALIAS]: fileURLToPath(
          new URL(
            env.VITE_DATA_SOURCE === 'api' ? './src/data/http/httpDataSource.ts' : './src/data/mock/mockDataSource.ts',
            import.meta.url,
          ),
        ),
      },
    },
    // 固定使用 5180，避免與本機其他 Vite 專案（預設 5173）衝突
    server: { port: DEV_SERVER_PORT, strictPort: true },
    preview: { port: PREVIEW_SERVER_PORT, strictPort: true },
    test: {
      environment: 'jsdom',
      setupFiles: ['tests/setup.ts'],
      include: ['tests/unit/**/*.test.{ts,tsx}'],
      css: false,
    },
  }
})
