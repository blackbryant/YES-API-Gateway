import { Button, Result } from 'antd'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { ROUTES } from '../../constants/routes'

export default function NotFoundPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()

  return (
    <Result
      status="404"
      title={t('notFound.title')}
      subTitle={t('notFound.description')}
      extra={
        <Button type="primary" onClick={() => navigate(ROUTES.overview)}>
          {t('notFound.back')}
        </Button>
      }
    />
  )
}
