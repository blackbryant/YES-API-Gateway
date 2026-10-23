import { render } from '@testing-library/react'
import { App as AntApp } from 'antd'
import type { ReactElement } from 'react'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router'

/** 顯示目前路徑，供測試驗證導頁結果 */
export function LocationDisplay() {
  const location = useLocation()
  return <div data-testid="location">{location.pathname}</div>
}

/** 以 Router 與 Ant Design App 包住元件後渲染 */
export function renderWithProviders(ui: ReactElement, { route = '/' }: { route?: string } = {}) {
  return render(
    <AntApp>
      <MemoryRouter initialEntries={[route]}>
        <Routes>
          <Route path="*" element={<>{ui}<LocationDisplay /></>} />
        </Routes>
      </MemoryRouter>
    </AntApp>,
  )
}
