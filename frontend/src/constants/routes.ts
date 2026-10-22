/** 前端路由路徑（與 contracts/ui-routes.md 一致） */
export const ROUTES = {
  login: '/login',
  overview: '/',
  services: '/services',
  serviceNew: '/services/new',
  serviceDetail: '/services/:id',
  hosts: '/hosts',
  templates: '/templates',
  releases: '/releases',
  system: '/system',
} as const

export type RoutePath = (typeof ROUTES)[keyof typeof ROUTES]

/** 服務頁路徑 */
export const serviceDetailPath = (id: number | string) => `${ROUTES.services}/${id}`
