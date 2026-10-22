import { App, Button, Tag, Tooltip } from 'antd'
import { Send, Upload, Waypoints } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ROLES } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import { useAsyncData } from '../../hooks/useAsyncData'
import { useDraftStore } from '../../stores/draftStore'
import { RoleGate } from '../auth/RoleGate'
import { PublishDialog } from '../publish/PublishDialog'
import { UserMenu } from './UserMenu'

const ICON_SIZE = 16

/** 生效版本、未發佈變更數與常駐的「發佈」按鈕（FR-025） */
function DraftIndicator({ onPublish }: { onPublish: () => void }) {
  const { t } = useTranslation()
  const { status, data } = useAsyncData(() => dataSource.getDraftStatus())

  if (status === 'loading') return null
  if (status === 'error' || !data) {
    return <span className="text-sm text-red-600">{t('topbar.loadFailed')}</span>
  }

  const hasChanges = data.pendingChanges > 0

  return (
    <div className="flex min-w-0 items-center gap-2">
      {data.activeVersion ? (
        <span className="hidden items-center gap-1 text-sm md:inline-flex">
          <span className="text-gray-500">{t('topbar.activeVersion')}</span>
          <Tag className="m-0 font-mono">{data.activeVersion}</Tag>
        </span>
      ) : (
        <span className="hidden text-sm text-gray-500 md:inline">{t('topbar.noActiveVersion')}</span>
      )}
      {hasChanges ? (
        <span className="hidden text-sm text-amber-700 lg:inline">{t('topbar.pendingChanges', { count: data.pendingChanges })}</span>
      ) : (
        <span className="hidden text-sm text-gray-500 lg:inline">{t('topbar.inSync')}</span>
      )}
      <RoleGate allow={[ROLES.admin]}>
        <Tooltip title={hasChanges ? undefined : t('topbar.noChanges')}>
          <Button
            type="primary"
            size="small"
            disabled={!hasChanges}
            icon={<Upload size={ICON_SIZE} aria-hidden />}
            aria-label={t('topbar.publish')}
            onClick={onPublish}
          >
            <span className="hidden sm:inline">{t('topbar.publish')}</span>
          </Button>
        </Tooltip>
      </RoleGate>
    </div>
  )
}

/** 頂部列（全頁常駐） */
export function TopBar() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const notAvailable = () => void message.info(t('common.notAvailable'))
  const revision = useDraftStore((s) => s.revision)
  const bumpDraft = useDraftStore((s) => s.bump)
  const [publishing, setPublishing] = useState(false)

  return (
    <div className="flex h-full items-center justify-between gap-2">
      <Link to={ROUTES.overview} className="flex shrink-0 items-center gap-2 font-semibold text-inherit">
        <Waypoints size={20} aria-hidden />
        <span className="hidden sm:inline">{t('app.name')}</span>
      </Link>
      <div className="flex min-w-0 items-center gap-2">
        {/* 草稿異動或發佈後以新的 key 重新載入狀態 */}
        <DraftIndicator key={revision} onPublish={() => setPublishing(true)} />
        <Button
          size="small"
          icon={<Send size={ICON_SIZE} aria-hidden />}
          aria-label={t('topbar.tryRequest')}
          onClick={notAvailable}
        >
          <span className="hidden sm:inline">{t('topbar.tryRequest')}</span>
        </Button>
        <UserMenu />
      </div>
      {publishing && (
        <PublishDialog
          onClose={() => setPublishing(false)}
          onPublished={() => {
            setPublishing(false)
            bumpDraft()
          }}
        />
      )}
    </div>
  )
}
