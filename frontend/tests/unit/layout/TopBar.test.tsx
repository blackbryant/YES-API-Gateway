import { screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { TopBar } from '../../../src/components/layout/TopBar'
import type { Role } from '../../../src/constants/app'
import { dataSource } from '../../../src/data/dataSource'
import { useAuthStore } from '../../../src/stores/authStore'
import { useUiStore } from '../../../src/stores/uiStore'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { getDraftStatus: vi.fn(), logout: vi.fn(), getPublishPreview: vi.fn(), publish: vi.fn() },
}))

const getDraftStatus = vi.mocked(dataSource.getDraftStatus)

function setSession(role: Role, username = 'netadmin') {
  useAuthStore.setState({ session: { username, role, loggedInAt: '2026-10-19T06:00:00Z' } })
}

describe('TopBar 草稿狀態', () => {
  beforeEach(() => {
    getDraftStatus.mockReset()
    setSession('admin')
  })

  it('有未發佈變更時顯示生效版本與草稿提示', async () => {
    getDraftStatus.mockResolvedValue({ activeVersion: '2026101901', pendingChanges: 3 })
    renderWithProviders(<TopBar />)

    expect(await screen.findByText('2026101901')).toBeInTheDocument()
    expect(screen.getByText('3 項變更尚未發佈')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '發佈' })).toBeEnabled()
  })

  it('沒有變更時顯示草稿與生效版本一致，發佈按鈕常駐但停用', async () => {
    getDraftStatus.mockResolvedValue({ activeVersion: '2026101901', pendingChanges: 0 })
    renderWithProviders(<TopBar />)

    expect(await screen.findByText('草稿與生效版本一致')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: '發佈' })).toBeDisabled()
  })

  it('尚無生效版本時顯示提示', async () => {
    getDraftStatus.mockResolvedValue({ activeVersion: null, pendingChanges: 0 })
    renderWithProviders(<TopBar />)

    expect(await screen.findByText('尚無生效版本')).toBeInTheDocument()
  })

  it('點發佈開啟發佈對話框', async () => {
    getDraftStatus.mockResolvedValue({ activeVersion: '2026101901', pendingChanges: 3 })
    vi.mocked(dataSource.getPublishPreview).mockResolvedValue({ baseVersion: '2026101901', draftToken: '1', changes: [], issues: [], warnings: [], canPublish: false })
    renderWithProviders(<TopBar />)

    await userEvent.click(await screen.findByRole('button', { name: '發佈' }))
    expect(await screen.findByRole('dialog', { name: '發佈設定' })).toBeInTheDocument()
  })

  it('點試打請求顯示尚未開放', async () => {
    getDraftStatus.mockResolvedValue({ activeVersion: '2026101901', pendingChanges: 3 })
    renderWithProviders(<TopBar />)

    await userEvent.click(await screen.findByRole('button', { name: /試打請求/ }))
    expect(await screen.findByText('此功能尚未開放')).toBeInTheDocument()
  })
})

describe('TopBar 使用者與角色', () => {
  beforeEach(() => {
    getDraftStatus.mockReset().mockResolvedValue({ activeVersion: '2026101901', pendingChanges: 3 })
    vi.mocked(dataSource.logout).mockResolvedValue()
    useUiStore.getState().setLanguage('zh-TW')
  })

  it('唯讀使用者看不到發佈按鈕', async () => {
    setSession('user')
    renderWithProviders(<TopBar />)

    expect(await screen.findByText('3 項變更尚未發佈')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: '發佈' })).not.toBeInTheDocument()
  })

  it('顯示帳號與角色，使用者選單含語系切換與登出', async () => {
    setSession('admin')
    renderWithProviders(<TopBar />)

    const trigger = screen.getByRole('button', { name: /使用者選單/ })
    expect(trigger).toHaveTextContent('netadmin')
    expect(trigger).toHaveTextContent('管理員')

    await userEvent.click(trigger)
    expect(await screen.findByText('English')).toBeInTheDocument()
    expect(screen.getByText('繁體中文')).toBeInTheDocument()
    expect(screen.getByText('登出')).toBeInTheDocument()
  })

  it('唯讀使用者的角色文字為唯讀使用者', () => {
    setSession('user')
    renderWithProviders(<TopBar />)

    expect(screen.getByRole('button', { name: /使用者選單/ })).toHaveTextContent('唯讀使用者')
  })

  it('帳號過長時截斷顯示並保留完整名稱', () => {
    const longName = 'a-very-long-network-administrator-account-name'
    setSession('admin', longName)
    renderWithProviders(<TopBar />)

    const name = screen.getByText(longName)
    expect(name).toHaveClass('truncate')
    expect(name).toHaveAttribute('title', longName)
  })

  it('選擇 English 後立即切換語系', async () => {
    setSession('admin')
    renderWithProviders(<TopBar />)

    await userEvent.click(screen.getByRole('button', { name: /使用者選單/ }))
    await userEvent.click(await screen.findByText('English'))

    expect(await screen.findByRole('button', { name: /Test request/ })).toBeInTheDocument()
    expect(useUiStore.getState().language).toBe('en')
  })

  it('登出後清除工作階段並回到登入頁', async () => {
    setSession('admin')
    renderWithProviders(<TopBar />, { route: '/hosts' })

    await userEvent.click(screen.getByRole('button', { name: /使用者選單/ }))
    await userEvent.click(await screen.findByText('登出'))

    expect(await screen.findByTestId('location')).toHaveTextContent('/login')
    expect(useAuthStore.getState().session).toBeNull()
  })
})
