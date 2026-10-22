import { create } from 'zustand'

interface DraftState {
  /** 草稿異動序號：存檔或發佈後遞增，頂部列據此重新載入未發佈變更數 */
  revision: number
  bump: () => void
}

/** 草稿異動通知（不持久化） */
export const useDraftStore = create<DraftState>()((set) => ({
  revision: 0,
  bump: () => set((state) => ({ revision: state.revision + 1 })),
}))
