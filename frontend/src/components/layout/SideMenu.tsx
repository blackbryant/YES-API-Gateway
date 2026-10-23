import { Menu } from 'antd'
import { Boxes, History, LayoutDashboard, LayoutTemplate, Server, Settings, type LucideIcon } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useLocation, useNavigate } from 'react-router'
import { ROUTES } from '../../constants/routes'

const MENU_ITEMS: { key: string; path: string; icon: LucideIcon }[] = [
  { key: 'overview', path: ROUTES.overview, icon: LayoutDashboard },
  { key: 'services', path: ROUTES.services, icon: Boxes },
  { key: 'hosts', path: ROUTES.hosts, icon: Server },
  { key: 'templates', path: ROUTES.templates, icon: LayoutTemplate },
  { key: 'releases', path: ROUTES.releases, icon: History },
  { key: 'system', path: ROUTES.system, icon: Settings },
]

const ICON_SIZE = 16

/** 依目前路徑判定選中的選單；總覽只在完全相符時選中，其他依路徑前綴 */
export function getSelectedMenuKey(pathname: string): string | undefined {
  return MENU_ITEMS.find(({ path }) =>
    path === ROUTES.overview ? pathname === path : pathname === path || pathname.startsWith(`${path}/`),
  )?.key
}

interface SideMenuProps {
  mode?: 'inline' | 'horizontal'
}

/** 左側選單（FR-003）；窄螢幕時以水平模式堆疊於內容上方（FR-005） */
export function SideMenu({ mode = 'inline' }: SideMenuProps) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const selectedKey = getSelectedMenuKey(pathname)

  return (
    <Menu
      mode={mode}
      aria-label={t('layout.menuLabel')}
      selectedKeys={selectedKey ? [selectedKey] : []}
      // 水平模式不收合成「…」，改由外層容器水平捲動，讓所有項目都看得到
      disabledOverflow={mode === 'horizontal'}
      className={mode === 'horizontal' ? 'min-w-max border-b-0' : 'h-full border-e-0'}
      items={MENU_ITEMS.map(({ key, path, icon: Icon }) => ({
        key,
        icon: <Icon size={ICON_SIZE} aria-hidden />,
        label: t(`layout.menu.${key}`),
        onClick: () => navigate(path),
      }))}
    />
  )
}
