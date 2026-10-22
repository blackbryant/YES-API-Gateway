import type { ExperimentInput, HostInput, HttpMethod, RouteInput, ServiceDetail, ServiceSaveRequest } from '../../data/types'

// 服務表單的常數與轉換（規則同後端 RouteRules / ExperimentRules）

export const HTTP_METHODS: HttpMethod[] = ['GET', 'POST', 'PUT', 'DELETE', 'PATCH']
export const DEFAULT_ROUTE_ORDER = 100
export const MIN_ROUTE_ORDER = 0
export const MAX_ROUTE_ORDER = 10000
export const SERVICE_KEY_PATTERN = /^[a-z0-9][a-z0-9-]{1,62}$/
export const SERVICE_KEY_MAX_LENGTH = 63
export const DISPLAY_NAME_MAX_LENGTH = 100
export const WEIGHT_TOTAL = 100
export const MAX_VARIANTS = 5
export const DEFAULT_STICKY_DAYS = 7
export const MAX_STICKY_DAYS = 365
export const DEFAULT_FORCE_HEADER = 'X-Exp-Group'
export const DEFAULT_ROLLBACK = { thresholdPercent: 2, durationMin: 5, minSamples: 100 }
export const MAX_ROLLBACK_DURATION_MIN = 60
export const VARIANT_KEYS = ['A', 'B', 'C', 'D', 'E']

export const emptyHost = (): HostInput => ({ id: null, address: '', isActive: true })

export const emptyRoute = (): RouteInput => ({
  id: null,
  matchPath: '',
  matchHosts: [],
  matchMethods: [],
  order: DEFAULT_ROUTE_ORDER,
  pathRemovePrefix: null,
  isActive: true,
  description: null,
})

/** 依服務名稱建議服務代碼：英數轉小寫、其他字元改為連字號 */
export function suggestServiceKey(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, SERVICE_KEY_MAX_LENGTH)
}

/** 移除路徑前綴的改寫範例（/api/orders/{**catch-all} + /api/orders → /api/orders/123 → /123） */
export function prefixExample(path: string, prefix: string): { before: string; after: string } {
  const before = path.replace(/\{\*\*[^}]*\}/g, '123').replace(/\{[^}]*\}/g, '123')
  const after = before.toLowerCase().startsWith(prefix.toLowerCase()) ? before.slice(prefix.length) || '/' : before
  return { before, after }
}

/** 以小寫並抹除參數名稱的路徑判斷入口是否重疊（僅前端提示，最終以發佈驗證為準） */
export const normalizePath = (path: string) =>
  path
    .toLowerCase()
    .replace(/\{\*[^}]*\}/g, '{**}')
    .replace(/\{[^*}][^}]*\}/g, '{}')
    .replace(/\/+$/, '')

/** 去除路由名稱等唯讀欄位，只保留存檔需要的入口欄位 */
export function toRouteInput(route: RouteInput): RouteInput {
  return {
    id: route.id,
    matchPath: route.matchPath,
    matchHosts: route.matchHosts,
    matchMethods: route.matchMethods,
    order: route.order,
    pathRemovePrefix: route.pathRemovePrefix,
    isActive: route.isActive,
    description: route.description,
  }
}

/** 服務詳細 → 存檔請求 */
export function toSaveRequest(service: ServiceDetail): ServiceSaveRequest {
  return {
    serviceKey: service.serviceKey,
    displayName: service.displayName,
    tags: service.tags,
    isActive: service.isActive,
    description: service.description,
    routes: service.routes.map(toRouteInput),
    routingMode: service.routingMode,
    hosts: service.hosts,
    experiment: service.experiment ? toExperimentInput(service.experiment) : null,
  }
}

function toExperimentInput(experiment: NonNullable<ServiceDetail['experiment']>): ExperimentInput {
  return {
    enabled: experiment.enabled,
    stickyCookieName: experiment.stickyCookieName,
    stickyDays: experiment.stickyDays,
    startAt: experiment.startAt,
    endAt: experiment.endAt,
    autoRollback: experiment.autoRollback,
    variants: experiment.variants.map((v) => ({
      variantKey: v.variantKey,
      weightPercent: v.weightPercent,
      isControl: v.isControl,
      hosts: v.hosts,
    })),
    forceRules: experiment.forceRules,
  }
}

/** 由單一模式切換為 A/B：原主機成為 A 組（對照組），B 組 10% */
export function defaultExperiment(hosts: HostInput[]): ExperimentInput {
  return {
    enabled: true,
    stickyCookieName: null,
    stickyDays: DEFAULT_STICKY_DAYS,
    startAt: null,
    endAt: null,
    autoRollback: null,
    variants: [
      { variantKey: 'A', weightPercent: 90, isControl: true, hosts: hosts.length > 0 ? hosts : [emptyHost()] },
      { variantKey: 'B', weightPercent: 10, isControl: false, hosts: [emptyHost()] },
    ],
    forceRules: [{ matchType: 'Header', matchName: DEFAULT_FORCE_HEADER, matchValue: null, variantKey: null }],
  }
}
