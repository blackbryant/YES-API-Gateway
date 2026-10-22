import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ExperimentStatusBadge } from '../../../src/components/common/ExperimentStatusBadge'
import type { ExperimentStatus } from '../../../src/data/types'

describe('ExperimentStatusBadge', () => {
  it.each<[ExperimentStatus, string]>([
    ['Draft', '草稿'],
    ['Scheduled', '已排程'],
    ['Running', '執行中'],
    ['Paused', '已暫停'],
    ['Ended', '已結束'],
  ])('%s 顯示文字「%s」與圖示', (status, text) => {
    const { container } = render(<ExperimentStatusBadge status={status} />)

    expect(screen.getByText(text)).toBeInTheDocument()
    expect(container.querySelector('svg')).not.toBeNull()
  })

  it('自動回退造成的暫停顯示「已自動回退」', () => {
    render(<ExperimentStatusBadge status="Paused" autoRolledBack />)

    expect(screen.getByText('已自動回退')).toBeInTheDocument()
  })

  it('非暫停狀態忽略自動回退標記', () => {
    render(<ExperimentStatusBadge status="Running" autoRolledBack />)

    expect(screen.getByText('執行中')).toBeInTheDocument()
  })
})
