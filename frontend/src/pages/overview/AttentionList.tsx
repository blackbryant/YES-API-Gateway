import { Button, Card, Tag } from 'antd'
import { ArrowRight, CircleAlert, Info, TriangleAlert, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { AsyncSection } from '../../components/common/AsyncSection'
import type { AttentionItem, Severity } from '../../data/types'
import { SectionTitle } from './SectionTitle'
import { sortAttentionItems } from './sortAttentionItems'

const SEVERITY_CONFIG: Record<Severity, { icon: LucideIcon; color: string }> = {
  critical: { icon: CircleAlert, color: 'error' },
  warning: { icon: TriangleAlert, color: 'warning' },
  info: { icon: Info, color: 'processing' },
}

/** 需要處理（FR-009） */
export function AttentionList({ items }: { items: AttentionItem[] }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const title = t('overview.attention.title')

  return (
    <section>
      <SectionTitle>{title}</SectionTitle>
      <Card>
        <AsyncSection status={items.length ? 'ready' : 'empty'} emptyText={t('overview.attention.empty')}>
          <ul aria-label={title} className="divide-y divide-gray-100">
            {sortAttentionItems(items).map((item) => {
              const { icon: Icon, color } = SEVERITY_CONFIG[item.severity]
              return (
                <li key={item.id} className="flex flex-wrap items-center gap-3 py-3 first:pt-0 last:pb-0">
                  <Tag color={color} className="inline-flex items-center gap-1" icon={<Icon size={14} aria-hidden />}>
                    {t(`overview.attention.severity.${item.severity}`)}
                  </Tag>
                  <span className="min-w-0 flex-1 break-words">{t(`overview.attention.${item.type}`, item.params)}</span>
                  <Button
                    size="small"
                    icon={<ArrowRight size={14} aria-hidden />}
                    iconPosition="end"
                    onClick={() => navigate(item.target)}
                  >
                    {t('common.goTo')}
                  </Button>
                </li>
              )
            })}
          </ul>
        </AsyncSection>
      </Card>
    </section>
  )
}
