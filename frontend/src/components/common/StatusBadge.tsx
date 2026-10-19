import { Tag } from 'antd'
import { CircleCheck, CircleDashed, CircleMinus, CircleX, RefreshCw, RefreshCwOff, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export type Status = 'healthy' | 'unhealthy' | 'disabled' | 'unchecked' | 'synced' | 'outOfSync'

const STATUS_CONFIG: Record<Status, { icon: LucideIcon; color: string }> = {
  healthy: { icon: CircleCheck, color: 'success' },
  unhealthy: { icon: CircleX, color: 'error' },
  disabled: { icon: CircleMinus, color: 'default' },
  unchecked: { icon: CircleDashed, color: 'default' },
  synced: { icon: RefreshCw, color: 'success' },
  outOfSync: { icon: RefreshCwOff, color: 'warning' },
}

const ICON_SIZE = 14

interface StatusBadgeProps {
  status: Status
  /** 覆寫預設文字；未提供時使用 status.* 翻譯 */
  label?: string
}

/** 狀態標示：一律同時呈現圖示、顏色與文字，不只用顏色區分（FR-013） */
export function StatusBadge({ status, label }: StatusBadgeProps) {
  const { t } = useTranslation()
  const { icon: Icon, color } = STATUS_CONFIG[status]

  return (
    <Tag color={color} className="inline-flex items-center gap-1" icon={<Icon size={ICON_SIZE} aria-hidden />}>
      {label ?? t(`status.${status}`)}
    </Tag>
  )
}
