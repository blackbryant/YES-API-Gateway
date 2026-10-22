import { App, Button, Card, Input, Select, Steps, Typography } from 'antd'
import { Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useNavigate } from 'react-router'
import { ROLES } from '../../constants/app'
import { ROUTES, serviceDetailPath } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import type { HostInput, RouteInput } from '../../data/types'
import { useAuthStore } from '../../stores/authStore'
import { useDraftStore } from '../../stores/draftStore'
import { errorText } from '../../utils/errorText'
import { HostListEditor } from './HostListEditor'
import { RouteFields } from './RouteFields'
import { DISPLAY_NAME_MAX_LENGTH, SERVICE_KEY_PATTERN, emptyHost, emptyRoute, suggestServiceKey } from './serviceForm'

const ICON_SIZE = 14
const STEP_BASIC = 0
const STEP_ENTRIES = 1
const STEP_HOSTS = 2

/** 新增服務精靈：基本資料 → 入口 → 主機（contracts/ui-routes.md） */
export default function ServiceWizardPage() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const role = useAuthStore((s) => s.session?.role)
  const bumpDraft = useDraftStore((s) => s.bump)

  const [step, setStep] = useState(STEP_BASIC)
  const [displayName, setDisplayName] = useState('')
  const [serviceKey, setServiceKey] = useState('')
  const [keyTouched, setKeyTouched] = useState(false)
  const [tags, setTags] = useState<string[]>([])
  const [routes, setRoutes] = useState<RouteInput[]>([emptyRoute()])
  const [hosts, setHosts] = useState<HostInput[]>([emptyHost()])
  const [submitting, setSubmitting] = useState(false)

  if (role !== ROLES.admin) {
    return <Navigate to={ROUTES.services} replace />
  }

  const basicValid = displayName.trim().length > 0 && displayName.length <= DISPLAY_NAME_MAX_LENGTH && SERVICE_KEY_PATTERN.test(serviceKey)
  const entriesValid = routes.length > 0 && routes.every((r) => r.matchPath.startsWith('/'))
  const hostsValid = hosts.length > 0 && hosts.every((h) => h.address.trim().length > 0)
  const canNext = [basicValid, entriesValid, hostsValid][step]

  const submit = async () => {
    setSubmitting(true)
    try {
      const created = await dataSource.createService({
        serviceKey,
        displayName: displayName.trim(),
        tags,
        isActive: true,
        description: null,
        routes,
        routingMode: 'Single',
        hosts: hosts.map((h) => ({ ...h, address: h.address.trim() })),
        experiment: null,
      })
      bumpDraft()
      void message.success(t('services.draftSaved'))
      navigate(serviceDetailPath(created.id))
    } catch (error) {
      void message.error(errorText(t, error))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <Typography.Title level={3} className="mb-0">
        {t('services.wizard.title')}
      </Typography.Title>
      <Steps
        current={step}
        items={[
          { title: t('services.wizard.steps.basic') },
          { title: t('services.wizard.steps.entries') },
          { title: t('services.wizard.steps.hosts') },
        ]}
      />
      <Card>
        {step === STEP_BASIC && (
          <div className="flex max-w-xl flex-col gap-3">
            <label className="flex flex-col gap-1">
              <span>{t('services.fields.displayName')}</span>
              <Input
                value={displayName}
                maxLength={DISPLAY_NAME_MAX_LENGTH}
                onChange={(e) => {
                  setDisplayName(e.target.value)
                  if (!keyTouched) setServiceKey(suggestServiceKey(e.target.value))
                }}
              />
            </label>
            <div className="flex flex-col gap-1">
              <label className="flex flex-col gap-1">
                <span>{t('services.fields.serviceKey')}</span>
                <Input
                  value={serviceKey}
                  status={serviceKey && !SERVICE_KEY_PATTERN.test(serviceKey) ? 'error' : undefined}
                  onChange={(e) => {
                    setKeyTouched(true)
                    setServiceKey(e.target.value.trim())
                  }}
                />
              </label>
              <Typography.Text type="secondary" className="text-xs">
                {t('services.fields.serviceKeyHint')}
              </Typography.Text>
            </div>
            <label className="flex flex-col gap-1">
              <span>{t('services.fields.tags')}</span>
              <Select mode="tags" open={false} value={tags} aria-label={t('services.fields.tags')} onChange={setTags} />
            </label>
          </div>
        )}

        {step === STEP_ENTRIES && (
          <div className="flex flex-col gap-4">
            {routes.map((route, index) => (
              <Card
                key={index}
                size="small"
                title={`${t('services.flow.entry')} ${index + 1}`}
                extra={
                  routes.length > 1 && (
                    <Button
                      type="text"
                      danger
                      aria-label={t('common.delete')}
                      icon={<Trash2 size={ICON_SIZE} aria-hidden />}
                      onClick={() => setRoutes(routes.filter((_, i) => i !== index))}
                    />
                  )
                }
              >
                <RouteFields route={route} onChange={(r) => setRoutes(routes.map((x, i) => (i === index ? r : x)))} />
              </Card>
            ))}
            <Button className="self-start" icon={<Plus size={ICON_SIZE} aria-hidden />} onClick={() => setRoutes([...routes, emptyRoute()])}>
              {t('services.entry.add')}
            </Button>
          </div>
        )}

        {step === STEP_HOSTS && (
          <div className="flex max-w-xl flex-col gap-2">
            <span>{t('services.backend.hosts')}</span>
            <HostListEditor hosts={hosts} onChange={setHosts} />
            <Typography.Text type="secondary" className="text-xs">
              {t('services.backend.receivingHelp')}
            </Typography.Text>
          </div>
        )}
      </Card>

      <div className="flex justify-between gap-2">
        <Button disabled={step === STEP_BASIC} onClick={() => setStep(step - 1)}>
          {t('common.prev')}
        </Button>
        {step < STEP_HOSTS ? (
          <Button type="primary" disabled={!canNext} onClick={() => setStep(step + 1)}>
            {t('common.next')}
          </Button>
        ) : (
          <Button type="primary" disabled={!canNext} loading={submitting} onClick={() => void submit()}>
            {t('services.wizard.submit')}
          </Button>
        )}
      </div>
    </div>
  )
}
