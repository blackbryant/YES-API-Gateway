import { Alert, Card, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { SYSTEM_SECTIONS } from './systemSections'

/** 系統頁（預留）：依序列出八個設定區塊，皆尚未開放（FR-022、FR-023） */
export default function SystemPage() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-6">
      <Typography.Title level={3} className="mb-0">
        {t('system.title')}
      </Typography.Title>
      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        {SYSTEM_SECTIONS.map(({ key, icon: Icon, noteKey }) => {
          const headingId = `system-section-${key}`
          return (
            <section key={key} aria-labelledby={headingId}>
              <Card className="h-full">
                <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
                  <Typography.Title level={4} id={headingId} className="m-0 inline-flex items-center gap-2">
                    <Icon size={18} aria-hidden />
                    {t(`system.${key}.title`)}
                  </Typography.Title>
                  <Tag>{t('system.notAvailable')}</Tag>
                </div>
                <Typography.Paragraph type="secondary" className="mb-0">
                  {t(`system.${key}.description`)}
                </Typography.Paragraph>
                {noteKey && <Alert type="info" showIcon className="mt-3" message={t(noteKey)} />}
              </Card>
            </section>
          )
        })}
      </div>
    </div>
  )
}
