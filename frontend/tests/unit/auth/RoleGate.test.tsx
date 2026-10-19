import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { RoleGate } from '../../../src/components/auth/RoleGate'
import { useAuthStore } from '../../../src/stores/authStore'

describe('RoleGate', () => {
  beforeEach(() => {
    useAuthStore.setState({ session: null })
  })

  it('角色符合時顯示內容', () => {
    useAuthStore.setState({ session: { username: 'a', role: 'admin', loggedInAt: '' } })
    render(<RoleGate allow={['admin']}>發佈</RoleGate>)

    expect(screen.getByText('發佈')).toBeInTheDocument()
  })

  it('角色不符時不顯示', () => {
    useAuthStore.setState({ session: { username: 'a', role: 'user', loggedInAt: '' } })
    render(<RoleGate allow={['admin']}>發佈</RoleGate>)

    expect(screen.queryByText('發佈')).not.toBeInTheDocument()
  })

  it('未登入時不顯示', () => {
    render(<RoleGate allow={['admin', 'user']}>內容</RoleGate>)

    expect(screen.queryByText('內容')).not.toBeInTheDocument()
  })
})
