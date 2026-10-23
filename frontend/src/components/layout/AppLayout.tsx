import { Grid, Layout } from 'antd'
import { Suspense } from 'react'
import { Outlet } from 'react-router'
import { PageLoading } from '../common/PageLoading'
import { SideMenu } from './SideMenu'
import { TopBar } from './TopBar'

const SIDER_WIDTH = 200

/**
 * 主畫面佈局：頂部列 + 左側選單 + 主工作區（FR-001）。
 * 寬度小於 md 斷點時隱藏左側選單，改以水平選單堆疊於內容上方（FR-005、research R6）。
 */
export function AppLayout() {
  const screens = Grid.useBreakpoint()
  // 首次渲染尚未取得斷點時視為桌機
  const isDesktop = screens.md ?? true

  return (
    <Layout className="min-h-full">
      <Layout.Header className="sticky top-0 z-10 border-b border-gray-200">
        <TopBar />
      </Layout.Header>
      {!isDesktop && (
        <div className="overflow-x-auto border-b border-gray-200 bg-white">
          <SideMenu mode="horizontal" />
        </div>
      )}
      <Layout>
        {isDesktop && (
          <Layout.Sider width={SIDER_WIDTH} className="border-e border-gray-200">
            <SideMenu />
          </Layout.Sider>
        )}
        <Layout.Content className="min-w-0 p-4 md:p-6">
          <Suspense fallback={<PageLoading />}>
            <Outlet />
          </Suspense>
        </Layout.Content>
      </Layout>
    </Layout>
  )
}
