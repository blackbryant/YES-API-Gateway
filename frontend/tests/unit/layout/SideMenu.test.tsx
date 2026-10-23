import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { getSelectedMenuKey, SideMenu } from '../../../src/components/layout/SideMenu'
import { renderWithProviders } from '../test-utils'

const SELECTED_CLASS = 'ant-menu-item-selected'

describe('SideMenu', () => {
  it('依序顯示六個選單項目', () => {
    renderWithProviders(<SideMenu />)

    expect(screen.getAllByRole('menuitem').map((item) => item.textContent)).toEqual([
      '總覽',
      '服務',
      '主機',
      '共用範本',
      '發佈紀錄',
      '系統',
    ])
  })

  it('目前路徑對應的項目呈選中狀態', () => {
    renderWithProviders(<SideMenu />, { route: '/services' })

    expect(screen.getByRole('menuitem', { name: /服務/ })).toHaveClass(SELECTED_CLASS)
    expect(screen.getByRole('menuitem', { name: /總覽/ })).not.toHaveClass(SELECTED_CLASS)
  })

  it('點選項目會切換路徑', async () => {
    renderWithProviders(<SideMenu />)

    await userEvent.click(screen.getByRole('menuitem', { name: /主機/ }))

    expect(screen.getByTestId('location')).toHaveTextContent('/hosts')
  })

  it.each([
    ['/', 'overview'],
    ['/services', 'services'],
    ['/services/123', 'services'],
    ['/system', 'system'],
    ['/abc', undefined],
    ['/servicesx', undefined],
  ])('路徑 %s 選中 %s', (pathname, key) => {
    expect(getSelectedMenuKey(pathname)).toBe(key)
  })
})
