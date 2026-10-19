import { Navigate, Outlet, useLocation } from 'react-router'
import { ROUTES } from '../../constants/routes'
import { useAuthStore } from '../../stores/authStore'

/** 路由守衛：未登入導向登入頁，並記住原路徑供登入後導回（FR-016） */
export function RequireAuth() {
  const session = useAuthStore((state) => state.session)
  const location = useLocation()

  if (!session) {
    return <Navigate to={ROUTES.login} replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
