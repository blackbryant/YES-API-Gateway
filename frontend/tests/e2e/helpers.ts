import type { Page } from '@playwright/test'

const AUTH_STORAGE_KEY = 'yes.auth'

/**
 * 直接寫入登入狀態，讓非登入流程的測試不必每次操作登入頁。
 * 登入頁尚未實作前寫入也無副作用。
 */
export async function loginAs(page: Page, role: 'admin' | 'user' = 'admin', username = 'netadmin') {
  await page.addInitScript(
    ({ key, value }) => localStorage.setItem(key, value),
    {
      key: AUTH_STORAGE_KEY,
      value: JSON.stringify({
        state: { session: { username, role, loggedInAt: new Date().toISOString() } },
        version: 0,
      }),
    },
  )
}
