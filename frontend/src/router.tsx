import type { ComponentType } from 'react'
import { createBrowserRouter } from 'react-router'
import { RequireAuth } from './components/auth/RequireAuth'
import { PageLoading } from './components/common/PageLoading'
import { AppLayout } from './components/layout/AppLayout'
import { ROUTES } from './constants/routes'

/** 以 lazy 載入頁面模組（憲法七：懶載入） */
const page = (load: () => Promise<{ default: ComponentType }>) => async () => ({
  Component: (await load()).default,
})

export const router = createBrowserRouter([
  {
    HydrateFallback: PageLoading,
    children: [
      { path: ROUTES.login, lazy: page(() => import('./pages/login/LoginPage')) },
      {
        Component: RequireAuth,
        children: [
          {
            Component: AppLayout,
            children: [
              { index: true, lazy: page(() => import('./pages/overview/OverviewPage')) },
              { path: ROUTES.services, lazy: page(() => import('./pages/services/ServicesPage')) },
              { path: ROUTES.serviceNew, lazy: page(() => import('./pages/services/ServiceWizardPage')) },
              { path: ROUTES.serviceDetail, lazy: page(() => import('./pages/services/ServiceDetailPage')) },
              { path: ROUTES.hosts, lazy: page(() => import('./pages/hosts/HostsPage')) },
              { path: ROUTES.templates, lazy: page(() => import('./pages/templates/TemplatesPage')) },
              { path: ROUTES.releases, lazy: page(() => import('./pages/releases/ReleasesPage')) },
              { path: ROUTES.system, lazy: page(() => import('./pages/system/SystemPage')) },
              { path: '*', lazy: page(() => import('./pages/not-found/NotFoundPage')) },
            ],
          },
        ],
      },
    ],
  },
])
