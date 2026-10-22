import { App, Button, Card, Popconfirm, Switch, Tag, Typography } from 'antd'
import { ArrowDown, Save, Trash2 } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import { AsyncSection } from '../../components/common/AsyncSection'
import { ROLES } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import type { ExperimentRuntimeAction, ServiceDetail, ServiceSaveRequest } from '../../data/types'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useAuthStore } from '../../stores/authStore'
import { useDraftStore } from '../../stores/draftStore'
import { errorText } from '../../utils/errorText'
import { BackendPanel } from './panels/BackendPanel'
import { EntryPanel, type EditableRoute } from './panels/EntryPanel'
import { ProcessingPanel } from './panels/ProcessingPanel'
import { SplitPanel } from './panels/SplitPanel'
import { WEIGHT_TOTAL, toRouteInput, toSaveRequest } from './serviceForm'

const ICON_SIZE = 14

/** 編輯中的服務：存檔請求 + 既有入口的路由名稱 */
type EditableService = Omit<ServiceSaveRequest, 'routes'> & { routes: EditableRoute[] }

function toEditable(service: ServiceDetail): EditableService {
  const request = toSaveRequest(service)
  return { ...request, routes: request.routes.map((r) => ({ ...r, routeKey: service.routes.find((x) => x.id === r.id)?.routeKey })) }
}

function FlowSection({ step, title, children }: { step: number; title: string; children: ReactNode }) {
  return (
    <>
      <Card size="small" title={`${String.fromCodePoint(0x2460 + step)} ${title}`}>
        {children}
      </Card>
      <ArrowDown size={ICON_SIZE} className="mx-auto text-gray-400 last:hidden" aria-hidden />
    </>
  )
}

/** 服務頁：依請求流向「入口 → 防護 → 處理 → 分流 → 後端」編排（憲章五） */
export default function ServiceDetailPage() {
  const { id } = useParams()
  const serviceId = Number(id)
  const { status, data, reload } = useAsyncData(() => dataSource.getService(serviceId))

  return (
    <div className="flex flex-col gap-6">
      <AsyncSection status={status} onRetry={reload}>
        {data && <ServiceEditor key={`${data.id}-${data.updatedAt}`} service={data} onReload={reload} />}
      </AsyncSection>
    </div>
  )
}

/** 以載入的服務為初始值編輯；重新載入後以新的 key 重建（不在 effect 內同步狀態） */
function ServiceEditor({ service: data, onReload: reload }: { service: ServiceDetail; onReload: () => void }) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const serviceId = data.id
  const readOnly = useAuthStore((s) => s.session?.role) !== ROLES.admin
  const bumpDraft = useDraftStore((s) => s.bump)
  const [draft, setDraft] = useState<EditableService>(() => toEditable(data))
  const [saving, setSaving] = useState(false)

  const update = (patch: Partial<EditableService>) => setDraft((current) => ({ ...current, ...patch }))

  const weightsValid = draft.routingMode !== 'AbTest' || draft.experiment?.variants.reduce((s, v) => s + v.weightPercent, 0) === WEIGHT_TOTAL

  const save = async () => {
    setSaving(true)
    try {
      const routes = draft.routes.map(toRouteInput)
      await dataSource.updateService(serviceId, { ...draft, routes })
      bumpDraft()
      void message.success(t('services.draftSaved'))
      reload()
    } catch (error) {
      void message.error(errorText(t, error))
    } finally {
      setSaving(false)
    }
  }

  const remove = async () => {
    try {
      await dataSource.deleteService(serviceId)
      bumpDraft()
      void message.success(t('services.deleted'))
      navigate(ROUTES.services)
    } catch (error) {
      void message.error(errorText(t, error))
    }
  }

  const runAction = async (action: ExperimentRuntimeAction) => {
    if (!data.experiment) return
    try {
      await dataSource.runExperimentAction(data.experiment.experimentKey, action)
      void message.success(t('split.actionDone', { action: t(`split.actions.${action}`) }))
      reload()
    } catch (error) {
      void message.error(errorText(t, error))
    }
  }

  const clusters =
    data.routingMode === 'AbTest'
      ? (data.experiment?.variants ?? []).map((v) => ({ label: t('split.group', { key: v.variantKey }), cluster: v.cluster }))
      : data.cluster
        ? [{ label: '', cluster: data.cluster }]
        : []

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex flex-wrap items-center gap-2">
          <Typography.Title level={3} className="mb-0">
            {data.displayName}
          </Typography.Title>
          <Tag className="font-mono">{data.serviceKey}</Tag>
        </div>
        {!readOnly && (
          <div className="flex flex-wrap items-center gap-2">
            <label className="flex items-center gap-1 text-sm">
              <Switch size="small" checked={draft.isActive} onChange={(isActive) => update({ isActive })} />
              {t('services.fields.isActive')}
            </label>
            <Popconfirm
              title={t('services.deleteConfirm', { name: data.displayName })}
              okText={t('common.delete')}
              cancelText={t('common.cancel')}
              okButtonProps={{ danger: true }}
              onConfirm={() => void remove()}
            >
              <Button danger icon={<Trash2 size={ICON_SIZE} aria-hidden />}>
                {t('common.delete')}
              </Button>
            </Popconfirm>
            <Button
              type="primary"
              icon={<Save size={ICON_SIZE} aria-hidden />}
              loading={saving}
              disabled={!weightsValid}
              onClick={() => void save()}
            >
              {t('common.save')}
            </Button>
          </div>
        )}
      </div>

      <FlowSection step={0} title={t('services.flow.entry')}>
        <EntryPanel routes={draft.routes} readOnly={readOnly} onChange={(routes) => update({ routes })} />
      </FlowSection>
      <FlowSection step={1} title={t('services.flow.protection')}>
        <Tag color="processing">{t('comingSoon.badge')}</Tag>
      </FlowSection>
      <FlowSection step={2} title={t('services.flow.processing')}>
        <ProcessingPanel routes={draft.routes} readOnly={readOnly} onChange={(routes) => update({ routes })} />
      </FlowSection>
      <FlowSection step={3} title={t('services.flow.split')}>
        <SplitPanel
          routingMode={draft.routingMode}
          experiment={draft.experiment}
          hosts={draft.hosts ?? []}
          effectiveStatus={data.experiment?.effectiveStatus}
          isAutoRolledBack={data.experiment?.isAutoRolledBack}
          readOnly={readOnly}
          onChange={(routingMode, experiment, hosts) => update({ routingMode, experiment, hosts })}
          onAction={(action) => void runAction(action)}
        />
      </FlowSection>
      <FlowSection step={4} title={t('services.flow.backend')}>
        <BackendPanel
          routingMode={draft.routingMode}
          hosts={draft.hosts ?? []}
          experiment={draft.experiment}
          clusters={clusters}
          readOnly={readOnly}
          onHostsChange={(hosts) => update({ hosts })}
          onExperimentChange={(experiment) => update({ experiment })}
        />
      </FlowSection>
    </div>
  )
}
