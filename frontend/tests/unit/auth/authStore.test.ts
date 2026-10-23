import { beforeEach, describe, expect, it, vi } from 'vitest'
import { STORAGE_KEYS } from '../../../src/constants/app'
import { dataSource } from '../../../src/data/dataSource'
import { useAuthStore } from '../../../src/stores/authStore'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { login: vi.fn(), logout: vi.fn() },
}))

const SECRET = 'P@ssw0rd-secret'

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.setState({ session: null })
    vi.mocked(dataSource.login).mockResolvedValue({
      username: 'netadmin',
      role: 'admin',
      loggedInAt: '2026-10-19T06:00:00Z',
    })
    vi.mocked(dataSource.logout).mockResolvedValue()
  })

  it('login 後保存帳號、角色、登入時間', async () => {
    await useAuthStore.getState().login('netadmin', SECRET)

    expect(dataSource.login).toHaveBeenCalledWith('netadmin', SECRET)
    expect(useAuthStore.getState().session).toEqual({
      username: 'netadmin',
      role: 'admin',
      loggedInAt: '2026-10-19T06:00:00Z',
    })
  })

  it('持久化內容不含密碼或權杖', async () => {
    await useAuthStore.getState().login('netadmin', SECRET)

    const stored = localStorage.getItem(STORAGE_KEYS.auth) ?? ''
    expect(stored).toContain('netadmin')
    expect(stored).not.toContain(SECRET)
    expect(stored.toLowerCase()).not.toContain('token')
    expect(stored.toLowerCase()).not.toContain('password')
  })

  it('logout 清除工作階段', async () => {
    await useAuthStore.getState().login('netadmin', SECRET)
    await useAuthStore.getState().logout()

    expect(dataSource.logout).toHaveBeenCalled()
    expect(useAuthStore.getState().session).toBeNull()
  })

  it('logout 呼叫失敗仍清除本地工作階段', async () => {
    await useAuthStore.getState().login('netadmin', SECRET)
    vi.mocked(dataSource.logout).mockRejectedValueOnce(new Error('network'))

    await useAuthStore.getState().logout()

    expect(useAuthStore.getState().session).toBeNull()
  })
})
