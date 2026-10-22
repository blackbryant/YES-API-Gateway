import { screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { dataSource } from '../../../src/data/dataSource'
import type { ExperimentSummaryItem } from '../../../src/data/types'
import { EMPTY_OVERVIEW, NORMAL_OVERVIEW } from '../../mocks/fixtures'
import OverviewPage from '../../../src/pages/overview/OverviewPage'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { getOverview: vi.fn(), getDraftStatus: vi.fn(), getExperimentSummary: vi.fn(), getExperimentAttention: vi.fn() },
}))

const getOverview = vi.mocked(dataSource.getOverview)
const getExperimentSummary = vi.mocked(dataSource.getExperimentSummary)
const getExperimentAttention = vi.mocked(dataSource.getExperimentAttention)

const EXPERIMENT: ExperimentSummaryItem = {
  experimentKey: 'orders-api-ab',
  serviceId: 1,
  serviceName: '訂單 API',
  effectiveStatus: 'Paused',
  isAutoRolledBack: true,
  triggeredAt: '2026-10-22T01:00:00Z',
  startAt: null,
  endAt: null,
  variants: [
    { variantKey: 'A', percent: 90, isControl: true },
    { variantKey: 'B', percent: 10, isControl: false },
  ],
  hasUnhealthyHost: false,
}

describe('OverviewPage', () => {
  beforeEach(() => {
    getOverview.mockReset()
    getExperimentSummary.mockReset().mockResolvedValue([])
    getExperimentAttention.mockReset().mockResolvedValue([])
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
    expect(await screen.findByText('目前沒有進行中的實驗')).toBeInTheDocument()
    expect(screen.getByTestId('stat-services')).toHaveTextContent('0')
  })

  it('載入失敗時顯示錯誤，點擊重新載入會再次呼叫', async () => {
    getOverview.mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce(NORMAL_OVERVIEW)
    renderWithProviders(<OverviewPage />)

    await userEvent.click(await screen.findByRole('button', { name: /重新載入/ }))

    expect(await screen.findByText('gw-03')).toBeInTheDocument()
    expect(getOverview).toHaveBeenCalledTimes(2)
  })

  it('進行中的 A/B 實驗顯示有效狀態與各組比例', async () => {
    getOverview.mockResolvedValue(EMPTY_OVERVIEW)
    getExperimentSummary.mockResolvedValue([EXPERIMENT])
    renderWithProviders(<OverviewPage />)

    expect(await screen.findByText('orders-api-ab')).toBeInTheDocument()
    expect(screen.getByText('已自動回退')).toBeInTheDocument()
    expect(screen.getByText('A 組 90%')).toBeInTheDocument()
    expect(screen.getByText('B 組 10%')).toBeInTheDocument()
  })

  it('自動回退的實驗列入需要處理（嚴重）並可前往服務頁', async () => {
    getOverview.mockResolvedValue(EMPTY_OVERVIEW)
    getExperimentAttention.mockResolvedValue([
      {
        id: 'experiment-orders-api-ab',
        type: 'experimentAutoRolledBack',
        severity: 'critical',
        params: { service: '訂單 API' },
        target: '/services/1',
        occurredAt: '2026-10-22T01:00:00Z',
      },
    ])
    renderWithProviders(<OverviewPage />)

    const list = await screen.findByRole('list', { name: '需要處理' })
    const item = await within(list).findByText('訂單 API 的 A/B 實驗已自動回退到對照組')
    expect(item.closest('li')).toHaveTextContent('嚴重')

    await userEvent.click(within(item.closest('li')!).getByRole('button', { name: /前往/ }))
    expect(screen.getByTestId('location')).toHaveTextContent('/services/1')
  })
})
