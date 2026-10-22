import { screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Role } from '../../../src/constants/app'
import { dataSource } from '../../../src/data/dataSource'
import type { PagedResult, ServiceSummary } from '../../../src/data/types'
import ServicesPage from '../../../src/pages/services/ServicesPage'
import { useAuthStore } from '../../../src/stores/authStore'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { searchServices: vi.fn(), getSystemRoutes: vi.fn() },
}))

const searchServices = vi.mocked(dataSource.searchServices)
const getSystemRoutes = vi.mocked(dataSource.getSystemRoutes)

const SERVICES: ServiceSummary[] = [
  {
    id: 1,
    serviceKey: 'orders-api',
    displayName: '訂單 API',
    tags: ['order'],
    isActive: true,
    routeCount: 2,
    hosts: ['api.company.com'],
    routingMode: 'AbTest',
    hasUnpublishedChanges: true,
    updatedAt: '2026-10-22T00:00:00Z',
  },
  {
    id: 2,
    serviceKey: 'legacy-api',
    displayName: '舊版 API',
    tags: [],
    isActive: false,
    routeCount: 1,
    hosts: [],
    routingMode: 'Single',
    hasUnpublishedChanges: false,
    updatedAt: '2026-10-20T00:00:00Z',
  },
]

const page = (items: ServiceSummary[]): PagedResult<ServiceSummary> => ({
  items,
  pageIndex: 1,
  pageSize: 20,
  totalCount: items.length,
  totalPages: 1,
})

function setSession(role: Role) {
  useAuthStore.setState({ session: { username: 'netadmin', role, loggedInAt: '2026-10-22T06:00:00Z' } })
}

describe('ServicesPage', () => {
  beforeEach(() => {
    searchServices.mockReset()
    getSystemRoutes.mockReset().mockResolvedValue({ healthCheckPath: '/health', reservedPathPrefixes: ['/health', '/api/v1'], staticRoutes: [] })
    setSession('admin')
  })

  it('列出服務欄位、未發佈標記與狀態文字', async () => {
    searchServices.mockResolvedValue(page(SERVICES))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    const row = (await screen.findByText('訂單 API')).closest('tr')!
    expect(within(row).getByText('未發佈')).toBeInTheDocument()
    expect(within(row).getByText('orders-api')).toBeInTheDocument()
    expect(within(row).getByText('api.company.com')).toBeInTheDocument()
    expect(within(row).getByText('A/B 測試')).toBeInTheDocument()
    expect(within(row).getByText('啟用')).toBeInTheDocument()

    const inactive = screen.getByText('舊版 API').closest('tr')!
    expect(within(inactive).getByText('停用')).toBeInTheDocument()
  })

  it('以關鍵字搜尋並重設到第一頁', async () => {
    searchServices.mockResolvedValue(page(SERVICES))
    renderWithProviders(<ServicesPage />, { route: '/services' })
    await screen.findByText('訂單 API')

    await userEvent.type(screen.getByRole('searchbox', { name: '搜尋名稱、代碼、網域或路徑' }), 'orders{Enter}')

    await waitFor(() =>
      expect(searchServices).toHaveBeenLastCalledWith({ keyword: 'orders', tag: undefined, pageIndex: 1, pageSize: 20 }),
    )
  })

  it('點選列進入服務頁', async () => {
    searchServices.mockResolvedValue(page(SERVICES))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    await userEvent.click(await screen.findByText('訂單 API'))

    expect(screen.getByTestId('location')).toHaveTextContent('/services/1')
  })

  it('沒有服務時顯示空狀態與新增引導（admin）', async () => {
    searchServices.mockResolvedValue(page([]))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    expect(await screen.findByText('目前還沒有服務，點「新增服務」建立第一支對外 API')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /新增服務/ })).toHaveLength(2)
  })

  it('唯讀使用者看不到新增服務', async () => {
    setSession('user')
    searchServices.mockResolvedValue(page([]))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    await screen.findByText('目前還沒有服務，點「新增服務」建立第一支對外 API')
    expect(screen.queryByRole('button', { name: /新增服務/ })).not.toBeInTheDocument()
  })

  it('載入失敗可重新載入', async () => {
    searchServices.mockRejectedValueOnce(new Error('boom')).mockResolvedValue(page(SERVICES))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    await userEvent.click(await screen.findByRole('button', { name: /重新載入/ }))

    expect(await screen.findByText('訂單 API')).toBeInTheDocument()
  })

  it('系統路由區塊顯示健康檢查與保留路徑', async () => {
    searchServices.mockResolvedValue(page(SERVICES))
    renderWithProviders(<ServicesPage />, { route: '/services' })

    await userEvent.click(await screen.findByText('系統路由（唯讀）'))

    expect(await screen.findByText('/api/v1')).toBeInTheDocument()
    expect(screen.getAllByText('/health').length).toBeGreaterThan(0)
  })
})
