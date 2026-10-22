import { Collapse, Tag, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import { AsyncSection } from '../../components/common/AsyncSection'
import { dataSource } from '../../data/dataSource'
import { useAsyncData } from '../../hooks/useAsyncData'

/** 系統路由（唯讀）：健康檢查、保留路徑與靜態代理路由，由閘道設定檔定義（US3 AS4） */
export function SystemRoutesSection() {
  const { t } = useTranslation()
  const { status, data, reload } = useAsyncData(() => dataSource.getSystemRoutes())

  return (
    <Collapse
      size="small"
      items={[
        {
          key: 'system-routes',
          label: t('services.systemRoutes.title'),
          children: (
            <AsyncSection status={status} onRetry={reload}>
              {data && (
                <div className="flex flex-col gap-2">
                  <Typography.Text type="secondary">{t('services.systemRoutes.description')}</Typography.Text>
                  <dl className="m-0 flex flex-col gap-2">
                    <div className="flex flex-wrap gap-2">
                      <dt className="text-gray-500">{t('services.systemRoutes.healthCheck')}</dt>
                      <dd className="m-0 font-mono">{data.healthCheckPath}</dd>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <dt className="text-gray-500">{t('services.systemRoutes.reserved')}</dt>
                      <dd className="m-0 flex flex-wrap gap-1">
                        {data.reservedPathPrefixes.map((prefix) => (
                          <Tag key={prefix} className="font-mono">
                            {prefix}
                          </Tag>
                        ))}
                      </dd>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <dt className="text-gray-500">{t('services.systemRoutes.static')}</dt>
                      <dd className="m-0">
                        {data.staticRoutes.length === 0
                          ? t('services.systemRoutes.none')
                          : data.staticRoutes.map((r) => (
                              <div key={r.routeId} className="font-mono">
                                {r.routeId}：{r.path} → {r.clusterId}
                              </div>
                            ))}
                      </dd>
                    </div>
                  </dl>
                </div>
              )}
            </AsyncSection>
          ),
        },
      ]}
    />
  )
}
