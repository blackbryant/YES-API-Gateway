import { Alert, Button, Card, Form, Input, Typography } from 'antd'
import { Lock, User } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { APP_ICON_URL, USERNAME_MAX_LENGTH } from '../../constants/app'
import { ROUTES } from '../../constants/routes'
import { useAuthStore } from '../../stores/authStore'

interface LoginForm {
  username: string
  password: string
}

/** 登入頁（預留）：帳密皆填寫才可送出，登入後導回原頁（FR-016、FR-017） */
export default function LoginPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const location = useLocation()
  const session = useAuthStore((state) => state.session)
  const login = useAuthStore((state) => state.login)
  const [form] = Form.useForm<LoginForm>()
  const username = Form.useWatch('username', form)
  const password = Form.useWatch('password', form)
  const [submitting, setSubmitting] = useState(false)
  const [failed, setFailed] = useState(false)

  const from = (location.state as { from?: string } | null)?.from ?? ROUTES.overview

  if (session && !submitting) {
    return <Navigate to={ROUTES.overview} replace />
  }

  const handleSubmit = async (values: LoginForm) => {
    setSubmitting(true)
    setFailed(false)
    try {
      await login(values.username.trim(), values.password)
      navigate(from, { replace: true })
    } catch {
      setFailed(true)
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-full items-center justify-center bg-gray-100 p-4">
      <Card className="w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center gap-2">
          <img src={APP_ICON_URL} alt="" width={56} height={56} aria-hidden />
          <Typography.Title level={3} className="mb-0">
            {t('login.title')}
          </Typography.Title>
          <Typography.Text type="secondary">{t('app.name')}</Typography.Text>
        </div>

        {failed && <Alert type="error" showIcon className="mb-4" message={t('login.failed')} />}

        <Form form={form} layout="vertical" requiredMark={false} onFinish={handleSubmit} disabled={submitting}>
          <Form.Item label={t('login.username')} name="username">
            <Input
              prefix={<User size={16} aria-hidden />}
              maxLength={USERNAME_MAX_LENGTH}
              autoComplete="username"
              autoFocus
            />
          </Form.Item>
          <Form.Item label={t('login.password')} name="password">
            <Input.Password prefix={<Lock size={16} aria-hidden />} autoComplete="current-password" />
          </Form.Item>
          <Button
            type="primary"
            htmlType="submit"
            block
            autoInsertSpace={false}
            loading={submitting}
            disabled={!username?.trim() || !password}
          >
            {t('login.submit')}
          </Button>
        </Form>
      </Card>
    </div>
  )
}
