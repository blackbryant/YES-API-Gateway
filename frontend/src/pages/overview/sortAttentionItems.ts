import type { AttentionItem, Severity } from '../../data/types'

const SEVERITY_RANK: Record<Severity, number> = { critical: 0, warning: 1, info: 2 }

/** 依嚴重程度（critical > warning > info）排序，同級依發生時間新到舊（research R9） */
export function sortAttentionItems(items: readonly AttentionItem[]): AttentionItem[] {
  return [...items].sort(
    (a, b) =>
      SEVERITY_RANK[a.severity] - SEVERITY_RANK[b.severity] ||
      Date.parse(b.occurredAt) - Date.parse(a.occurredAt),
  )
}
