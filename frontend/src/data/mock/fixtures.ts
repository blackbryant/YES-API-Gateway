import { CERT_EXPIRY_NOTICE_DAYS } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import type { AttentionItem, DraftStatus, Overview } from '../types'

// 範例資料（參考規格 §5.4.1 與畫面原型），僅供 mock 資料來源使用

const ACTIVE_VERSION = '2026101901'
const PENDING_CHANGES = 3

/** 憑證清單；只有剩餘天數 ≤ CERT_EXPIRY_NOTICE_DAYS 的才列入需要處理 */
const CERTIFICATES = [
  { domain: 'api.company.com', daysLeft: 12 },
  { domain: 'partner.company.com', daysLeft: 85 },
]

const certAttention: AttentionItem[] = CERTIFICATES.filter(
  ({ daysLeft }) => daysLeft <= CERT_EXPIRY_NOTICE_DAYS,
).map(({ domain, daysLeft }) => ({
  id: `cert-${domain}`,
  type: 'certExpiring',
  severity: 'warning',
  params: { domain, daysLeft },
  target: ROUTES.system,
  occurredAt: '2026-10-19T00:00:00Z',
}))

export const NORMAL_OVERVIEW: Overview = {
  attentionItems: [
    {
      id: 'unpublished',
      type: 'unpublishedChanges',
      severity: 'info',
      params: { count: PENDING_CHANGES },
      target: ROUTES.releases,
      occurredAt: '2026-10-19T05:40:00Z',
    },
    {
      id: 'node-gw-03',
      type: 'nodeOutOfSync',
      severity: 'warning',
      params: { node: 'gw-03', version: '2026101801' },
      target: ROUTES.overview,
      occurredAt: '2026-10-19T04:10:00Z',
    },
    ...certAttention,
    {
      id: 'host-192.168.1.103',
      type: 'hostUnhealthy',
      severity: 'critical',
      params: { host: '192.168.1.103:5001', service: '訂單 API', group: 'B' },
      target: ROUTES.services,
      occurredAt: '2026-10-19T05:12:00Z',
    },
  ],
  stats: {
    serviceCount: 8,
    internalOnlyServiceCount: 2,
    healthyHostCount: 11,
    hostCount: 12,
    activeVersion: ACTIVE_VERSION,
    retainedVersionCount: 14,
    syncedNodeCount: 2,
    nodeCount: 3,
  },
  nodes: [
    { name: 'gw-01', loadedVersion: ACTIVE_VERSION, synced: true },
    { name: 'gw-02', loadedVersion: ACTIVE_VERSION, synced: true },
    { name: 'gw-03', loadedVersion: '2026101801', synced: false },
  ],
  experiments: [
    {
      id: 'exp-checkout',
      name: '訂單 API 新版結帳',
      serviceName: '訂單 API',
      variants: [
        { name: 'A', percent: 70 },
        { name: 'B', percent: 30 },
      ],
      hasUnhealthyHost: true,
    },
  ],
}

export const EMPTY_OVERVIEW: Overview = {
  attentionItems: [],
  stats: {
    serviceCount: 0,
    internalOnlyServiceCount: 0,
    healthyHostCount: 0,
    hostCount: 0,
    activeVersion: null,
    retainedVersionCount: 0,
    syncedNodeCount: 0,
    nodeCount: 0,
  },
  nodes: [],
  experiments: [],
}

export const NORMAL_DRAFT_STATUS: DraftStatus = { activeVersion: ACTIVE_VERSION, pendingChanges: PENDING_CHANGES }

export const EMPTY_DRAFT_STATUS: DraftStatus = { activeVersion: null, pendingChanges: 0 }
