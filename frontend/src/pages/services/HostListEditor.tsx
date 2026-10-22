import { Button, Input, Switch, Tooltip } from 'antd'
import { Plus, Trash2 } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { HostInput } from '../../data/types'
import { emptyHost } from './serviceForm'

const ICON_SIZE = 14

interface HostListEditorProps {
  hosts: HostInput[]
  readOnly?: boolean
  onChange: (hosts: HostInput[]) => void
  /** 供無障礙標籤區分不同組別 */
  label?: string
}

/** 主機位址清單（位址 + 是否接收流量） */
export function HostListEditor({ hosts, readOnly, onChange, label }: HostListEditorProps) {
  const { t } = useTranslation()
  const update = (index: number, patch: Partial<HostInput>) => onChange(hosts.map((h, i) => (i === index ? { ...h, ...patch } : h)))

  return (
    <div className="flex flex-col gap-2">
      {hosts.map((host, index) => (
        <div key={host.id ?? `new-${index}`} className="flex flex-wrap items-center gap-2">
          <Input
            className="min-w-0 flex-1"
            value={host.address}
            placeholder={t('services.backend.addressHint')}
            aria-label={`${label ?? t('services.backend.address')} ${index + 1}`}
            disabled={readOnly}
            onChange={(e) => update(index, { address: e.target.value })}
          />
          <label className="flex items-center gap-1 text-sm">
            <Switch size="small" checked={host.isActive} disabled={readOnly} onChange={(isActive) => update(index, { isActive })} />
            {t('services.backend.receiving')}
          </label>
          {!readOnly && (
            <Tooltip title={t('common.delete')}>
              <Button
                type="text"
                danger
                aria-label={t('common.delete')}
                icon={<Trash2 size={ICON_SIZE} aria-hidden />}
                disabled={hosts.length <= 1}
                onClick={() => onChange(hosts.filter((_, i) => i !== index))}
              />
            </Tooltip>
          )}
        </div>
      ))}
      {!readOnly && (
        <Button className="self-start" icon={<Plus size={ICON_SIZE} aria-hidden />} onClick={() => onChange([...hosts, emptyHost()])}>
          {t('services.backend.addHost')}
        </Button>
      )}
    </div>
  )
}
