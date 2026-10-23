import { StyleProvider } from '@ant-design/cssinjs'
import { App as AntApp, ConfigProvider, type ThemeConfig } from 'antd'
import enUS from 'antd/locale/en_US'
import zhTW from 'antd/locale/zh_TW'
import { RouterProvider } from 'react-router'
import { router } from './router'
import { useUiStore } from './stores/uiStore'

const ANTD_LOCALES = { 'zh-TW': zhTW, en: enUS } as const

const THEME: ThemeConfig = {
  token: { colorPrimary: '#2563eb', borderRadius: 6 },
  components: {
    Layout: { headerBg: '#ffffff', siderBg: '#ffffff', headerHeight: 56, headerPadding: '0 16px' },
  },
}

export default function App() {
  const language = useUiStore((state) => state.language)

  return (
    // layer：讓 Ant Design 樣式進入 @layer antd，Tailwind utilities 才能覆蓋（research R5）
    <StyleProvider layer>
      {/* 關閉兩個中文字按鈕自動插入空格，維持用語一致 */}
      <ConfigProvider locale={ANTD_LOCALES[language]} theme={THEME} button={{ autoInsertSpace: false }}>
        <AntApp className="h-full">
          <RouterProvider router={router} />
        </AntApp>
      </ConfigProvider>
    </StyleProvider>
  )
}
