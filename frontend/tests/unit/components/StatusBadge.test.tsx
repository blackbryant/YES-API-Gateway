import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { StatusBadge, type Status } from '../../../src/components/common/StatusBadge'

const CASES: [Status, string][] = [
  ['healthy', '正常'],
  ['unhealthy', '異常'],
  ['disabled', '停用'],
  ['unchecked', '未檢查'],
  ['synced', '已同步'],
  ['outOfSync', '未同步'],
]

describe('StatusBadge', () => {
  it.each(CASES)('狀態 %s 同時顯示圖示與文字「%s」', (status, text) => {
    const { container } = render(<StatusBadge status={status} />)

    expect(screen.getByText(text)).toBeInTheDocument()
    expect(container.querySelector('svg')).not.toBeNull()
  })

  it('可覆寫顯示文字', () => {
    render(<StatusBadge status="unhealthy" label="有異常主機" />)

    expect(screen.getByText('有異常主機')).toBeInTheDocument()
  })
})
