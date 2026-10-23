import { describe, expect, it } from 'vitest'
import type { AttentionItem } from '../../../src/data/types'
import { sortAttentionItems } from '../../../src/pages/overview/sortAttentionItems'

const item = (id: string, severity: AttentionItem['severity'], occurredAt: string): AttentionItem => ({
  id,
  type: 'unpublishedChanges',
  severity,
  params: {},
  target: '/',
  occurredAt,
})

describe('sortAttentionItems', () => {
  it('依嚴重程度 critical > warning > info 排序', () => {
    const result = sortAttentionItems([
      item('info', 'info', '2026-10-19T03:00:00Z'),
      item('warning', 'warning', '2026-10-19T02:00:00Z'),
      item('critical', 'critical', '2026-10-19T01:00:00Z'),
    ])

    expect(result.map((x) => x.id)).toEqual(['critical', 'warning', 'info'])
  })

  it('同級依發生時間新到舊', () => {
    const result = sortAttentionItems([
      item('older', 'warning', '2026-10-18T00:00:00Z'),
      item('newer', 'warning', '2026-10-19T00:00:00Z'),
    ])

    expect(result.map((x) => x.id)).toEqual(['newer', 'older'])
  })

  it('空陣列回傳空陣列且不修改原陣列', () => {
    const input: AttentionItem[] = []

    expect(sortAttentionItems(input)).toEqual([])
    expect(sortAttentionItems(input)).not.toBe(input)
  })
})
