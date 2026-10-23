import { Card, Table } from 'antd'
import { useTranslation } from 'react-i18next'
import { AsyncSection } from '../../components/common/AsyncSection'
import { StatusBadge } from '../../components/common/StatusBadge'
import type { GatewayNode } from '../../data/types'
import { SectionTitle } from './SectionTitle'

/** 閘道節點同步狀態（FR-011） */
export function NodeSyncTable({ nodes }: { nodes: GatewayNode[] }) {
  const { t } = useTranslation()

  return (
    <section>
      <SectionTitle>{t('overview.nodes.title')}</SectionTitle>
      <Card>
        <AsyncSection status={nodes.length ? 'ready' : 'empty'} emptyText={t('overview.nodes.empty')}>
          <Table<GatewayNode>
            rowKey="name"
            dataSource={nodes}
            pagination={false}
            size="small"
            scroll={{ x: 'max-content' }}
            columns={[
              { key: 'name', dataIndex: 'name', title: t('overview.nodes.name') },
              {
                key: 'loadedVersion',
                dataIndex: 'loadedVersion',
                title: t('overview.nodes.loadedVersion'),
                render: (version: string | null) => version ?? t('overview.stats.none'),
              },
              {
                key: 'synced',
                dataIndex: 'synced',
                title: t('overview.nodes.status'),
                render: (synced: boolean) => <StatusBadge status={synced ? 'synced' : 'outOfSync'} />,
              },
            ]}
          />
        </AsyncSection>
      </Card>
    </section>
  )
}
