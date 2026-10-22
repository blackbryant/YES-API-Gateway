import type {
  AttentionItem,
  ChangeItem,
  ExperimentRuntimeAction,
  ExperimentStatus,
  ExperimentSummaryItem,
  PagedResult,
  PublishPreview,
  PublishResult,
  ServiceDetail,
  ServiceQuery,
  ServiceSaveRequest,
  ServiceSummary,
  SystemRoutes,
  ValidationIssue,
} from '../../src/data/types'
import { ApiError } from '../../src/api/apiClient'
import { ROUTES } from '../../src/constants/routes'
import { MOCK_ACTIVE_VERSION, MOCK_SERVICES } from './fixtures'

// 範例資料的記憶體儲存：模擬草稿 → 發佈流程（僅 mock 資料來源使用）

const WEIGHT_TOTAL = 100
/** 模擬後端錯誤碼對應的 HTTP 狀態碼 */
const MOCK_ERROR_STATUS: Record<string, number> = {
  SERVICES_KEY_EXISTS: 409,
  PUBLISH_DRAFT_CHANGED: 409,
  PUBLISH_NOTE_REQUIRED: 400,
  EXPERIMENTS_NOT_FOUND: 404,
}
const ULID_LENGTH = 26
const CROCKFORD = '0123456789ABCDEFGHJKMNPQRSTVWXYZ'

interface StoreState {
  services: ServiceDetail[]
  pending: ChangeItem[]
  activeVersion: string | null
  versionSeq: number
  nextId: number
  draftToken: number
  runtime: Record<string, ExperimentStatus>
}

const state: StoreState = {
  services: structuredClone(MOCK_SERVICES),
  pending: [],
  activeVersion: MOCK_ACTIVE_VERSION,
  versionSeq: 1,
  nextId: 1000,
  draftToken: 1,
  runtime: {},
}

const newId = () => ++state.nextId

function newUlid(): string {
  let id = Date.now().toString(32).toUpperCase().padStart(10, '0').slice(-10)
  while (id.length < ULID_LENGTH) id += CROCKFORD[Math.floor(Math.random() * CROCKFORD.length)]
  return id
}

function track(service: Pick<ServiceDetail, 'id' | 'serviceKey' | 'displayName'>, action: ChangeItem['action']) {
  state.pending = state.pending.filter((c) => c.serviceId !== service.id)
  state.pending.push({ entityType: 'Service', entityKey: service.serviceKey, serviceId: service.id, serviceName: service.displayName, action })
  state.draftToken += 1
}

function toDetail(request: ServiceSaveRequest, existing?: ServiceDetail): ServiceDetail {
  let routeSeq = Math.max(0, ...(existing?.routes ?? []).map((r) => Number(r.routeKey.split('-').pop()) || 0))
  const hostDetails = (hosts: ServiceSaveRequest['hosts']) => (hosts ?? []).map((h) => ({ ...h, id: h.id ?? newId() }))

  return {
    id: existing?.id ?? newId(),
    serviceKey: request.serviceKey,
    displayName: request.displayName,
    tags: request.tags,
    isActive: request.isActive,
    description: request.description,
    routes: request.routes.map((r) => ({
      ...r,
      id: r.id ?? newId(),
      routeKey: existing?.routes.find((x) => x.id === r.id)?.routeKey ?? `${request.serviceKey}-${++routeSeq}`,
    })),
    routingMode: request.routingMode,
    cluster:
      request.routingMode === 'Single'
        ? (existing?.cluster ?? { clusterKey: newUlid(), displayName: request.displayName })
        : null,
    hosts: request.routingMode === 'Single' ? hostDetails(request.hosts) : [],
    experiment:
      request.routingMode === 'AbTest' && request.experiment
        ? {
            ...request.experiment,
            experimentKey: `${request.serviceKey}-ab`,
            stickyCookieName: request.experiment.stickyCookieName ?? `.yes.exp.${request.serviceKey}`,
            variants: request.experiment.variants.map((v) => ({
              variantKey: v.variantKey,
              weightPercent: v.weightPercent,
              isControl: v.isControl,
              cluster: existing?.experiment?.variants.find((x) => x.variantKey === v.variantKey)?.cluster ?? {
                clusterKey: newUlid(),
                displayName: `${request.displayName} · ${v.variantKey}`,
              },
              hosts: hostDetails(v.hosts),
            })),
            effectiveStatus: existing?.experiment?.effectiveStatus ?? 'Draft',
            isAutoRolledBack: false,
          }
        : null,
    updatedAt: new Date().toISOString(),
  }
}

function find(id: number): ServiceDetail {
  const service = state.services.find((s) => s.id === id)
  if (!service) throw new Error('mock: 找不到服務')
  return service
}

function validate(): ValidationIssue[] {
  const issues: ValidationIssue[] = []
  const issue = (code: string, s: ServiceDetail, routeKey: string | null = null, params: Record<string, string> | null = null) =>
    issues.push({ code, serviceId: s.id, serviceName: s.displayName, routeKey, field: null, params })
  const active = state.services.filter((s) => s.isActive)
  const routes = active.flatMap((s) => s.routes.filter((r) => r.isActive).map((r) => ({ s, r })))

  for (const { s, r } of routes) {
    const conflict = routes.find(
      (o) => o.r !== r && o.r.matchPath.toLowerCase() === r.matchPath.toLowerCase() && o.r.order === r.order &&
        (o.r.matchHosts.length === 0 && r.matchHosts.length === 0 || o.r.matchHosts.some((h) => r.matchHosts.includes(h))),
    )
    if (conflict) issue('ROUTE_CONFLICT', s, r.routeKey, { other: conflict.r.routeKey })
  }

  for (const s of active.filter((x) => x.experiment)) {
    const sum = s.experiment!.variants.reduce((total, v) => total + v.weightPercent, 0)
    if (sum !== WEIGHT_TOTAL) issue('EXPERIMENT_WEIGHT_SUM', s, null, { experiment: s.experiment!.experimentKey, sum: String(sum) })
  }

  return issues
}

function summaryOf(service: ServiceDetail): ServiceSummary {
  return {
    id: service.id,
    serviceKey: service.serviceKey,
    displayName: service.displayName,
    tags: service.tags,
    isActive: service.isActive,
    routeCount: service.routes.length,
    hosts: [...new Set(service.routes.flatMap((r) => r.matchHosts))],
    routingMode: service.routingMode,
    hasUnpublishedChanges: state.pending.some((c) => c.serviceId === service.id),
    updatedAt: service.updatedAt,
  }
}

function experimentSummary(service: ServiceDetail): ExperimentSummaryItem {
  const experiment = service.experiment!
  return {
    experimentKey: experiment.experimentKey,
    serviceId: service.id,
    serviceName: service.displayName,
    effectiveStatus: state.runtime[experiment.experimentKey] ?? experiment.effectiveStatus,
    isAutoRolledBack: experiment.isAutoRolledBack,
    triggeredAt: null,
    startAt: experiment.startAt,
    endAt: experiment.endAt,
    variants: experiment.variants.map((v) => ({ variantKey: v.variantKey, percent: v.weightPercent, isControl: v.isControl })),
    hasUnhealthyHost: false,
  }
}

export const serviceStore = {
  draftStatus: () => ({ activeVersion: state.activeVersion, pendingChanges: state.pending.length }),

  search(query: ServiceQuery): PagedResult<ServiceSummary> {
    const keyword = query.keyword?.trim().toLowerCase() ?? ''
    const matched = state.services.filter(
      (s) =>
        (!keyword ||
          s.displayName.toLowerCase().includes(keyword) ||
          s.serviceKey.includes(keyword) ||
          s.routes.some((r) => r.matchPath.toLowerCase().includes(keyword) || r.matchHosts.some((h) => h.includes(keyword)))) &&
        (!query.tag || s.tags.includes(query.tag)),
    )
    const start = (query.pageIndex - 1) * query.pageSize
    return {
      items: matched.slice(start, start + query.pageSize).map(summaryOf),
      pageIndex: query.pageIndex,
      pageSize: query.pageSize,
      totalCount: matched.length,
      totalPages: Math.ceil(matched.length / query.pageSize),
    }
  },

  get: (id: number) => structuredClone(find(id)),

  create(request: ServiceSaveRequest): ServiceDetail {
    if (state.services.some((s) => s.serviceKey === request.serviceKey)) {
      throw new ApiError(MOCK_ERROR_STATUS['SERVICES_KEY_EXISTS'], 'SERVICES_KEY_EXISTS')
    }
    const service = toDetail(request)
    state.services.push(service)
    track(service, 'Added')
    return structuredClone(service)
  },

  update(id: number, request: ServiceSaveRequest): ServiceDetail {
    const service = toDetail(request, find(id))
    state.services = state.services.map((s) => (s.id === id ? service : s))
    track(service, 'Modified')
    return structuredClone(service)
  },

  setActive(id: number, isActive: boolean) {
    const service = find(id)
    service.isActive = isActive
    track(service, 'Modified')
  },

  remove(id: number) {
    const service = find(id)
    state.services = state.services.filter((s) => s.id !== id)
    track(service, 'Removed')
  },

  systemRoutes: (): SystemRoutes => ({ healthCheckPath: '/health', reservedPathPrefixes: ['/health', '/api/v1', '/swagger'], staticRoutes: [] }),

  preview(): PublishPreview {
    const issues = validate()
    return {
      baseVersion: state.activeVersion,
      draftToken: String(state.draftToken),
      changes: structuredClone(state.pending),
      issues,
      canPublish: state.pending.length > 0 && issues.length === 0,
    }
  },

  publish(note: string, draftToken: string): PublishResult {
    if (draftToken !== String(state.draftToken)) {
      throw new ApiError(MOCK_ERROR_STATUS['PUBLISH_DRAFT_CHANGED'], 'PUBLISH_DRAFT_CHANGED')
    }
    if (!note.trim()) throw new ApiError(MOCK_ERROR_STATUS['PUBLISH_NOTE_REQUIRED'], 'PUBLISH_NOTE_REQUIRED')
    const date = new Date().toISOString().slice(0, 10).replaceAll('-', '')
    const versionNo = `${date}${String(++state.versionSeq).padStart(2, '0')}`
    const changeCount = state.pending.length
    state.activeVersion = versionNo
    state.pending = []
    for (const service of state.services) {
      if (service.experiment?.enabled && service.experiment.effectiveStatus === 'Draft') service.experiment.effectiveStatus = 'Running'
    }
    return { versionNo, checksum: 'mock', publishedAt: new Date().toISOString(), changeCount }
  },

  experimentSummary: (): ExperimentSummaryItem[] =>
    state.services
      .filter((s) => s.experiment)
      .map(experimentSummary)
      .filter((e) => ['Scheduled', 'Running', 'Paused'].includes(e.effectiveStatus)),

  experimentAttention: (): AttentionItem[] =>
    state.services
      .filter((s) => s.experiment?.isAutoRolledBack)
      .map((s) => ({
        id: `exp-${s.experiment!.experimentKey}`,
        type: 'experimentAutoRolledBack',
        severity: 'critical',
        params: { service: s.displayName },
        target: `${ROUTES.services}/${s.id}`,
        occurredAt: s.updatedAt,
      })),

  runAction(experimentKey: string, action: ExperimentRuntimeAction): ExperimentSummaryItem {
    const service = state.services.find((s) => s.experiment?.experimentKey === experimentKey)
    if (!service?.experiment) throw new ApiError(MOCK_ERROR_STATUS['EXPERIMENTS_NOT_FOUND'], 'EXPERIMENTS_NOT_FOUND')
    const next: Record<ExperimentRuntimeAction, ExperimentStatus> = { Pause: 'Paused', Resume: 'Running', End: 'Ended' }
    service.experiment.effectiveStatus = next[action]
    service.experiment.isAutoRolledBack = false
    return experimentSummary(service)
  },
}
