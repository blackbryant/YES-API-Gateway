import type { Role } from '../constants/app'

/** 使用者工作階段（data-model.md UserSession） */
export interface UserSession {
  /** 帳號名稱，1 ～ 64 字元 */
  username: string
  role: Role
  /** 登入時間，ISO 8601 */
  loggedInAt: string
}

/** 頂部列草稿狀態 */
export interface DraftStatus {
  /** 生效版本號，格式 YYYYMMDD + 兩碼流水號；null 表示尚無生效版本 */
  activeVersion: string | null
  /** 未發佈變更數，≥ 0 */
  pendingChanges: number
}

export type AttentionType =
  | 'hostUnhealthy'
  | 'certExpiring'
  | 'nodeOutOfSync'
  | 'unpublishedChanges'
  | 'experimentAutoRolledBack'

export type Severity = 'critical' | 'warning' | 'info'

/** 待處理事項 */
export interface AttentionItem {
  id: string
  type: AttentionType
  severity: Severity
  /** 顯示文字參數，由 i18n 依 type 組成文字 */
  params: Record<string, string | number>
  /** 「前往」的路由路徑 */
  target: string
  /** 發生時間，ISO 8601 */
  occurredAt: string
}

export interface OverviewStats {
  serviceCount: number
  internalOnlyServiceCount: number
  healthyHostCount: number
  hostCount: number
  activeVersion: string | null
  /** 0 ～ 20 */
  retainedVersionCount: number
  syncedNodeCount: number
  nodeCount: number
}

export interface GatewayNode {
  name: string
  loadedVersion: string | null
  synced: boolean
}

export interface Overview {
  attentionItems: AttentionItem[]
  stats: OverviewStats
  nodes: GatewayNode[]
}

// ---- 001-02 服務、入口、A/B 實驗、發佈（contracts/admin-api.yaml）----

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH'

export type RoutingMode = 'Single' | 'AbTest'

/** 實驗有效狀態 */
export type ExperimentStatus = 'Draft' | 'Scheduled' | 'Running' | 'Paused' | 'Ended'

export type ForceRuleType = 'Header' | 'Cookie' | 'Query'

export interface RouteInput {
  id: number | null
  matchPath: string
  matchHosts: string[]
  /** 空陣列代表全部方法 */
  matchMethods: HttpMethod[]
  /** 0 ～ 10000，數字越小越優先 */
  order: number
  pathRemovePrefix: string | null
  isActive: boolean
  description: string | null
}

export interface HostInput {
  id: number | null
  address: string
  isActive: boolean
}

export interface VariantInput {
  variantKey: string
  /** 0 ～ 100，同一實驗加總為 100 */
  weightPercent: number
  isControl: boolean
  hosts: HostInput[]
}

export interface ForceRuleInput {
  matchType: ForceRuleType
  matchName: string
  /** 空值：以請求帶入的值作為組別代號 */
  matchValue: string | null
  variantKey: string | null
}

export interface AutoRollbackRule {
  metric?: string
  thresholdPercent: number
  durationMin: number
  minSamples: number
}

export interface ExperimentInput {
  enabled: boolean
  stickyCookieName: string | null
  stickyDays: number
  startAt: string | null
  endAt: string | null
  autoRollback: AutoRollbackRule | null
  variants: VariantInput[]
  forceRules: ForceRuleInput[]
}

export interface ServiceSaveRequest {
  serviceKey: string
  displayName: string
  tags: string[]
  isActive: boolean
  description: string | null
  routes: RouteInput[]
  routingMode: RoutingMode
  hosts: HostInput[] | null
  experiment: ExperimentInput | null
}

export interface RouteDetail extends RouteInput {
  id: number
  routeKey: string
}

export interface HostDetail extends HostInput {
  id: number
}

export interface ClusterInfo {
  /** ULID */
  clusterKey: string
  displayName: string
}

export interface VariantDetail {
  variantKey: string
  weightPercent: number
  isControl: boolean
  cluster: ClusterInfo
  hosts: HostDetail[]
}

export interface ExperimentDetail extends Omit<ExperimentInput, 'variants'> {
  experimentKey: string
  variants: VariantDetail[]
  effectiveStatus: ExperimentStatus
  isAutoRolledBack: boolean
}

export interface ServiceDetail {
  id: number
  serviceKey: string
  displayName: string
  tags: string[]
  isActive: boolean
  description: string | null
  routes: RouteDetail[]
  routingMode: RoutingMode
  cluster: ClusterInfo | null
  hosts: HostDetail[]
  experiment: ExperimentDetail | null
  updatedAt: string
}

export interface ServiceSummary {
  id: number
  serviceKey: string
  displayName: string
  tags: string[]
  isActive: boolean
  routeCount: number
  hosts: string[]
  routingMode: RoutingMode
  hasUnpublishedChanges: boolean
  updatedAt: string
}

export interface ServiceQuery {
  keyword?: string
  tag?: string
  pageIndex: number
  pageSize: number
}

export interface PagedResult<T> {
  items: T[]
  pageIndex: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type ChangeEntityType = 'Service' | 'Route' | 'Experiment' | 'Cluster' | 'Host'

export type ChangeAction = 'Added' | 'Modified' | 'Removed'

export interface ChangeItem {
  entityType: ChangeEntityType
  entityKey: string
  serviceId: number | null
  serviceName: string | null
  action: ChangeAction
}

export interface ValidationIssue {
  code: string
  serviceId: number | null
  serviceName: string | null
  routeKey: string | null
  field: string | null
  params: Record<string, string> | null
}

export interface PublishPreview {
  baseVersion: string | null
  draftToken: string
  changes: ChangeItem[]
  issues: ValidationIssue[]
  canPublish: boolean
}

export interface PublishResult {
  versionNo: string
  checksum: string
  publishedAt: string
  changeCount: number
}

export interface SystemRoutes {
  healthCheckPath: string
  reservedPathPrefixes: string[]
  staticRoutes: { routeId: string; path: string | null; clusterId: string | null }[]
}

export interface ExperimentSummaryItem {
  experimentKey: string
  serviceId: number
  serviceName: string
  effectiveStatus: ExperimentStatus
  isAutoRolledBack: boolean
  triggeredAt: string | null
  startAt: string | null
  endAt: string | null
  variants: { variantKey: string; percent: number; isControl: boolean }[]
  hasUnhealthyHost: boolean
}

export type ExperimentRuntimeAction = 'Pause' | 'Resume' | 'End'
