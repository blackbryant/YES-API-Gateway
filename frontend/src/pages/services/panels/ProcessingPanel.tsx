import { Input, Switch, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { prefixExample } from '../serviceForm'
import type { EditableRoute } from './EntryPanel'

interface ProcessingPanelProps {
  routes: EditableRoute[]
  readOnly: boolean
  onChange: (routes: EditableRoute[]) => void
}

/** ③ 處理：本功能只提供「轉送時移除路徑前綴」（附改寫前後範例） */
export function ProcessingPanel({ routes, readOnly, onChange }: ProcessingPanelProps) {
  const { t } = useTranslation()
  const update = (index: number, pathRemovePrefix: string | null) =>
    onChange(routes.map((r, i) => (i === index ? { ...r, pathRemovePrefix } : r)))

  return (
    <div className="flex flex-col gap-4">
      {routes.map((route, index) => {
        const example = route.pathRemovePrefix ? prefixExample(route.matchPath, route.pathRemovePrefix) : null
        return (
          <div key={route.id ?? `new-${index}`} className="flex flex-col gap-1">
            <code>{route.matchPath}</code>
            <label className="flex items-center gap-2">
              <Switch
                size="small"
                checked={route.pathRemovePrefix !== null}
                disabled={readOnly}
                aria-label={`${t('services.processing.removePrefix')} ${route.matchPath}`}
                onChange={(on) => update(index, on ? '' : null)}
              />
              {t('services.processing.removePrefix')}
            </label>
            {route.pathRemovePrefix !== null && (
              <Input
                aria-label={`${t('services.processing.prefix')} ${route.matchPath}`}
                value={route.pathRemovePrefix}
                disabled={readOnly}
                onChange={(e) => update(index, e.target.value)}
              />
            )}
            {example && (
              <Typography.Text type="secondary" className="text-xs">
                {t('services.processing.example', example)}
              </Typography.Text>
            )}
          </div>
        )
      })}
    </div>
  )
}
