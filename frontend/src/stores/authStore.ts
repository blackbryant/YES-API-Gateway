import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import { STORAGE_KEYS } from '../constants/app'
import { dataSource } from '../data/dataSource'
import type { UserSession } from '../data/types'

interface AuthState {
  session: UserSession | null
  login: (username: string, password: string) => Promise<void>
  logout: () => Promise<void>
}

/**
 * 使用者工作階段，持久化於 localStorage 以便重新整理後維持登入（FR-021）。
 * 只保存帳號、角色、登入時間，不保存密碼或權杖（research R4）。
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      session: null,
      login: async (username, password) => {
        const session = await dataSource.login(username, password)
        set({ session: { username: session.username, role: session.role, loggedInAt: session.loggedInAt } })
      },
      logout: async () => {
        try {
          await dataSource.logout()
        } catch (error) {
          // 伺服器端登出失敗時仍清除本地狀態，避免使用者卡在已登入畫面
          console.warn('[auth] 伺服器端登出失敗，已清除本地工作階段', error)
        }
        set({ session: null })
      },
    }),
    {
      name: STORAGE_KEYS.auth,
      partialize: ({ session }) => ({ session }),
    },
  ),
)
