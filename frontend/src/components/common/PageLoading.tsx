import { Spin } from 'antd'

/** 頁面模組懶載入時的佔位畫面 */
export function PageLoading() {
  return (
    <div className="flex h-full min-h-40 items-center justify-center" role="status">
      <Spin />
    </div>
  )
}
