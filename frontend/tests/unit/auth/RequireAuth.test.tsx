import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'
import { RequireAuth } from '../../../src/components/auth/RequireAuth'
import { useAuthStore } from '../../../src/stores/authStore'

function LoginStub() {
  const location = useLocation()
  return <div>登入頁，來源：{(location.state as { from?: string } | null)?.from}</div>
}

function renderAt(path: string) {
  render(
    <MemoryRouter initialEntries={[path]}>
      <Routes>
        <Route path="/login" element={<LoginStub />} />
        <Route element={<RequireAuth />}>
          <Route path="/hosts" element={<div>主機頁</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('RequireAuth', () => {
  beforeEach(() => {
    useAuthStore.setState({ session: null })
  })

  it('未登入時導向登入頁並記住原路徑', () => {
    renderAt('/hosts')

    expect(screen.getByText('登入頁，來源：/hosts')).toBeInTheDocument()
    expect(screen.queryByText('主機頁')).not.toBeInTheDocument()
  })

  it('已登入時顯示受保護頁面', () => {
    useAuthStore.setState({ session: { username: 'netadmin', role: 'admin', loggedInAt: '' } })
    renderAt('/hosts')

    expect(screen.getByText('主機頁')).toBeInTheDocument()
  })
})
