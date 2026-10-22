import { CERT_EXPIRY_NOTICE_DAYS } from '../../src/constants/app'
import { ROUTES } from '../../src/constants/routes'
import type { AttentionItem, DraftStatus, Overview, ServiceDetail } from '../../src/data/types'

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
}

export const NORMAL_DRAFT_STATUS: DraftStatus = { activeVersion: ACTIVE_VERSION, pendingChanges: PENDING_CHANGES }

export const EMPTY_DRAFT_STATUS: DraftStatus = { activeVersion: null, pendingChanges: 0 }

/** 生效版本號（mock 服務儲存的初始值） */
export const MOCK_ACTIVE_VERSION = ACTIVE_VERSION

/** 範例服務：單一主機群組與 A/B 測試各一 */
export const MOCK_SERVICES: ServiceDetail[] = [
  {
    id: 1,
    serviceKey: 'orders-api',
    displayName: '訂單 API',
    tags: ['order'],
    isActive: true,
    description: null,
    routes: [
      {
        id: 11,
        routeKey: 'orders-api-1',
        matchPath: '/api/orders/{**catch-all}',
        matchHosts: ['api.company.com'],
        matchMethods: ['GET', 'POST'],
        order: 100,
        pathRemovePrefix: '/api/orders',
        isActive: true,
        description: null,
      },
    ],
    routingMode: 'AbTest',
    cluster: null,
    hosts: [],
    experiment: {
      experimentKey: 'orders-api-ab',
      enabled: true,
      stickyCookieName: '.yes.exp.orders-api',
      stickyDays: 7,
      startAt: null,
      endAt: null,
      autoRollback: { metric: '5xxRate', thresholdPercent: 2, durationMin: 5, minSamples: 100 },
      variants: [
        {
          variantKey: 'A',
          weightPercent: 70,
          isControl: true,
          cluster: { clusterKey: '01JB8Z6Q3N0000000000000001', displayName: '訂單 API · A' },
          hosts: [{ id: 21, address: 'http://192.168.1.101:5001', isActive: true }],
        },
        {
          variantKey: 'B',
          weightPercent: 30,
          isControl: false,
          cluster: { clusterKey: '01JB8Z6Q3N0000000000000002', displayName: '訂單 API · B' },
          hosts: [{ id: 22, address: 'http://192.168.1.103:5001', isActive: true }],
        },
      ],
      forceRules: [{ matchType: 'Header', matchName: 'X-Exp-Group', matchValue: null, variantKey: null }],
      effectiveStatus: 'Running',
      isAutoRolledBack: false,
    },
    updatedAt: '2026-10-19T05:40:00Z',
  },
  {
    id: 2,
    serviceKey: 'members-api',
    displayName: '會員 API',
    tags: ['member'],
    isActive: true,
    description: null,
    routes: [
      {
        id: 12,
        routeKey: 'members-api-1',
        matchPath: '/api/members/{**catch-all}',
        matchHosts: ['api.company.com'],
        matchMethods: [],
        order: 100,
        pathRemovePrefix: null,
        isActive: true,
        description: null,
      },
    ],
    routingMode: 'Single',
    cluster: { clusterKey: '01JB8Z6Q3N0000000000000003', displayName: '會員 API' },
    hosts: [{ id: 23, address: 'http://192.168.1.110:5002', isActive: true }],
    experiment: null,
    updatedAt: '2026-10-18T09:00:00Z',
  },
]
