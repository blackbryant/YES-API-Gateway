import { Button, Empty, Result, Spin } from 'antd'
import { RotateCw } from 'lucide-react'
import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import type { AsyncStatus } from '../../hooks/useAsyncData'

interface AsyncSectionProps {
  status: AsyncStatus
  /** 錯誤時「重新載入」的動作；未提供則不顯示按鈕 */
  onRetry?: () => void
  /** 空資料時的說明文字；未提供時使用 common.empty */
  emptyText?: string
  children: ReactNode
}

/** 統一處理載入中、錯誤、空資料三種狀態（FR-015） */
export function AsyncSection({ status, onRetry, emptyText, children }: AsyncSectionProps) {
  const { t } = useTranslation()

  switch (status) {
    case 'loading':
      return (
        <div className="flex items-center justify-center gap-2 py-8" role="status">
          <Spin />
          <span>{t('common.loading')}</span>
        </div>
      )
    case 'error':
      return (
        <Result
          status="error"
          title={t('common.loadFailed')}
          extra={
            onRetry && (
              <Button icon={<RotateCw size={14} aria-hidden />} onClick={onRetry}>
                {t('common.retry')}
              </Button>
            )
          }
        />
      )
    case 'empty':
      return <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description={emptyText ?? t('common.empty')} />
    default:
      return <>{children}</>
  }
}
