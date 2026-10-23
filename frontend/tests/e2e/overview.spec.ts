import { expect, test } from '@playwright/test'
import { loginAs } from './helpers'

test.describe('總覽與頂部列（quickstart 情境 4、5、6）', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page)
    await page.goto('/')
  })

  test('需要處理置頂且主機異常排第一，四張統計卡片', async ({ page }) => {
    const headings = page.getByRole('heading', { level: 4 })
    await expect(headings.first()).toHaveText('需要處理')

    const firstItem = page.getByRole('list', { name: '需要處理' }).getByRole('listitem').first()
    await expect(firstItem).toContainText('健康檢查失敗')

    await expect(page.getByTestId(/^stat-/)).toHaveCount(4)
  })

  test('未同步節點顯示燈號與文字', async ({ page }) => {
    const row = page.getByRole('row', { name: /gw-03/ })
    await expect(row).toContainText('未同步')
  })

  test('頂部列顯示生效版本與草稿提示，按鈕提示尚未開放', async ({ page }) => {
    const header = page.getByRole('banner')
    await expect(header).toContainText('2026101901')
    await expect(header).toContainText('3 項變更尚未發佈')

    await header.getByRole('button', { name: /檢視並發佈/ }).click()
    await expect(page.getByText('此功能尚未開放').first()).toBeVisible()

    await header.getByRole('button', { name: /試打請求/ }).click()
    await expect(page.getByText('此功能尚未開放').first()).toBeVisible()
  })
})
