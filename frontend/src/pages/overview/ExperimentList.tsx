import { Card, Progress } from 'antd'
import { useTranslation } from 'react-i18next'
import { AsyncSection } from '../../components/common/AsyncSection'
import { StatusBadge } from '../../components/common/StatusBadge'
import type { ExperimentSummary } from '../../data/types'
import { SectionTitle } from './SectionTitle'

/** 進行中的 A/B 實驗（FR-012） */
export function ExperimentList({ experiments }: { experiments: ExperimentSummary[] }) {
  const { t } = useTranslation()

  return (
    <section>
      <SectionTitle>{t('overview.experiments.title')}</SectionTitle>
      <AsyncSection
        status={experiments.length ? 'ready' : 'empty'}
        emptyText={t('overview.experiments.empty')}
      >
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          {experiments.map((exp) => (
            <Card key={exp.id} size="small">
              <div className="mb-2 flex flex-wrap items-start justify-between gap-2">
                <div className="min-w-0">
                  <div className="font-medium break-words">{exp.name}</div>
                  <div className="text-sm text-gray-500">{t('overview.experiments.service', { name: exp.serviceName })}</div>
                </div>
                <StatusBadge
                  status={exp.hasUnhealthyHost ? 'unhealthy' : 'healthy'}
                  label={t(exp.hasUnhealthyHost ? 'overview.experiments.unhealthyHost' : 'overview.experiments.allHealthy')}
                />
              </div>
              {exp.variants.map((variant) => (
                <div key={variant.name} className="flex items-center gap-3">
                  <span className="w-24 shrink-0 text-sm">
                    {t('overview.experiments.variant', { name: variant.name, percent: variant.percent })}
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
