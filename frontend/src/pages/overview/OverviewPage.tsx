import { Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { AsyncSection } from '../../components/common/AsyncSection'
import { dataSource } from '../../data/dataSource'
import { useAsyncData } from '../../hooks/useAsyncData'
import { AttentionList } from './AttentionList'
import { ExperimentList } from './ExperimentList'
import { NodeSyncTable } from './NodeSyncTable'
import { StatCards } from './StatCards'

/** 總覽頁：需要處理 → 統計 → 節點同步 → A/B 實驗（FR-008） */
export default function OverviewPage() {
  const { t } = useTranslation()
  const { status, data, reload } = useAsyncData(() => dataSource.getOverview())

  return (
    <div className="flex flex-col gap-6">
      <Typography.Title level={3} className="mb-0">
        {t('overview.title')}
      </Typography.Title>
      <AsyncSection status={status} onRetry={reload}>
        {data && (
          <div className="flex flex-col gap-6">
            <AttentionList items={data.attentionItems} />
            <StatCards stats={data.stats} />
            <NodeSyncTable nodes={data.nodes} />
            <ExperimentList experiments={data.experiments} />
          </div>
        )}
      </AsyncSection>
    </div>
  )
}
