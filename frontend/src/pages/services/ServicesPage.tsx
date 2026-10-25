import { Button, Empty, Input, Result, Select, Table, Tag, Typography } from 'antd'
import type { SortOrder } from 'antd/es/table/interface'
import { CircleCheck, CircleMinus, Plus, RotateCw } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { RoleGate } from '../../components/auth/RoleGate'
import { ROLES } from '../../constants/app'
import { ROUTES, serviceDetailPath } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import type { ServiceSortField, ServiceSummary } from '../../data/types'
import { useAsyncData } from '../../hooks/useAsyncData'
import { SystemRoutesSection } from './SystemRoutesSection'

const PAGE_SIZE = 20
const ICON_SIZE = 14
const SORT_DIRECTIONS: SortOrder[] = ['ascend', 'descend']

interface ServiceSort {
  field: ServiceSortField
  desc: boolean
}

const DEFAULT_SORT: ServiceSort = { field: 'Name', desc: false }

/** 服務列表：依名稱、代碼、網域、路徑、標籤篩選，可依名稱、服務代碼、網域排序（FR-011、US5） */
export default function ServicesPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [keyword, setKeyword] = useState('')
  const [tag, setTag] = useState<string | undefined>()
  const [pageIndex, setPageIndex] = useState(1)
  const [sort, setSort] = useState<ServiceSort>(DEFAULT_SORT)

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Typography.Title level={3} className="mb-0">
          {t('services.title')}
        </Typography.Title>
        <RoleGate allow={[ROLES.admin]}>
          <Button type="primary" icon={<Plus size={ICON_SIZE} aria-hidden />} onClick={() => navigate(ROUTES.serviceNew)}>
            {t('services.add')}
          </Button>
        </RoleGate>
      </div>
      <div className="flex flex-wrap gap-2">
        <Input.Search
          className="max-w-md"
          allowClear
          placeholder={t('services.searchPlaceholder')}
          aria-label={t('services.searchPlaceholder')}
          onSearch={(value) => {
            setKeyword(value.trim())
            setPageIndex(1)
          }}
        />
      </div>
      {/* 查詢條件改變時以新的 key 重新載入 */}
      <ServiceTable
        key={`${keyword}|${tag ?? ''}|${pageIndex}|${sort.field}|${sort.desc}`}
        keyword={keyword}
        tag={tag}
        pageIndex={pageIndex}
        sort={sort}
        onTagChange={(value) => {
          setTag(value)
          setPageIndex(1)
        }}
        onPageChange={setPageIndex}
        onSortChange={(value) => {
          setSort(value)
          setPageIndex(1)
        }}
        onOpen={(id) => navigate(serviceDetailPath(id))}
        onAdd={() => navigate(ROUTES.serviceNew)}
      />
      <SystemRoutesSection />
    </div>
  )
}

interface ServiceTableProps {
  keyword: string
  tag: string | undefined
  pageIndex: number
  sort: ServiceSort
  onTagChange: (tag: string | undefined) => void
  onPageChange: (pageIndex: number) => void
  onSortChange: (sort: ServiceSort) => void
  onOpen: (id: number) => void
  onAdd: () => void
}

function ServiceTable({ keyword, tag, pageIndex, sort, onTagChange, onPageChange, onSortChange, onOpen, onAdd }: ServiceTableProps) {
  const { t } = useTranslation()
  const { status, data, reload } = useAsyncData(() =>
    dataSource.searchServices({ keyword, tag, pageIndex, pageSize: PAGE_SIZE, sortBy: sort.field, sortDesc: sort.desc }),
  )
  const tags = useMemo(() => [...new Set((data?.items ?? []).flatMap((s) => s.tags))].sort(), [data])

  // 排序在後端進行（跨頁），欄位標題只反映目前的排序狀態
  const sortable = (field: ServiceSortField) => ({
    key: field,
    sorter: true,
    sortDirections: SORT_DIRECTIONS,
    sortOrder: (sort.field === field ? (sort.desc ? 'descend' : 'ascend') : null) as SortOrder,
  })

  if (status === 'error') {
    return (
      <Result
        status="error"
        title={t('common.loadFailed')}
        extra={
          <Button icon={<RotateCw size={ICON_SIZE} aria-hidden />} onClick={reload}>
            {t('common.retry')}
          </Button>
        }
      />
    )
  }

  const isEmpty = status === 'ready' && data?.totalCount === 0 && !keyword && !tag

  return (
    <div className="flex flex-col gap-2">
      <Select
        className="w-48"
        allowClear
        value={tag}
        placeholder={t('services.tagFilter')}
        aria-label={t('services.tagFilter')}
        options={(tag && !tags.includes(tag) ? [...tags, tag] : tags).map((value) => ({ label: value, value }))}
        onChange={onTagChange}
      />
      {isEmpty ? (
        <Empty
          image={Empty.PRESENTED_IMAGE_SIMPLE}
          description={
            <div className="flex flex-col items-center gap-2">
              <span>{t('services.emptyHint')}</span>
              <RoleGate allow={[ROLES.admin]}>
                <Button type="primary" icon={<Plus size={ICON_SIZE} aria-hidden />} onClick={onAdd}>
                  {t('services.add')}
                </Button>
              </RoleGate>
            </div>
          }
        />
      ) : (
        <Table<ServiceSummary>
          rowKey="id"
          size="middle"
          scroll={{ x: true }}
          loading={status === 'loading'}
          dataSource={data?.items ?? []}
          onRow={(row) => ({ onClick: () => onOpen(row.id), className: 'cursor-pointer' })}
          onChange={(_pagination, _filters, sorter, { action }) => {
            if (action !== 'sort' || Array.isArray(sorter)) return
            onSortChange(sorter.order ? { field: sorter.columnKey as ServiceSortField, desc: sorter.order === 'descend' } : DEFAULT_SORT)
          }}
          pagination={{
            current: pageIndex,
            pageSize: PAGE_SIZE,
            total: data?.totalCount ?? 0,
            showSizeChanger: false,
            onChange: onPageChange,
          }}
          columns={[
            {
              title: t('services.columns.name'),
              ...sortable('Name'),
              render: (_, row) => (
                <div className="flex flex-wrap items-center gap-1">
                  <span className="font-medium">{row.displayName}</span>
                  {row.hasUnpublishedChanges && <Tag color="gold">{t('services.unpublished')}</Tag>}
                </div>
              ),
            },
            { title: t('services.columns.serviceKey'), ...sortable('ServiceKey'), render: (_, row) => <code>{row.serviceKey}</code> },
            { title: t('services.columns.hosts'), ...sortable('Host'), render: (_, row) => (row.hosts.length > 0 ? row.hosts.join(', ') : '—') },
            {
              title: t('services.columns.paths'),
              key: 'paths',
              render: (_, row) =>
                row.paths.length > 0 ? (
                  <div className="flex flex-col gap-0.5">
                    {row.paths.map((path) => (
                      <code key={path} className="break-all">
                        {path}
                      </code>
                    ))}
                  </div>
                ) : (
                  '—'
                ),
            },
            { title: t('services.columns.routeCount'), dataIndex: 'routeCount' },
            { title: t('services.columns.routingMode'), render: (_, row) => t(`services.routingMode.${row.routingMode}`) },
            {
              title: t('services.columns.status'),
              render: (_, row) =>
                row.isActive ? (
                  <Tag color="success" className="inline-flex items-center gap-1" icon={<CircleCheck size={ICON_SIZE} aria-hidden />}>
                    {t('services.status.active')}
                  </Tag>
                ) : (
                  <Tag className="inline-flex items-center gap-1" icon={<CircleMinus size={ICON_SIZE} aria-hidden />}>
                    {t('services.status.inactive')}
                  </Tag>
                ),
            },
          ]}
        />
      )}
    </div>
  )
}
