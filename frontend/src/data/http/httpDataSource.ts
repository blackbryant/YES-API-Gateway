import { ApiError, apiClient } from '../../api/apiClient'
import type { DataSource } from '../dataSource'
import type { DraftStatus, Overview, UserSession } from '../types'

const HTTP_UNAUTHORIZED = 401

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
}

export default httpDataSource
