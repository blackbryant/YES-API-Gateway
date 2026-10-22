import { fireEvent, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ExperimentInput } from '../../../src/data/types'
import { SplitPanel } from '../../../src/pages/services/panels/SplitPanel'
import { defaultExperiment } from '../../../src/pages/services/serviceForm'
import { renderWithProviders } from '../test-utils'

const HOSTS = [{ id: 1, address: 'http://10.0.0.1:8080', isActive: true }]

function threeGroups(): ExperimentInput {
  const experiment = defaultExperiment(HOSTS)
  return {
    ...experiment,
    variants: [
      { ...experiment.variants[0], weightPercent: 60 },
      { ...experiment.variants[1], weightPercent: 20 },
      { variantKey: 'C', weightPercent: 10, isControl: false, hosts: [] },
    ],
  }
}

describe('SplitPanel', () => {
  it('由不分流切換為 A/B 時，原主機成為 A 組（對照組）', async () => {
    const onChange = vi.fn()
    renderWithProviders(<SplitPanel routingMode="Single" experiment={null} hosts={HOSTS} readOnly={false} onChange={onChange} />)

    await userEvent.click(screen.getByRole('radio', { name: 'A/B 測試' }))

    const [mode, experiment] = onChange.mock.calls[0]
    expect(mode).toBe('AbTest')
    expect(experiment.variants[0]).toMatchObject({ variantKey: 'A', isControl: true, hosts: HOSTS, weightPercent: 90 })
    expect(experiment.forceRules[0]).toMatchObject({ matchType: 'Header', matchName: 'X-Exp-Group' })
  })

  it('兩組時調整一組比例，另一組自動補足為 100', () => {
    const onChange = vi.fn()
    renderWithProviders(
      <SplitPanel routingMode="AbTest" experiment={defaultExperiment(HOSTS)} hosts={[]} readOnly={false} onChange={onChange} />,
    )

    const input = screen.getByRole('spinbutton', { name: 'B 組 %' })
    fireEvent.change(input, { target: { value: '30' } })
    fireEvent.blur(input)

    const experiment: ExperimentInput = onChange.mock.lastCall![1]
    expect(experiment.variants.map((v) => v.weightPercent)).toEqual([70, 30])
  })

  it('三組以上加總不為 100 時顯示錯誤', () => {
    renderWithProviders(<SplitPanel routingMode="AbTest" experiment={threeGroups()} hosts={[]} readOnly={false} onChange={vi.fn()} />)

    expect(screen.getByText('各組比例加總必須為 100（目前 90）')).toBeInTheDocument()
  })

  it('強制分組標頭與自動回退設定會送出', async () => {
    const onChange = vi.fn()
    renderWithProviders(
      <SplitPanel routingMode="AbTest" experiment={defaultExperiment(HOSTS)} hosts={[]} readOnly={false} onChange={onChange} />,
    )

    await userEvent.click(screen.getByRole('switch', { name: '啟用自動回退' }))

    expect(onChange.mock.lastCall![1].autoRollback).toEqual({ thresholdPercent: 2, durationMin: 5, minSamples: 100 })
  })

  it('有效狀態以燈號加文字呈現，並依狀態提供手動操作', async () => {
    const onAction = vi.fn()
    renderWithProviders(
      <SplitPanel
        routingMode="AbTest"
        experiment={defaultExperiment(HOSTS)}
        hosts={[]}
        effectiveStatus="Paused"
        isAutoRolledBack
        readOnly={false}
        onChange={vi.fn()}
        onAction={onAction}
      />,
    )

    expect(screen.getByText('已自動回退')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /暫\s?停/ })).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /恢\s?復/ }))
    expect(onAction).toHaveBeenCalledWith('Resume')
  })

  it('結束實驗前需要確認', async () => {
    const onAction = vi.fn()
    renderWithProviders(
      <SplitPanel
        routingMode="AbTest"
        experiment={defaultExperiment(HOSTS)}
        hosts={[]}
        effectiveStatus="Running"
        readOnly={false}
        onChange={vi.fn()}
        onAction={onAction}
      />,
    )

    await userEvent.click(screen.getByRole('button', { name: /結\s?束/ }))
    expect(onAction).not.toHaveBeenCalled()
    expect(await screen.findByText('結束後需修改設定並重新發佈才可再次執行，確定結束？')).toBeInTheDocument()

    await userEvent.click(screen.getByRole('button', { name: /確\s?認/ }))
    expect(onAction).toHaveBeenCalledWith('End')
  })

  it('唯讀時不顯示手動操作且欄位停用', () => {
    renderWithProviders(
      <SplitPanel routingMode="AbTest" experiment={defaultExperiment(HOSTS)} hosts={[]} effectiveStatus="Running" readOnly onChange={vi.fn()} />,
    )

    expect(screen.queryByRole('button', { name: /暫\s?停/ })).not.toBeInTheDocument()
    expect(screen.getByRole('spinbutton', { name: 'B 組 %' })).toBeDisabled()
  })
})
