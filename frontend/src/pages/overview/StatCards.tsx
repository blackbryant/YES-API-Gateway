import { Card, Statistic } from 'antd'
import { Boxes, GitCommitHorizontal, RefreshCw, Server, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { MAX_RETAINED_VERSIONS } from '../../constants/app'
import type { OverviewStats } from '../../data/types'
import { SectionTitle } from './SectionTitle'

interface StatCard {
  key: string
  icon: LucideIcon
  title: string
  value: string | number
  hint: string
}

/** 統計卡片（FR-010） */
export function StatCards({ stats }: { stats: OverviewStats }) {
  const { t } = useTranslation()

  const cards: StatCard[] = [
    {
      key: 'services',
      icon: Boxes,
      title: t('overview.stats.services'),
      value: stats.serviceCount,
      hint: t('overview.stats.servicesHint', { count: stats.internalOnlyServiceCount }),
    },
    {
      key: 'hosts',
      icon: Server,
      title: t('overview.stats.hosts'),
      value: `${stats.healthyHostCount} / ${stats.hostCount}`,
      hint: t('overview.stats.hostsHint'),
    },
    {
      key: 'version',
      icon: GitCommitHorizontal,
      title: t('overview.stats.version'),
      value: stats.activeVersion ?? t('overview.stats.none'),
      hint: t('overview.stats.versionHint', { count: stats.retainedVersionCount, max: MAX_RETAINED_VERSIONS }),
    },
    {
      key: 'nodes',
      icon: RefreshCw,
      title: t('overview.stats.nodes'),
      value: `${stats.syncedNodeCount} / ${stats.nodeCount}`,
      hint: t('overview.stats.nodesHint'),
    },
  ]

  return (
    <section>
      <SectionTitle>{t('overview.stats.title')}</SectionTitle>
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {cards.map(({ key, icon: Icon, title, value, hint }) => (
          <Card key={key} data-testid={`stat-${key}`}>
            <Statistic
              title={
                <span className="inline-flex items-center gap-1">
                  <Icon size={14} aria-hidden />
                  {title}
                </span>
              }
              value={value}
              groupSeparator=""
            />
            <div className="mt-1 text-sm text-gray-500">{hint}</div>
          </Card>
        ))}
      </div>
    </section>
  )
}
