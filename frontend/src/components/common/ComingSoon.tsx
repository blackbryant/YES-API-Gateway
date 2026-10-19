import { Card, Result, Tag, Typography } from 'antd'
import { Construction } from 'lucide-react'
import { useTranslation } from 'react-i18next'

interface ComingSoonProps {
  titleKey: string
  descriptionKey: string
}

/** 尚未開發頁面的統一提示（FR-006） */
export function ComingSoon({ titleKey, descriptionKey }: ComingSoonProps) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-6">
      <Typography.Title level={3} className="mb-0">
        {t(titleKey)}
      </Typography.Title>
      <Card>
        <Result
          icon={<Construction size={48} className="mx-auto text-gray-400" aria-hidden />}
          title={<Tag color="processing">{t('comingSoon.badge')}</Tag>}
          subTitle={t(descriptionKey)}
        />
      </Card>
    </div>
  )
}
