import type { ReactNode } from 'react'
import type { Role } from '../../constants/app'
import { useAuthStore } from '../../stores/authStore'

interface RoleGateProps {
  allow: Role[]
  children: ReactNode
}

/** 依角色決定是否顯示內容；唯讀使用者看不到編輯與發佈按鈕（FR-019） */
export function RoleGate({ allow, children }: RoleGateProps) {
  const role = useAuthStore((state) => state.session?.role)

  return role && allow.includes(role) ? <>{children}</> : null
}
