import { Typography } from 'antd'
import type { ReactNode } from 'react'

/** 總覽頁各區塊標題（h4） */
export function SectionTitle({ children }: { children: ReactNode }) {
  return (
    <Typography.Title level={4} className="mb-3">
      {children}
    </Typography.Title>
  )
}
