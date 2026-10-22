import { Tag } from 'antd'
import { CircleDot, CirclePause, CirclePlay, CircleStop, Clock, Undo2, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { ExperimentStatus } from '../../data/types'

const STATUS_CONFIG: Record<ExperimentStatus, { icon: LucideIcon; color: string }> = {
  Draft: { icon: CircleDot, color: 'default' },
  Scheduled: { icon: Clock, color: 'processing' },
  Running: { icon: CirclePlay, color: 'success' },
  Paused: { icon: CirclePause, color: 'warning' },
  Ended: { icon: CircleStop, color: 'default' },
}

const ICON_SIZE = 14

interface ExperimentStatusBadgeProps {
  status: ExperimentStatus
  /** 因自動回退而暫停時改以紅色「已自動回退」呈現 */
  autoRolledBack?: boolean
}

/** 實驗有效狀態：圖示 + 顏色 + 文字（不只用顏色區分，憲章五） */
export function ExperimentStatusBadge({ status, autoRolledBack }: ExperimentStatusBadgeProps) {
  const { t } = useTranslation()
  const rolledBack = autoRolledBack && status === 'Paused'
  const { icon: Icon, color } = rolledBack ? { icon: Undo2, color: 'error' } : STATUS_CONFIG[status]

  return (
    <Tag color={color} className="inline-flex items-center gap-1" icon={<Icon size={ICON_SIZE} aria-hidden />}>
      {t(rolledBack ? 'experiment.status.AutoRolledBack' : `experiment.status.${status}`)}
    </Tag>
  )
}
