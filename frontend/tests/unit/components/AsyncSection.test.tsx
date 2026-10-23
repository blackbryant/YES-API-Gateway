import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { AsyncSection } from '../../../src/components/common/AsyncSection'

describe('AsyncSection', () => {
  it('loading 時顯示載入中', () => {
    render(<AsyncSection status="loading" onRetry={vi.fn()}>內容</AsyncSection>)

    expect(screen.getByText('載入中…')).toBeInTheDocument()
    expect(screen.queryByText('內容')).not.toBeInTheDocument()
  })

  it('error 時顯示錯誤與重新載入，點擊觸發 onRetry', async () => {
    const onRetry = vi.fn()
    render(<AsyncSection status="error" onRetry={onRetry}>內容</AsyncSection>)

    expect(screen.getByText('資料載入失敗')).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: /重新載入/ }))
    expect(onRetry).toHaveBeenCalledTimes(1)
  })

  it('empty 時顯示指定的空狀態文字', () => {
    render(
      <AsyncSection status="empty" onRetry={vi.fn()} emptyText="目前沒有需要處理的事項">
        內容
      </AsyncSection>,
    )

    expect(screen.getByText('目前沒有需要處理的事項')).toBeInTheDocument()
  })

  it('ready 時顯示子元件', () => {
    render(<AsyncSection status="ready" onRetry={vi.fn()}>內容</AsyncSection>)

    expect(screen.getByText('內容')).toBeInTheDocument()
  })
})
