// 實際實作由 vite.config.ts 的別名決定：管理介面一律串接後端 Admin API，只有測試改用 tests/mocks 範例資料
import implementation from '~data-source-impl'
import type {
  AttentionItem,
  DraftStatus,
  ExperimentRuntimeAction,
  ExperimentSummaryItem,
  Overview,
  PagedResult,
  PublishPreview,
  PublishResult,
  ServiceDetail,
  ServiceQuery,
  ServiceSaveRequest,
  ServiceSummary,
  SystemRoutes,
  UserSession,
} from './types'

/** 畫面唯一依賴的資料來源介面；mock 與 http 實作可互換（FR-014、research R2） */
export interface DataSource {
  getOverview(): Promise<Overview>
  getDraftStatus(): Promise<DraftStatus>
  login(username: string, password: string): Promise<UserSession>
  logout(): Promise<void>
  getCurrentUser(): Promise<UserSession | null>

  // 001-02 服務
  searchServices(query: ServiceQuery): Promise<PagedResult<ServiceSummary>>
  getService(id: number): Promise<ServiceDetail>
  createService(request: ServiceSaveRequest): Promise<ServiceDetail>
  updateService(id: number, request: ServiceSaveRequest): Promise<ServiceDetail>
  setServiceActive(id: number, isActive: boolean): Promise<void>
  deleteService(id: number): Promise<void>
  getSystemRoutes(): Promise<SystemRoutes>

  // 001-02 發佈
  getPublishPreview(): Promise<PublishPreview>
  publish(note: string, draftToken: string): Promise<PublishResult>

  // 001-02 A/B 實驗
  getExperimentSummary(): Promise<ExperimentSummaryItem[]>
  getExperimentAttention(): Promise<AttentionItem[]>
  runExperimentAction(experimentKey: string, action: ExperimentRuntimeAction): Promise<ExperimentSummaryItem>
}

export const dataSource: DataSource = implementation
