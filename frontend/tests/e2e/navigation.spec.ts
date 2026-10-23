import { expect, test } from '@playwright/test'
import { loginAs } from './helpers'

const MENU = [
  { name: '總覽', path: '/', heading: '總覽' },
  { name: '服務', path: '/services', heading: '服務' },
  { name: '主機', path: '/hosts', heading: '主機' },
  { name: '共用範本', path: '/templates', heading: '共用範本' },
  { name: '發佈紀錄', path: '/releases', heading: '發佈紀錄' },
  { name: '系統', path: '/system', heading: '系統' },
]

test.describe('選單導覽（quickstart 情境 7、13、14）', () => {
  test.beforeEach(async ({ page }) => {
    await loginAs(page)
  })

  test('六個選單皆可切換，選中狀態正確', async ({ page }) => {
    await page.goto('/')
    const menu = page.getByRole('menu', { name: '主選單' })

    for (const item of MENU) {
      await menu.getByRole('menuitem', { name: item.name }).click()
      await expect(page).toHaveURL(item.path)
      await expect(page.getByRole('heading', { level: 3, name: item.heading })).toBeVisible()
      await expect(menu.getByRole('menuitem', { name: item.name })).toHaveClass(/ant-menu-item-selected/)
    }
  })

  test('可直接開啟網址，並支援上一頁 / 下一頁', async ({ page }) => {
    await page.goto('/hosts')
    await expect(page.getByRole('heading', { level: 3, name: '主機' })).toBeVisible()
    await expect(page.getByText('建置中', { exact: true })).toBeVisible()

    await page.getByRole('menu', { name: '主選單' }).getByRole('menuitem', { name: '系統' }).click()
    await expect(page).toHaveURL('/system')

    await page.goBack()
    await expect(page).toHaveURL('/hosts')
    await page.goForward()
    await expect(page).toHaveURL('/system')
  })

  test('不存在的網址顯示找不到頁面並可返回總覽', async ({ page }) => {
    await page.goto('/abc')
    await expect(page.getByText('找不到頁面')).toBeVisible()

    await page.getByRole('button', { name: '返回總覽' }).click()
    await expect(page).toHaveURL('/')
  })

  for (const width of [375, 768, 1920]) {
    test(`寬度 ${width}px 無水平捲動`, async ({ page }) => {
      await page.setViewportSize({ width, height: 900 })
      for (const { path } of MENU) {
        await page.goto(path)
        await expect(page.getByRole('heading', { level: 3 })).toBeVisible()
        const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth)
        expect(overflow, `${path} 水平溢出`).toBeLessThanOrEqual(0)
      }
    })
  }

  test('手機寬度時選單堆疊於內容上方', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 900 })
    await page.goto('/')

    const menuBox = await page.getByRole('menu', { name: '主選單' }).boundingBox()
    const headingBox = await page.getByRole('heading', { level: 3, name: '總覽' }).boundingBox()

    expect(menuBox!.y + menuBox!.height).toBeLessThanOrEqual(headingBox!.y)
    expect(menuBox!.width).toBeGreaterThan(300)
  })
})
