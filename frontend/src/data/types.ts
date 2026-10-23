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

export type AttentionType = 'hostUnhealthy' | 'certExpiring' | 'nodeOutOfSync' | 'unpublishedChanges'

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

export interface ExperimentVariant {
  name: string
  percent: number
}

export interface ExperimentSummary {
  id: string
  name: string
  serviceName: string
  /** 至少 2 組，percent 加總為 100 */
  variants: ExperimentVariant[]
  hasUnhealthyHost: boolean
}

export interface Overview {
  attentionItems: AttentionItem[]
  stats: OverviewStats
  nodes: GatewayNode[]
  experiments: ExperimentSummary[]
}
