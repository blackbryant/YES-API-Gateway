import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../src/api/apiClient'
import type { Role } from '../../../src/constants/app'
import { dataSource } from '../../../src/data/dataSource'
import { MOCK_SERVICES } from '../../mocks/fixtures'
import ServiceWizardPage from '../../../src/pages/services/ServiceWizardPage'
import { useAuthStore } from '../../../src/stores/authStore'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { createService: vi.fn() },
}))

const createService = vi.mocked(dataSource.createService)

function setSession(role: Role) {
  useAuthStore.setState({ session: { username: 'netadmin', role, loggedInAt: '2026-10-22T06:00:00Z' } })
}

async function fillAndSubmit(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('服務名稱'), 'Orders API')
  await user.click(screen.getByRole('button', { name: '下一步' }))

  await user.type(screen.getByLabelText('路徑'), '/api/orders/{{**catch-all}')
  await user.click(screen.getByRole('checkbox', { name: 'GET' }))
  await user.click(screen.getByRole('button', { name: '下一步' }))

  await user.type(screen.getByLabelText('主機位址 1'), 'http://10.0.0.1:8080')
  await user.click(screen.getByRole('button', { name: '建立服務' }))
}

describe('ServiceWizardPage', () => {
  beforeEach(() => {
    createService.mockReset()
    setSession('admin')
  })

  it('服務代碼依名稱自動建議', async () => {
    const user = userEvent.setup()
    renderWithProviders(<ServiceWizardPage />, { route: '/services/new' })

    await user.type(screen.getByLabelText('服務名稱'), 'Orders API')

    expect(screen.getByLabelText('服務代碼')).toHaveValue('orders-api')
  })

  it('必填未完成時不可前往下一步', () => {
    renderWithProviders(<ServiceWizardPage />, { route: '/services/new' })

    expect(screen.getByRole('button', { name: '下一步' })).toBeDisabled()
  })

  it('送出正確的服務內容並導向服務頁', async () => {
    const user = userEvent.setup()
    createService.mockResolvedValue({ ...MOCK_SERVICES[1], id: 42 })
    renderWithProviders(<ServiceWizardPage />, { route: '/services/new' })

    await fillAndSubmit(user)

    expect(createService).toHaveBeenCalledWith(
      expect.objectContaining({
        serviceKey: 'orders-api',
        displayName: 'Orders API',
        routingMode: 'Single',
        routes: [expect.objectContaining({ matchPath: '/api/orders/{**catch-all}', matchMethods: ['GET'] })],
        hosts: [{ id: null, address: 'http://10.0.0.1:8080', isActive: true }],
      }),
    )
    await waitFor(() => expect(screen.getByTestId('location')).toHaveTextContent('/services/42'))
    expect(await screen.findByText('已存為草稿，按右上角「發佈」後生效')).toBeInTheDocument()
  })

  it('後端錯誤以錯誤碼對應的文字提示', async () => {
    const user = userEvent.setup()
    createService.mockRejectedValue(new ApiError(409, 'SERVICES_KEY_EXISTS'))
    renderWithProviders(<ServiceWizardPage />, { route: '/services/new' })

    await fillAndSubmit(user)

    expect(await screen.findByText('服務代碼已被使用')).toBeInTheDocument()
    expect(screen.getByTestId('location')).toHaveTextContent('/services/new')
  })

  it('唯讀角色導回服務列表', () => {
    setSession('user')
    renderWithProviders(<ServiceWizardPage />, { route: '/services/new' })

    expect(screen.getByTestId('location')).toHaveTextContent('/services')
    expect(screen.queryByLabelText('服務名稱')).not.toBeInTheDocument()
  })
})
