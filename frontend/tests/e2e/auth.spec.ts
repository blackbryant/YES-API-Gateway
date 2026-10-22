import { expect, test, type Page } from '@playwright/test'

async function submitLogin(page: Page, username = 'netadmin', password = 'secret') {
  await page.getByLabel('帳號').fill(username)
  await page.getByLabel('密碼').fill(password)
  await page.getByRole('button', { name: '登入' }).click()
}

/** 等頂部列的草稿狀態載入完成（載入後版面會變動），再開啟使用者選單 */
async function openUserMenu(page: Page) {
  await expect(page.getByRole('banner')).toContainText('2026101901')
  await page.getByRole('button', { name: /使用者選單/ }).click()
}

test.describe('登入流程（quickstart 情境 1、2、3、9、10、11、12）', () => {
  test('未登入開啟頁面導向登入，登入後回到原頁', async ({ page }) => {
    await page.goto('/hosts')
    await expect(page).toHaveURL(/\/login$/)

    await expect(page.getByRole('button', { name: '登入' })).toBeDisabled()
    await page.getByLabel('帳號').fill('netadmin')
    await expect(page.getByRole('button', { name: '登入' })).toBeDisabled()

    await page.getByLabel('密碼').fill('secret')
    await page.getByRole('button', { name: '登入' }).click()

    await expect(page).toHaveURL('/hosts')
    await expect(page.getByText('建置中', { exact: true })).toBeVisible()
    const header = page.getByRole('banner')
    await expect(header).toContainText('netadmin')
    await expect(header).toContainText('管理員')
  })

  test('重新整理後維持登入', async ({ page }) => {
    await page.goto('/login')
    await submitLogin(page)
    await expect(page).toHaveURL('/')

    await page.reload()
    await expect(page).toHaveURL('/')
    await expect(page.getByRole('heading', { level: 3, name: '總覽' })).toBeVisible()
  })

  test('切換為英文不會重新整理頁面', async ({ page }) => {
    await page.goto('/login')
    await submitLogin(page)
    await expect(page.getByRole('heading', { level: 3, name: '總覽' })).toBeVisible()

    await page.evaluate(() => ((window as unknown as { __noReload: boolean }).__noReload = true))
    await openUserMenu(page)
    await page.getByRole('menuitem', { name: 'English' }).click()

    await expect(page.getByRole('heading', { level: 3, name: 'Overview' })).toBeVisible()
    expect(await page.evaluate(() => (window as unknown as { __noReload?: boolean }).__noReload)).toBe(true)
  })

  test('登出後按上一頁仍停在登入頁', async ({ page }) => {
    await page.goto('/login')
    await submitLogin(page)
    await page.getByRole('menu', { name: '主選單' }).getByRole('menuitem', { name: '主機' }).click()
    await expect(page).toHaveURL('/hosts')

    await openUserMenu(page)
    await page.getByRole('menuitem', { name: '登出' }).click()
    await expect(page).toHaveURL(/\/login$/)

    await page.goBack()
    await expect(page).toHaveURL(/\/login$/)
    await expect(page.getByRole('button', { name: '登入' })).toBeVisible()
  })

  test('以 role=user 登入為唯讀使用者，看不到發佈按鈕', async ({ page }) => {
    await page.goto('/login?role=user')
    await submitLogin(page, 'viewer')

    const header = page.getByRole('banner')
    await expect(header).toContainText('唯讀使用者')
    await expect(header).toContainText('2026101901')
    await expect(header.getByRole('button', { name: '發佈' })).toHaveCount(0)
  })
})
