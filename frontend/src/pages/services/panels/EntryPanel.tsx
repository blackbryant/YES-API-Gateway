import { Alert, Button, InputNumber, Modal, Switch, Table, Tag } from 'antd'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import type { RouteInput } from '../../../data/types'
import { RouteFields } from '../RouteFields'
import { MAX_ROUTE_ORDER, MIN_ROUTE_ORDER, emptyRoute, normalizePath } from '../serviceForm'

const ICON_SIZE = 14

/** 入口（含既有入口的路由名稱） */
export type EditableRoute = RouteInput & { routeKey?: string }

interface EntryPanelProps {
  routes: EditableRoute[]
  readOnly: boolean
  onChange: (routes: EditableRoute[]) => void
}

/** 路徑與優先順序相同的入口組合（前端提示，最終以發佈驗證為準） */
function findOverlaps(routes: EditableRoute[]): [string, string][] {
  const pairs: [string, string][] = []
  const label = (r: EditableRoute, i: number) => r.routeKey ?? `#${i + 1}`
  routes.forEach((a, i) =>
    routes.slice(i + 1).forEach((b, k) => {
      if (normalizePath(a.matchPath) === normalizePath(b.matchPath) && a.order === b.order) {
        pairs.push([label(a, i), label(b, i + k + 1)])
      }
    }),
  )
  return pairs
}

/** ① 入口：入口清單、新增 / 編輯 / 刪除、啟用與優先順序 */
export function EntryPanel({ routes, readOnly, onChange }: EntryPanelProps) {
  const { t } = useTranslation()
  const [editing, setEditing] = useState<{ index: number | null; route: EditableRoute } | null>(null)
  const sorted = routes.map((route, index) => ({ route, index })).sort((a, b) => a.route.order - b.route.order)
  const update = (index: number, patch: Partial<EditableRoute>) => onChange(routes.map((r, i) => (i === index ? { ...r, ...patch } : r)))

  const saveEditing = () => {
    if (!editing) return
    onChange(editing.index === null ? [...routes, editing.route] : routes.map((r, i) => (i === editing.index ? editing.route : r)))
    setEditing(null)
  }

  return (
    <div className="flex flex-col gap-3">
      {findOverlaps(routes).map(([a, b]) => (
        <Alert key={`${a}-${b}`} type="warning" showIcon message={t('services.entry.overlapWarning', { a, b })} />
      ))}
      <Table
        size="small"
        rowKey={(row) => row.route.id ?? `new-${row.index}`}
        pagination={false}
        scroll={{ x: true }}
        dataSource={sorted}
        columns={[
          { title: t('services.entry.path'), render: (_, { route }) => <code>{route.matchPath}</code> },
          {
            title: t('services.entry.hosts'),
            render: (_, { route }) => (route.matchHosts.length > 0 ? route.matchHosts.join(', ') : '—'),
          },
          {
            title: t('services.entry.methods'),
            render: (_, { route }) =>
              route.matchMethods.length > 0 ? route.matchMethods.map((m) => <Tag key={m}>{m}</Tag>) : <Tag>{t('services.entry.allMethods')}</Tag>,
          },
          {
            title: t('services.entry.order'),
            render: (_, { route, index }) => (
              <InputNumber
                size="small"
                min={MIN_ROUTE_ORDER}
                max={MAX_ROUTE_ORDER}
                value={route.order}
                disabled={readOnly}
                aria-label={`${t('services.entry.order')} ${route.matchPath}`}
                onChange={(order) => update(index, { order: order ?? MIN_ROUTE_ORDER })}
              />
            ),
          },
          {
            title: t('services.entry.isActive'),
            render: (_, { route, index }) => (
              <Switch
                size="small"
                checked={route.isActive}
                disabled={readOnly}
                aria-label={`${t('services.entry.isActive')} ${route.matchPath}`}
                onChange={(isActive) => update(index, { isActive })}
              />
            ),
          },
          ...(readOnly
            ? []
            : [
                {
                  key: 'actions',
                  render: (_: unknown, { route, index }: { route: EditableRoute; index: number }) => (
                    <div className="flex gap-1">
                      <Button
                        type="text"
                        aria-label={t('common.edit')}
                        icon={<Pencil size={ICON_SIZE} aria-hidden />}
                        onClick={() => setEditing({ index, route })}
                      />
                      <Button
                        type="text"
                        danger
                        aria-label={t('common.delete')}
                        icon={<Trash2 size={ICON_SIZE} aria-hidden />}
                        disabled={routes.length <= 1}
                        onClick={() => onChange(routes.filter((_, i) => i !== index))}
                      />
                    </div>
                  ),
                },
              ]),
        ]}
      />
      {!readOnly && (
        <Button className="self-start" icon={<Plus size={ICON_SIZE} aria-hidden />} onClick={() => setEditing({ index: null, route: emptyRoute() })}>
          {t('services.entry.add')}
        </Button>
      )}
      <Modal
        open={editing !== null}
        title={editing?.index === null ? t('services.entry.add') : t('common.edit')}
        okText={t('common.confirm')}
        cancelText={t('common.cancel')}
        okButtonProps={{ disabled: !editing?.route.matchPath.startsWith('/') }}
        onOk={saveEditing}
        onCancel={() => setEditing(null)}
        destroyOnHidden
      >
        {editing && <RouteFields route={editing.route} showPrefix={false} onChange={(route) => setEditing({ ...editing, route })} />}
      </Modal>
    </div>
  )
}
