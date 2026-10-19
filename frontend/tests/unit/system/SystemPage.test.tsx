import { screen, within } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import SystemPage from '../../../src/pages/system/SystemPage'
import { renderWithProviders } from '../test-utils'

const SECTION_TITLES = [
  '帳號',
  '稽核日誌',
  '監聽端點',
  'TLS 憑證',
  'DDoS 全域參數',
  'IP 黑白名單',
  '隱藏內部端點',
  '分散式追蹤',
]

describe('SystemPage', () => {
  it('依序顯示八個區塊，每個區塊皆標示尚未開放', () => {
    renderWithProviders(<SystemPage />)

    const sections = screen.getAllByRole('region')
    expect(sections.map((s) => within(s).getByRole('heading', { level: 4 }).textContent)).toEqual(SECTION_TITLES)
    sections.forEach((section) => {
      expect(within(section).getByText('尚未開放')).toBeInTheDocument()
    })
  })

  it('監聽端點區塊註明由設定檔管理且僅供檢視', () => {
    renderWithProviders(<SystemPage />)

    const endpoints = screen.getByRole('region', { name: '監聽端點' })
    expect(within(endpoints).getByText('由設定檔管理、變更需重啟、此處僅供檢視')).toBeInTheDocument()
  })
})
