import { screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../src/api/apiClient'
import { PublishDialog } from '../../../src/components/publish/PublishDialog'
import { dataSource } from '../../../src/data/dataSource'
import type { PublishPreview } from '../../../src/data/types'
import { renderWithProviders } from '../test-utils'

vi.mock('../../../src/data/dataSource', () => ({
  dataSource: { getPublishPreview: vi.fn(), publish: vi.fn() },
}))

const getPublishPreview = vi.mocked(dataSource.getPublishPreview)
const publish = vi.mocked(dataSource.publish)

const PREVIEW: PublishPreview = {
  baseVersion: '2026102101',
  draftToken: '100',
  changes: [
    { entityType: 'Service', entityKey: 'orders-api', serviceId: 1, serviceName: '訂單 API', action: 'Added' },
    { entityType: 'Route', entityKey: 'orders-api-1', serviceId: 1, serviceName: '訂單 API', action: 'Added' },
    { entityType: 'Service', entityKey: 'members-api', serviceId: 2, serviceName: '會員 API', action: 'Modified' },
  ],
  issues: [],
  canPublish: true,
}

function renderDialog() {
  const onClose = vi.fn()
  const onPublished = vi.fn()
  renderWithProviders(<PublishDialog onClose={onClose} onPublished={onPublished} />)
  return { onClose, onPublished }
}

describe('PublishDialog', () => {
  beforeEach(() => {
    getPublishPreview.mockReset()
    publish.mockReset()
  })

  it('依服務分組顯示變更摘要', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    renderDialog()

    expect(await screen.findByText('本次變更（3）')).toBeInTheDocument()
    expect(screen.getByText('訂單 API')).toBeInTheDocument()
    expect(screen.getByText('會員 API')).toBeInTheDocument()
    expect(screen.getAllByText('新增')).toHaveLength(2)
    expect(screen.getByText('修改')).toBeInTheDocument()
  })

  it('有驗證錯誤時停用發佈，點擊錯誤導向服務頁', async () => {
    getPublishPreview.mockResolvedValue({
      ...PREVIEW,
      issues: [
        {
          code: 'EXPERIMENT_WEIGHT_SUM',
          serviceId: 1,
          serviceName: '訂單 API',
          routeKey: null,
          field: 'variants',
          params: { experiment: 'orders-api-ab', sum: '90' },
        },
      ],
      canPublish: false,
    })
    const { onClose } = renderDialog()

    await userEvent.type(await screen.findByLabelText('發佈說明'), '上線')
    expect(screen.getByRole('button', { name: '發佈' })).toBeDisabled()

    await userEvent.click(screen.getByRole('button', { name: /A\/B 實驗各組比例加總為 90/ }))
    expect(onClose).toHaveBeenCalled()
    expect(screen.getByTestId('location')).toHaveTextContent('/services/1')
  })

  it('發佈說明為必填', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    renderDialog()

    await screen.findByText('本次變更（3）')
    expect(screen.getByRole('button', { name: '發佈' })).toBeDisabled()
  })

  it('發佈成功顯示版本號並通知', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    publish.mockResolvedValue({ versionNo: '2026102202', checksum: 'x', publishedAt: '2026-10-22T01:00:00Z', changeCount: 3 })
    const { onPublished } = renderDialog()

    await userEvent.type(await screen.findByLabelText('發佈說明'), '上線訂單 API')
    await userEvent.click(screen.getByRole('button', { name: '發佈' }))

    expect(publish).toHaveBeenCalledWith('上線訂單 API', '100')
    expect(await screen.findByText('已發佈版本 2026102202')).toBeInTheDocument()
    expect(onPublished).toHaveBeenCalled()
  })

  it('草稿已變更時重新載入預覽', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    publish.mockRejectedValue(new ApiError(409, 'PUBLISH_DRAFT_CHANGED'))
    renderDialog()

    await userEvent.type(await screen.findByLabelText('發佈說明'), 'x')
    await userEvent.click(screen.getByRole('button', { name: '發佈' }))

    expect(await screen.findByText('變更內容已更新，請重新確認')).toBeInTheDocument()
    await waitFor(() => expect(getPublishPreview).toHaveBeenCalledTimes(2))
  })

  it('他人發佈中時提示稍後再試', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    publish.mockRejectedValue(new ApiError(409, 'PUBLISH_IN_PROGRESS'))
    const { onPublished } = renderDialog()

    await userEvent.type(await screen.findByLabelText('發佈說明'), 'x')
    await userEvent.click(screen.getByRole('button', { name: '發佈' }))

    expect(await screen.findByText('其他管理者正在發佈，請稍後再試')).toBeInTheDocument()
    expect(onPublished).not.toHaveBeenCalled()
  })

  it('伺服器驗證失敗時列出錯誤', async () => {
    getPublishPreview.mockResolvedValue(PREVIEW)
    publish.mockRejectedValue(
      new ApiError(400, 'PUBLISH_VALIDATION_FAILED', undefined, {
        errors: [{ code: 'ROUTE_CONFLICT', serviceId: 2, serviceName: '會員 API', routeKey: 'members-api-1', field: null, params: { other: 'orders-api-1' } }],
      }),
    )
    renderDialog()

    await userEvent.type(await screen.findByLabelText('發佈說明'), 'x')
    await userEvent.click(screen.getByRole('button', { name: '發佈' }))

    expect(await screen.findByText(/入口 members-api-1 與 orders-api-1/)).toBeInTheDocument()
    // jsdom 不會結束 Ant Design 載入圖示的退場動畫，名稱可能殘留 loading 字樣
    await waitFor(() => expect(screen.getByRole('button', { name: /發佈$/ })).toBeDisabled())
  })
})
