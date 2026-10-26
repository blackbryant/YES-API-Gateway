import { Alert, Button, Input, InputNumber, Popconfirm, Radio, Slider, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { ExperimentStatusBadge } from '../../../components/common/ExperimentStatusBadge'
import type { ExperimentInput, ExperimentRuntimeAction, ExperimentStatus, HostInput, RoutingMode } from '../../../data/types'
import { DEFAULT_FORCE_HEADER, MAX_STICKY_DAYS, WEIGHT_TOTAL, defaultExperiment } from '../serviceForm'

const PERCENT_MAX = 100

/** 各狀態可執行的手動操作 */
const ACTIONS_BY_STATUS: Record<ExperimentStatus, ExperimentRuntimeAction[]> = {
  Draft: [],
  Scheduled: ['Pause', 'End'],
  Running: ['Pause', 'End'],
  Paused: ['Resume', 'End'],
  Ended: [],
}

interface SplitPanelProps {
  routingMode: RoutingMode
  experiment: ExperimentInput | null
  /** 單一模式的主機（切換為 A/B 時成為 A 組） */
  hosts: HostInput[]
  /** 已發佈實驗的有效狀態；未發佈時為 undefined */
  effectiveStatus?: ExperimentStatus
  isAutoRolledBack?: boolean
  readOnly: boolean
  onChange: (routingMode: RoutingMode, experiment: ExperimentInput | null, hosts: HostInput[]) => void
  onAction?: (action: ExperimentRuntimeAction) => void
}

/** ④ 分流：不分流 / A/B 測試（比例、黏著、強制分組、手動操作）；排程與自動回退暫不開放設定 */
export function SplitPanel({ routingMode, experiment, hosts, effectiveStatus, isAutoRolledBack, readOnly, onChange, onAction }: SplitPanelProps) {
  const { t } = useTranslation()

  const switchMode = (mode: RoutingMode) => {
    if (mode === 'AbTest') {
      onChange('AbTest', experiment ?? defaultExperiment(hosts), hosts)
    } else {
      const control = experiment?.variants.find((v) => v.isControl)
      onChange('Single', null, control?.hosts ?? hosts)
    }
  }

  const update = (patch: Partial<ExperimentInput>) => experiment && onChange('AbTest', { ...experiment, ...patch }, hosts)

  /** 兩組時自動補足為 100；三組以上各自調整並檢查加總 */
  const setWeight = (index: number, weight: number) => {
    if (!experiment) return
    const variants = experiment.variants.map((v, i) => {
      if (i === index) return { ...v, weightPercent: weight }
      if (experiment.variants.length === 2) return { ...v, weightPercent: WEIGHT_TOTAL - weight }
      return v
    })
    update({ variants })
  }

  const sum = experiment?.variants.reduce((total, v) => total + v.weightPercent, 0) ?? WEIGHT_TOTAL
  const forceHeader = experiment?.forceRules.find((r) => r.matchType === 'Header')?.matchName ?? ''

  return (
    <div className="flex flex-col gap-4">
      <Radio.Group
        value={routingMode}
        disabled={readOnly}
        aria-label={t('split.mode')}
        options={[
          { label: t('services.routingMode.Single'), value: 'Single' },
          { label: t('services.routingMode.AbTest'), value: 'AbTest' },
        ]}
        onChange={(e) => switchMode(e.target.value as RoutingMode)}
      />

      {routingMode === 'AbTest' && experiment && (
        <>
          <div className="flex flex-wrap items-center gap-2">
            {effectiveStatus && <ExperimentStatusBadge status={effectiveStatus} autoRolledBack={isAutoRolledBack} />}
            {!readOnly &&
              effectiveStatus &&
              ACTIONS_BY_STATUS[effectiveStatus].map((action) =>
                action === 'End' ? (
                  <Popconfirm key={action} title={t('split.endConfirm')} okText={t('common.confirm')} cancelText={t('common.cancel')} onConfirm={() => onAction?.(action)}>
                    <Button size="small" danger>
                      {t(`split.actions.${action}`)}
                    </Button>
                  </Popconfirm>
                ) : (
                  <Button key={action} size="small" onClick={() => onAction?.(action)}>
                    {t(`split.actions.${action}`)}
                  </Button>
                ),
              )}
          </div>

          <label className="flex items-center gap-2">
            <Switch checked={experiment.enabled} disabled={readOnly} onChange={(enabled) => update({ enabled })} />
            {t('split.enabled')}
          </label>

          <div className="flex flex-col gap-2">
            <Typography.Text strong>{t('split.variants')}</Typography.Text>
            {experiment.variants.map((variant, index) => (
              <div key={variant.variantKey} className="flex flex-wrap items-center gap-3">
                <span className="w-24 shrink-0">
                  {t('split.group', { key: variant.variantKey })}
                  {variant.isControl && <span className="ml-1 text-xs text-gray-500">（{t('split.control')}）</span>}
                </span>
                <Slider
                  className="min-w-40 flex-1"
                  min={0}
                  max={PERCENT_MAX}
                  value={variant.weightPercent}
                  disabled={readOnly}
                  aria-label={t('split.group', { key: variant.variantKey })}
                  onChange={(value: number) => setWeight(index, value)}
                />
                <InputNumber
                  min={0}
                  max={PERCENT_MAX}
                  value={variant.weightPercent}
                  disabled={readOnly}
                  addonAfter="%"
                  aria-label={`${t('split.group', { key: variant.variantKey })} %`}
                  onChange={(value) => setWeight(index, value ?? 0)}
                />
              </div>
            ))}
            {sum !== WEIGHT_TOTAL && <Alert type="error" showIcon message={t('split.weightSumError', { sum })} />}
          </div>

          <div className="flex flex-wrap gap-4">
            <div className="flex flex-col gap-1">
              <label className="flex flex-col gap-1">
                <span>{t('split.stickyDays')}</span>
                <InputNumber
                  min={0}
                  max={MAX_STICKY_DAYS}
                  value={experiment.stickyDays}
                  disabled={readOnly}
                  onChange={(stickyDays) => update({ stickyDays: stickyDays ?? 0 })}
                />
              </label>
              <Typography.Text type="secondary" className="text-xs">
                {t('split.stickyHint')}
              </Typography.Text>
            </div>
            <label className="flex min-w-60 flex-1 flex-col gap-1">
              <span>{t('split.forceHeader')}</span>
              <Input
                value={forceHeader}
                placeholder={DEFAULT_FORCE_HEADER}
                disabled={readOnly}
                onChange={(e) =>
                  update({
                    forceRules: e.target.value.trim()
                      ? [{ matchType: 'Header', matchName: e.target.value.trim(), matchValue: null, variantKey: null }]
                      : [],
                  })
                }
              />
              <Typography.Text type="secondary" className="text-xs">
                {t('split.forceHeaderHint')}
              </Typography.Text>
            </label>
          </div>

        </>
      )}
    </div>
  )
}
