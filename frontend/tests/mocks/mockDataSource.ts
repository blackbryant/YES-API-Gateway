import { ROLES } from '../../src/constants/app'
import type { DataSource } from '../../src/data/dataSource'
import type { UserSession } from '../../src/data/types'
import { EMPTY_DRAFT_STATUS, EMPTY_OVERVIEW, NORMAL_OVERVIEW } from './fixtures'
import { serviceStore } from './serviceStore'

// 範例資料來源：僅供單元測試與 Playwright 端對端測試（npm run dev:e2e）使用，不會打包進管理介面

/** 模擬的網路延遲毫秒數 */
const MOCK_DELAY_MS = 300

/** 範例資料情境的瀏覽器儲存鍵 */
const MOCK_SCENARIO_STORAGE_KEY = 'yes.mock.scenario'

/** 範例資料情境：正常、空資料、載入失敗 */
const MOCK_SCENARIOS = ['normal', 'empty', 'error'] as const

type MockScenario = (typeof MOCK_SCENARIOS)[number]

/** 登入時指定唯讀角色的網址查詢參數 */
const MOCK_ROLE_QUERY_PARAM = 'role'

/**
 * 目前的範例資料情境。
 * 於瀏覽器主控台執行 localStorage.setItem('yes.mock.scenario', 'empty' | 'error') 後重新整理即可切換。
 */
function currentScenario(): MockScenario {
  const value = localStorage.getItem(MOCK_SCENARIO_STORAGE_KEY)
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

/** 模擬網路延遲後執行動作；error 情境一律失敗 */
async function run<T>(action: () => T): Promise<T> {
  const scenario = currentScenario()
  await delay()
  if (scenario === 'error') throw new Error('mock: 模擬載入失敗')
  return action()
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
  getDraftStatus: () => respond(serviceStore.draftStatus(), EMPTY_DRAFT_STATUS),
  login,
  logout: () => Promise.resolve(),
  // 工作階段由 authStore 自 localStorage 還原，mock 不另外保存
  getCurrentUser: () => Promise.resolve(null),

  // 服務、發佈、A/B 實驗：以記憶體儲存模擬草稿 → 發佈（serviceStore）
  searchServices: (query) => run(() => serviceStore.search(query)),
  getService: (id) => run(() => serviceStore.get(id)),
  createService: (request) => run(() => serviceStore.create(request)),
  updateService: (id, request) => run(() => serviceStore.update(id, request)),
  setServiceActive: (id, isActive) => run(() => serviceStore.setActive(id, isActive)),
  deleteService: (id) => run(() => serviceStore.remove(id)),
  getSystemRoutes: () => run(() => serviceStore.systemRoutes()),
  getPublishPreview: () => run(() => serviceStore.preview()),
  publish: (note, draftToken) => run(() => serviceStore.publish(note, draftToken)),
  getExperimentSummary: () => run(() => serviceStore.experimentSummary()),
  getExperimentAttention: () => run(() => serviceStore.experimentAttention()),
  runExperimentAction: (experimentKey, action) => run(() => serviceStore.runAction(experimentKey, action)),
}

export default mockDataSource
