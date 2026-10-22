import { Checkbox, Input, InputNumber, Select, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import type { HttpMethod, RouteInput } from '../../data/types'
import { HTTP_METHODS, MAX_ROUTE_ORDER, MIN_ROUTE_ORDER, prefixExample } from './serviceForm'

interface RouteFieldsProps {
  route: RouteInput
  onChange: (route: RouteInput) => void
  /** 是否顯示「移除路徑前綴」（精靈中一併設定） */
  showPrefix?: boolean
}

/** 單一入口的欄位：路徑、網域、方法、優先順序、啟用、移除前綴 */
export function RouteFields({ route, onChange, showPrefix = true }: RouteFieldsProps) {
  const { t } = useTranslation()
  const update = (patch: Partial<RouteInput>) => onChange({ ...route, ...patch })
  const example = route.pathRemovePrefix ? prefixExample(route.matchPath, route.pathRemovePrefix) : null

  return (
    <div className="flex flex-col gap-3">
      <label className="flex flex-col gap-1">
        <span>{t('services.entry.path')}</span>
        <Input
          value={route.matchPath}
          placeholder={t('services.entry.pathHint')}
          onChange={(e) => update({ matchPath: e.target.value })}
        />
        <Typography.Text type="secondary" className="text-xs">
          {t('services.entry.pathHelp')}
        </Typography.Text>
      </label>
      <label className="flex flex-col gap-1">
        <span>{t('services.entry.hosts')}</span>
        <Select
          mode="tags"
          value={route.matchHosts}
          placeholder={t('services.entry.hostsHint')}
          aria-label={t('services.entry.hosts')}
          open={false}
          tokenSeparators={[',', ' ']}
          onChange={(matchHosts: string[]) => update({ matchHosts })}
        />
        <Typography.Text type="secondary" className="text-xs">
          {t('services.entry.hostsHelp')}
        </Typography.Text>
      </label>
      <div className="flex flex-col gap-1">
        <span>{t('services.entry.methods')}</span>
        <Checkbox.Group
          options={HTTP_METHODS.map((m) => ({ label: m, value: m }))}
          value={route.matchMethods}
          onChange={(values) => update({ matchMethods: values as HttpMethod[] })}
        />
        <Typography.Text type="secondary" className="text-xs">
          {t('services.entry.methodsHint')}
        </Typography.Text>
      </div>
      <div className="flex flex-wrap gap-4">
        <div className="flex flex-col gap-1">
          <label className="flex flex-col gap-1">
            <span>{t('services.entry.order')}</span>
            <InputNumber
              min={MIN_ROUTE_ORDER}
              max={MAX_ROUTE_ORDER}
              value={route.order}
              onChange={(order) => update({ order: order ?? MIN_ROUTE_ORDER })}
            />
          </label>
          <Typography.Text type="secondary" className="text-xs">
            {t('services.entry.orderHint')}
          </Typography.Text>
        </div>
        <label className="flex items-center gap-2">
          <Switch checked={route.isActive} onChange={(isActive) => update({ isActive })} />
          {t('services.entry.isActive')}
        </label>
      </div>
      {showPrefix && (
        <div className="flex flex-col gap-1">
          <label className="flex items-center gap-2">
            <Switch
              size="small"
              checked={route.pathRemovePrefix !== null}
              onChange={(on) => update({ pathRemovePrefix: on ? literalPrefix(route.matchPath) : null })}
            />
            {t('services.processing.removePrefix')}
          </label>
          {route.pathRemovePrefix !== null && (
            <>
              <Input
                aria-label={t('services.processing.prefix')}
                value={route.pathRemovePrefix}
                onChange={(e) => update({ pathRemovePrefix: e.target.value })}
              />
              {example && (
                <Typography.Text type="secondary" className="text-xs">
                  {t('services.processing.example', example)}
                </Typography.Text>
              )}
            </>
          )}
        </div>
      )}
    </div>
  )
}

/** 路徑中第一個參數段之前的固定部分（作為移除前綴的預設值） */
function literalPrefix(path: string): string {
  const literal = path.split('/').filter(Boolean).filter((_, i, all) => !all.slice(0, i + 1).some((s) => s.includes('{')))
  return literal.length > 0 ? `/${literal.join('/')}` : ''
}
