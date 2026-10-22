import { expect, test } from '@playwright/test'
import { loginAs } from './helpers'

test.describe('新增服務並發佈（quickstart §4，mock 資料來源）', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page)
  })

  test('新增服務 → 頂部列變更數 1 → 發佈 → 顯示新版本且變更數歸零', async ({ page }) => {
    await page.goto('/services/new')

    await page.getByLabel('服務名稱').fill('Payment API')
    await expect(page.getByLabel('服務代碼')).toHaveValue('payment-api')
    await page.getByRole('button', { name: '下一步' }).click()

    await page.getByRole('textbox', { name: '路徑' }).fill('/api/payments/{**catch-all}')
    await page.getByRole('checkbox', { name: 'POST' }).check()
    await page.getByRole('button', { name: '下一步' }).click()

    await page.getByLabel('主機位址 1').fill('http://10.0.0.21:8080')
    await page.getByRole('button', { name: '建立服務' }).click()

    await expect(page).toHaveURL(/\/services\/\d+$/)
    await expect(page.getByRole('heading', { name: 'Payment API' })).toBeVisible()

    const header = page.getByRole('banner')
    await expect(header).toContainText('1 項變更尚未發佈')

    await header.getByRole('button', { name: '發佈' }).click()
    const dialog = page.getByRole('dialog', { name: '發佈設定' })
    await expect(dialog).toContainText('Payment API')
    await dialog.getByRole('textbox', { name: '發佈說明' }).fill('上線支付 API')
    await dialog.getByRole('button', { name: '發佈' }).click()

    await expect(page.getByText(/已發佈版本 \d{10}/)).toBeVisible()
    await expect(header).toContainText('草稿與生效版本一致')
    await expect(header.getByRole('button', { name: '發佈' })).toBeDisabled()
  })
})
