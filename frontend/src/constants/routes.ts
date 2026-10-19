/** 前端路由路徑（與 contracts/ui-routes.md 一致） */
export const ROUTES = {
  login: '/login',
  overview: '/',
  services: '/services',
  hosts: '/hosts',
  templates: '/templates',
  releases: '/releases',
  system: '/system',
} as const

export type RoutePath = (typeof ROUTES)[keyof typeof ROUTES]
