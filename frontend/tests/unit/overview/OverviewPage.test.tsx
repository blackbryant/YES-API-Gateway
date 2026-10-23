import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { dataSource } from '../../../src/data/dataSource'
import { EMPTY_OVERVIEW, NORMAL_OVERVIEW } from '../../../src/data/mock/fixtures'
import OverviewPage from '../../../src/pages/overview/OverviewPage'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { getOverview: vi.fn(), getDraftStatus: vi.fn() },
}))

const getOverview = vi.mocked(dataSource.getOverview)

describe('OverviewPage', () => {
  beforeEach(() => {
    getOverview.mockReset()
  })

  it('四個區塊依序為需要處理、統計、節點同步、A/B 實驗', async () => {
    getOverview.mockResolvedValue(NORMAL_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    const headings = await screen.findAllByRole('heading', { level: 4 })
    expect(headings.map((h) => h.textContent)).toEqual([
      '需要處理',
      '統計',
      '閘道節點同步狀態',
      '進行中的 A/B 實驗',
    ])
  })

  it('需要處理依嚴重程度排序，主機異常排第一且有前往按鈕', async () => {
    getOverview.mockResolvedValue(NORMAL_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    const list = await screen.findByRole('list', { name: '需要處理' })
    const items = within(list).getAllByRole('listitem')
    expect(items[0]).toHaveTextContent('健康檢查失敗')
    expect(within(items[0]).getByRole('button', { name: /前往/ })).toBeInTheDocument()
  })

  it('未同步節點以燈號加文字標示', async () => {
    getOverview.mockResolvedValue(NORMAL_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    const row = (await screen.findByText('gw-03')).closest('tr')!
    expect(within(row).getByText('未同步')).toBeInTheDocument()
  })

  it('空資料時顯示空狀態文字，統計顯示 0', async () => {
    getOverview.mockResolvedValue(EMPTY_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    expect(await screen.findByText('目前沒有需要處理的事項')).toBeInTheDocument()
    expect(screen.getByText('目前沒有進行中的實驗')).toBeInTheDocument()
    expect(screen.getByTestId('stat-services')).toHaveTextContent('0')
  })

  it('載入失敗時顯示錯誤，點擊重新載入會再次呼叫', async () => {
    getOverview.mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce(NORMAL_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    await userEvent.click(await screen.findByRole('button', { name: /重新載入/ }))

    expect(await screen.findByText('gw-03')).toBeInTheDocument()
    expect(getOverview).toHaveBeenCalledTimes(2)
  })
})
