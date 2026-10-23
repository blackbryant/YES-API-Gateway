import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { App as AntApp } from 'antd'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router'
import { dataSource } from '../../../src/data/dataSource'
import LoginPage from '../../../src/pages/login/LoginPage'
import { useAuthStore } from '../../../src/stores/authStore'
import { LocationDisplay } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { login: vi.fn(), logout: vi.fn() },
}))

function renderLogin(from?: string) {
  render(
    <AntApp>
      <MemoryRouter initialEntries={[{ pathname: '/login', state: from ? { from } : null }]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="*" element={<LocationDisplay />} />
        </Routes>
      </MemoryRouter>
    </AntApp>,
  )
}

const submitButton = () => screen.getByRole('button', { name: /登入/ })

describe('LoginPage', () => {
  beforeEach(() => {
    useAuthStore.setState({ session: null })
    vi.mocked(dataSource.login).mockResolvedValue({ username: 'netadmin', role: 'admin', loggedInAt: '' })
  })

  it('帳號或密碼任一為空時登入按鈕不可按', async () => {
    renderLogin()
    expect(submitButton()).toBeDisabled()

    await userEvent.type(screen.getByLabelText('帳號'), 'netadmin')
    expect(submitButton()).toBeDisabled()

    await userEvent.type(screen.getByLabelText('密碼'), 'secret')
    expect(submitButton()).toBeEnabled()
  })

  it('登入成功後導回原本要前往的頁面', async () => {
    renderLogin('/hosts')

    await userEvent.type(screen.getByLabelText('帳號'), 'netadmin')
    await userEvent.type(screen.getByLabelText('密碼'), 'secret')
    await userEvent.click(submitButton())

    expect(await screen.findByTestId('location')).toHaveTextContent('/hosts')
  })

  it('沒有原路徑時導向總覽', async () => {
    renderLogin()

    await userEvent.type(screen.getByLabelText('帳號'), 'netadmin')
    await userEvent.type(screen.getByLabelText('密碼'), 'secret')
    await userEvent.click(submitButton())

    expect(await screen.findByTestId('location')).toHaveTextContent(/^\/$/)
  })

  it('登入失敗時顯示錯誤訊息並停留在登入頁', async () => {
    vi.mocked(dataSource.login).mockRejectedValueOnce(new Error('401'))
    renderLogin()

    await userEvent.type(screen.getByLabelText('帳號'), 'netadmin')
    await userEvent.type(screen.getByLabelText('密碼'), 'wrong')
    await userEvent.click(submitButton())

    expect(await screen.findByText('登入失敗，請確認帳號密碼後再試一次')).toBeInTheDocument()
    expect(screen.queryByTestId('location')).not.toBeInTheDocument()
  })

  it('已登入時直接導向總覽', () => {
    useAuthStore.setState({ session: { username: 'netadmin', role: 'admin', loggedInAt: '' } })
    renderLogin()

    expect(screen.getByTestId('location')).toHaveTextContent(/^\/$/)
  })
})
