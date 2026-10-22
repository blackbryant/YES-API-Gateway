import { Collapse, Typography } from 'antd'
import { useTranslation } from 'react-i18next'
import type { ClusterInfo, ExperimentInput, HostInput, RoutingMode } from '../../../data/types'
import { HostListEditor } from '../HostListEditor'

interface BackendPanelProps {
  routingMode: RoutingMode
  hosts: HostInput[]
  experiment: ExperimentInput | null
  /** 進階資訊：各群組的 ClusterId（ULID） */
  clusters: { label: string; cluster: ClusterInfo }[]
  readOnly: boolean
  onHostsChange: (hosts: HostInput[]) => void
  onExperimentChange: (experiment: ExperimentInput) => void
}

/** ⑤ 後端：主機清單（A/B 時分組）；ClusterId 等 YARP 術語收在進階資訊（憲章五：漸進式揭露） */
export function BackendPanel({ routingMode, hosts, experiment, clusters, readOnly, onHostsChange, onExperimentChange }: BackendPanelProps) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-col gap-4">
      {routingMode === 'Single' || !experiment ? (
        <HostListEditor hosts={hosts} readOnly={readOnly} onChange={onHostsChange} />
      ) : (
        experiment.variants.map((variant, index) => (
          <div key={variant.variantKey} className="flex flex-col gap-2">
            <Typography.Text strong>{t('services.backend.groupHosts', { group: variant.variantKey })}</Typography.Text>
            <HostListEditor
              hosts={variant.hosts}
              readOnly={readOnly}
              label={t('services.backend.groupHosts', { group: variant.variantKey })}
              onChange={(variantHosts) =>
                onExperimentChange({
                  ...experiment,
                  variants: experiment.variants.map((v, i) => (i === index ? { ...v, hosts: variantHosts } : v)),
                })
              }
            />
          </div>
        ))
      )}
      {!readOnly && (
        <Typography.Text type="secondary" className="text-xs">
          {t('services.backend.receivingHelp')}
        </Typography.Text>
      )}
      {clusters.length > 0 && (
        <Collapse
          size="small"
          items={[
            {
              key: 'advanced',
              label: t('common.advanced'),
              children: (
                <dl className="m-0 flex flex-col gap-1">
                  {clusters.map(({ label, cluster }) => (
                    <div key={cluster.clusterKey} className="flex flex-wrap gap-2">
                      <dt className="text-gray-500">
                        {t('services.backend.clusterId')}
                        {label && ` · ${label}`}
                      </dt>
                      <dd className="m-0 font-mono">{cluster.clusterKey}</dd>
                    </div>
                  ))}
                </dl>
              ),
            },
          ]}
        />
      )}
    </div>
  )
}
