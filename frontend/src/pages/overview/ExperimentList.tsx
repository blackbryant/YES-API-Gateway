import { Card, Progress } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { AsyncSection } from '../../components/common/AsyncSection'
import { ExperimentStatusBadge } from '../../components/common/ExperimentStatusBadge'
import { StatusBadge } from '../../components/common/StatusBadge'
import { serviceDetailPath } from '../../constants/routes'
import type { ExperimentSummaryItem } from '../../data/types'
import type { AsyncStatus } from '../../hooks/useAsyncData'
import { SectionTitle } from './SectionTitle'

interface ExperimentListProps {
  status: AsyncStatus
  experiments: ExperimentSummaryItem[]
  onRetry: () => void
}

/** 進行中的 A/B 實驗（FR-021）：有效狀態、各組比例、主機健康 */
export function ExperimentList({ status, experiments, onRetry }: ExperimentListProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <section>
      <SectionTitle>{t('overview.experiments.title')}</SectionTitle>
      <AsyncSection
        status={status === 'ready' && experiments.length === 0 ? 'empty' : status}
        onRetry={onRetry}
        emptyText={t('overview.experiments.empty')}
      >
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {experiments.map((exp) => (
            <Card key={exp.experimentKey} size="small" hoverable onClick={() => navigate(serviceDetailPath(exp.serviceId))}>
              <div className="mb-2 flex flex-wrap items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="font-medium break-words">{exp.serviceName}</div>
                  <div className="font-mono text-xs text-gray-500">{exp.experimentKey}</div>
                </div>
                <div className="flex flex-wrap gap-1">
                  <ExperimentStatusBadge status={exp.effectiveStatus} autoRolledBack={exp.isAutoRolledBack} />
                  <StatusBadge
                    status={exp.hasUnhealthyHost ? 'unhealthy' : 'healthy'}
                    label={t(exp.hasUnhealthyHost ? 'overview.experiments.unhealthyHost' : 'overview.experiments.allHealthy')}
                  />
                </div>
              </div>
              {exp.variants.map((variant) => (
                <div key={variant.variantKey} className="flex items-center gap-3">
                  <span className="w-24 shrink-0 text-sm">
                    {t('overview.experiments.variant', { name: variant.variantKey, percent: variant.percent })}
                  </span>
                  <Progress percent={variant.percent} showInfo={false} className="m-0 flex-1" />
                </div>
              ))}
            </Card>
          ))}
        </div>
      </AsyncSection>
    </section>
  )
}
