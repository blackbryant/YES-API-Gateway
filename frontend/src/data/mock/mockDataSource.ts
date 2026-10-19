import {
  MOCK_DELAY_MS,
  MOCK_ROLE_QUERY_PARAM,
  MOCK_SCENARIOS,
  ROLES,
  STORAGE_KEYS,
  type MockScenario,
} from '../../constants/app'
import type { DataSource } from '../dataSource'
import type { UserSession } from '../types'
import { EMPTY_DRAFT_STATUS, EMPTY_OVERVIEW, NORMAL_DRAFT_STATUS, NORMAL_OVERVIEW } from './fixtures'

/**
 * 目前的範例資料情境。
 * 於瀏覽器主控台執行 localStorage.setItem('yes.mock.scenario', 'empty' | 'error') 後重新整理即可切換。
 */
function currentScenario(): MockScenario {
  const value = localStorage.getItem(STORAGE_KEYS.mockScenario)
  return MOCK_SCENARIOS.find((s) => s === value) ?? 'normal'
}

const delay = () => new Promise((resolve) => setTimeout(resolve, MOCK_DELAY_MS))

/** 模擬網路延遲後回傳對應情境的資料 */
async function respond<T>(normal: T, empty: T): Promise<T> {
  const scenario = currentScenario()
  await delay()
  if (scenario === 'error') throw new Error('mock: 模擬載入失敗')
  return structuredClone(scenario === 'empty' ? empty : normal)
}

/**
 * 假登入：不驗證帳密（規格 FR-018 僅預留流程）。
 * 網址帶 ?role=user 時以唯讀角色登入，其餘為管理員（research R3）。
 */
async function login(username: string): Promise<UserSession> {
  await delay()
  const params = new URLSearchParams(window.location.search)
  return {
    username,
    role: params.get(MOCK_ROLE_QUERY_PARAM) === ROLES.user ? ROLES.user : ROLES.admin,
    loggedInAt: new Date().toISOString(),
  }
}

/** 範例資料實作（僅開發使用，正式建置由 vite.config.ts 阻擋） */
export const mockDataSource: DataSource = {
  getOverview: () => respond(NORMAL_OVERVIEW, EMPTY_OVERVIEW),
  getDraftStatus: () => respond(NORMAL_DRAFT_STATUS, EMPTY_DRAFT_STATUS),
  login,
  logout: () => Promise.resolve(),
  // 工作階段由 authStore 自 localStorage 還原，mock 不另外保存
  getCurrentUser: () => Promise.resolve(null),
}

export default mockDataSource
