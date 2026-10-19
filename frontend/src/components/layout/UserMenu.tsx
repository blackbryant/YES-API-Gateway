import { Button, Dropdown, type MenuProps } from 'antd'
import { ChevronDown, CircleUser, Languages, LogOut } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { SUPPORTED_LANGUAGES, type Language } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import { useAuthStore } from '../../stores/authStore'
import { useUiStore } from '../../stores/uiStore'

const LANGUAGE_KEY_PREFIX = 'lang:'
const LOGOUT_KEY = 'logout'
const ICON_SIZE = 16

/** 頂部列使用者選單：帳號、角色、語系切換、登出（FR-002、FR-020、FR-025） */
export function UserMenu() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const session = useAuthStore((state) => state.session)
  const logout = useAuthStore((state) => state.logout)
  const language = useUiStore((state) => state.language)
  const setLanguage = useUiStore((state) => state.setLanguage)

  if (!session) return null

  const items: MenuProps['items'] = [
    {
      key: 'language',
      type: 'group',
      label: (
        <span className="inline-flex items-center gap-1">
          <Languages size={14} aria-hidden />
          {t('user.language')}
        </span>
      ),
      children: SUPPORTED_LANGUAGES.map((lang) => ({
        key: `${LANGUAGE_KEY_PREFIX}${lang}`,
        label: t(`language.${lang}`),
      })),
    },
    { type: 'divider' },
    { key: LOGOUT_KEY, icon: <LogOut size={ICON_SIZE} aria-hidden />, label: t('user.logout') },
  ]

  const handleClick: MenuProps['onClick'] = async ({ key }) => {
    if (key.startsWith(LANGUAGE_KEY_PREFIX)) {
      setLanguage(key.slice(LANGUAGE_KEY_PREFIX.length) as Language)
      return
    }
    if (key === LOGOUT_KEY) {
      await logout()
      navigate(ROUTES.login, { replace: true })
    }
  }

  return (
    <Dropdown
      trigger={['click']}
      menu={{ items, onClick: handleClick, selectable: true, selectedKeys: [`${LANGUAGE_KEY_PREFIX}${language}`] }}
    >
      <Button type="text" size="small" aria-label={t('user.menu')} className="flex min-w-0 items-center gap-1">
        <CircleUser size={ICON_SIZE} className="shrink-0" aria-hidden />
        <span className="max-w-24 truncate md:max-w-40" title={session.username}>
          {session.username}
        </span>
        <span className="hidden shrink-0 text-xs text-gray-500 md:inline">{t(`user.role.${session.role}`)}</span>
        <ChevronDown size={14} className="shrink-0" aria-hidden />
      </Button>
    </Dropdown>
  )
}
