import { App, Button, Tag } from 'antd'
import { Send, Upload, Waypoints } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ROLES } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import { useAsyncData } from '../../hooks/useAsyncData'
import { RoleGate } from '../auth/RoleGate'
import { UserMenu } from './UserMenu'

const ICON_SIZE = 16

/** 生效版本與草稿提示（FR-002） */
function DraftIndicator({ onPublish }: { onPublish: () => void }) {
  const { t } = useTranslation()
  const { status, data } = useAsyncData(() => dataSource.getDraftStatus())

  if (status === 'loading') return null
  if (status === 'error' || !data) {
    return <span className="text-sm text-red-600">{t('topbar.loadFailed')}</span>
  }

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
      {data.pendingChanges > 0 ? (
        <>
          <span className="hidden text-sm text-amber-700 lg:inline">
            {t('topbar.pendingChanges', { count: data.pendingChanges })}
          </span>
          <RoleGate allow={[ROLES.admin]}>
            <Button
              type="primary"
              size="small"
              icon={<Upload size={ICON_SIZE} aria-hidden />}
              aria-label={t('topbar.reviewAndPublish')}
              onClick={onPublish}
            >
              <span className="hidden sm:inline">{t('topbar.reviewAndPublish')}</span>
            </Button>
          </RoleGate>
        </>
      ) : (
        <span className="hidden text-sm text-gray-500 lg:inline">{t('topbar.inSync')}</span>
      )}
    </div>
  )
}

/** 頂部列（全頁常駐） */
export function TopBar() {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const notAvailable = () => void message.info(t('common.notAvailable'))

  return (
    <div className="flex h-full items-center justify-between gap-2">
      <Link to={ROUTES.overview} className="flex shrink-0 items-center gap-2 font-semibold text-inherit">
        <Waypoints size={20} aria-hidden />
        <span className="hidden sm:inline">{t('app.name')}</span>
      </Link>
      <div className="flex min-w-0 items-center gap-2">
        <DraftIndicator onPublish={notAvailable} />
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
    </div>
  )
}
