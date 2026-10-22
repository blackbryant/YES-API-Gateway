import { Alert, App, Button, Input, List, Modal, Tag, Typography } from 'antd'
import { CircleAlert } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router'
import { ApiError } from '../../api/apiClient'
import { serviceDetailPath } from '../../constants/routes'
import { dataSource } from '../../data/dataSource'
import type { ChangeAction, ChangeItem, PublishPreview, ValidationIssue } from '../../data/types'
import { useAsyncData } from '../../hooks/useAsyncData'
import { errorText } from '../../utils/errorText'
import { AsyncSection } from '../common/AsyncSection'

const NOTE_MAX_LENGTH = 500
const HTTP_CONFLICT = 409
const ICON_SIZE = 14

const ACTION_COLOR: Record<ChangeAction, string> = { Added: 'green', Modified: 'blue', Removed: 'red' }

interface PublishDialogProps {
  onClose: () => void
  onPublished: () => void
}

/** 依服務分組的變更摘要 */
function groupByService(changes: ChangeItem[], otherLabel: string) {
  const groups = new Map<string, ChangeItem[]>()
  for (const change of changes) {
    const key = change.serviceName ?? otherLabel
    groups.set(key, [...(groups.get(key) ?? []), change])
  }
  return [...groups.entries()]
}

/** 發佈對話框：變更摘要 → 驗證錯誤 → 發佈說明（contracts/ui-routes.md、FR-026） */
export function PublishDialog({ onClose, onPublished }: PublishDialogProps) {
  const { t } = useTranslation()
  const { message } = App.useApp()
  const navigate = useNavigate()
  const { status, data, reload } = useAsyncData(() => dataSource.getPublishPreview())
  const [note, setNote] = useState('')
  const [submitting, setSubmitting] = useState(false)
  // 發佈時伺服器回傳的驗證錯誤；只套用在同一份預覽（draftToken 相同），重新載入預覽後以最新結果為準
  const [submitIssues, setSubmitIssues] = useState<{ token: string; issues: ValidationIssue[] } | null>(null)

  const currentIssues = (submitIssues && submitIssues.token === data?.draftToken ? submitIssues.issues : data?.issues) ?? []
  const canSubmit = !!data && data.changes.length > 0 && currentIssues.length === 0 && note.trim().length > 0

  const issueText = (issue: ValidationIssue) => {
    const key = `issues.${issue.code}`
    const params = { routeKey: issue.routeKey ?? '', ...(issue.params ?? {}) }
    const text = t(key, params)
    return text === key ? t('issues.unknown', { code: issue.code }) : text
  }

  const submit = async (preview: PublishPreview) => {
    setSubmitting(true)
    try {
      const result = await dataSource.publish(note.trim(), preview.draftToken)
      void message.success(t('publish.success', { version: result.versionNo }))
      onPublished()
    } catch (error) {
      if (error instanceof ApiError && error.status === HTTP_CONFLICT && error.code === 'PUBLISH_DRAFT_CHANGED') {
        void message.warning(t('publish.draftChanged'))
        reload()
      } else if (error instanceof ApiError && error.code === 'PUBLISH_VALIDATION_FAILED') {
        const details = error.details as { errors?: ValidationIssue[] } | undefined
        setSubmitIssues({ token: preview.draftToken, issues: details?.errors ?? [] })
      } else {
        void message.error(errorText(t, error))
      }
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal
      open
      title={t('publish.title')}
      onCancel={onClose}
      footer={[
        <Button key="cancel" onClick={onClose}>
          {t('common.cancel')}
        </Button>,
        <Button key="submit" type="primary" autoInsertSpace={false} disabled={!canSubmit} loading={submitting} onClick={() => data && void submit(data)}>
          {t('publish.submit')}
        </Button>,
      ]}
    >
      <AsyncSection status={status} onRetry={reload}>
        {data && (
          <div className="flex flex-col gap-4">
            {data.baseVersion && <Typography.Text type="secondary">{t('publish.base', { version: data.baseVersion })}</Typography.Text>}

            {data.changes.length === 0 ? (
              <Alert type="info" showIcon message={t('publish.noChanges')} />
            ) : (
              <section aria-label={t('publish.changes', { count: data.changes.length })}>
                <Typography.Text strong>{t('publish.changes', { count: data.changes.length })}</Typography.Text>
                {groupByService(data.changes, t('publish.otherService')).map(([service, items]) => (
                  <div key={service} className="mt-2">
                    <Typography.Text>{service}</Typography.Text>
                    <ul className="m-0 list-none p-0">
                      {items.map((change) => (
                        <li key={`${change.entityType}-${change.entityKey}`} className="flex flex-wrap items-center gap-2 py-0.5 text-sm">
                          <Tag color={ACTION_COLOR[change.action]}>{t(`publish.action.${change.action}`)}</Tag>
                          <span>{t(`publish.entity.${change.entityType}`)}</span>
                          <code className="break-all">{change.entityKey}</code>
                        </li>
                      ))}
                    </ul>
                  </div>
                ))}
              </section>
            )}

            {currentIssues.length > 0 && (
              <Alert
                type="error"
                showIcon
                message={t('publish.issues', { count: currentIssues.length })}
                description={
                  <List
                    size="small"
                    dataSource={currentIssues}
                    renderItem={(issue) => (
                      <List.Item className="px-0">
                        {issue.serviceId !== null ? (
                          <Button
                            type="link"
                            className="h-auto p-0 text-left whitespace-normal"
                            icon={<CircleAlert size={ICON_SIZE} aria-hidden />}
                            onClick={() => {
                              onClose()
                              navigate(serviceDetailPath(issue.serviceId!))
                            }}
                          >
                            {issue.serviceName && `${issue.serviceName}：`}
                            {issueText(issue)}
                          </Button>
                        ) : (
                          <span>{issueText(issue)}</span>
                        )}
                      </List.Item>
                    )}
                  />
                }
              />
            )}

            <div className="flex flex-col gap-1">
              <span id="publish-note-label">{t('publish.note')}</span>
              <Input.TextArea
                aria-labelledby="publish-note-label"
                value={note}
                maxLength={NOTE_MAX_LENGTH}
                showCount
                rows={3}
                placeholder={t('publish.notePlaceholder')}
                onChange={(e) => setNote(e.target.value)}
              />
            </div>
          </div>
        )}
      </AsyncSection>
    </Modal>
  )
}
