import { ApiError, apiClient } from '../../api/apiClient'
import type { DataSource } from '../dataSource'
import type {
  AttentionItem,
  DraftStatus,
  ExperimentSummaryItem,
  Overview,
  PublishPreview,
  PublishResult,
  ServiceDetail,
  ServiceSummary,
  SystemRoutes,
  UserSession,
} from '../types'

const HTTP_UNAUTHORIZED = 401

function toQueryString(params: Record<string, string | number | boolean | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') search.set(key, String(value))
  }
  return search.toString()
}

/** 呼叫 Admin API 的實作（依 contracts/admin-api.yaml） */
export const httpDataSource: DataSource = {
  getOverview: () => apiClient.get<Overview>('/overview'),
  getDraftStatus: () => apiClient.get<DraftStatus>('/draft/status'),
  login: (username, password) => apiClient.post<UserSession>('/auth/login', { username, password }),
  logout: () => apiClient.post<void>('/auth/logout'),
  getCurrentUser: async () => {
    try {
      return await apiClient.get<UserSession>('/auth/me')
    } catch (error) {
      if (error instanceof ApiError && error.status === HTTP_UNAUTHORIZED) return null
      throw error
    }
  },

  searchServices: (query) =>
    apiClient.getPaged<ServiceSummary>(
      `/services?${toQueryString({
        keyword: query.keyword,
        tag: query.tag,
        pageIndex: query.pageIndex,
        pageSize: query.pageSize,
        sortBy: query.sortBy,
        sortDesc: query.sortDesc,
      })}`,
    ),
  getService: (id) => apiClient.get<ServiceDetail>(`/services/${id}`),
  createService: (request) => apiClient.post<ServiceDetail>('/services', request),
  updateService: (id, request) => apiClient.put<ServiceDetail>(`/services/${id}`, request),
  setServiceActive: (id, isActive) => apiClient.put<void>(`/services/${id}/active`, { isActive }),
  deleteService: (id) => apiClient.delete(`/services/${id}`),
  getSystemRoutes: () => apiClient.get<SystemRoutes>('/system-routes'),

  getPublishPreview: () => apiClient.get<PublishPreview>('/publish/preview'),
  publish: (note, draftToken) => apiClient.post<PublishResult>('/publish', { note, draftToken }),

  getExperimentSummary: () => apiClient.get<ExperimentSummaryItem[]>('/experiments/summary'),
  getExperimentAttention: () => apiClient.get<AttentionItem[]>('/experiments/attention'),
  runExperimentAction: (experimentKey, action) =>
    apiClient.post<ExperimentSummaryItem>(`/experiments/${encodeURIComponent(experimentKey)}/runtime-actions`, { action }),
}

export default httpDataSource
