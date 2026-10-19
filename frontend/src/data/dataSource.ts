// 實際實作由 vite.config.ts 的別名依 VITE_DATA_SOURCE 決定（mock 或 http）
import implementation from '~data-source-impl'
import type { DraftStatus, Overview, UserSession } from './types'

/** 畫面唯一依賴的資料來源介面；mock 與 http 實作可互換（FR-014、research R2） */
export interface DataSource {
  getOverview(): Promise<Overview>
  getDraftStatus(): Promise<DraftStatus>
  login(username: string, password: string): Promise<UserSession>
  logout(): Promise<void>
  getCurrentUser(): Promise<UserSession | null>
}

export const dataSource: DataSource = implementation
