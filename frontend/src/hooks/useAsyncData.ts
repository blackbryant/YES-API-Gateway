import { useCallback, useEffect, useState } from 'react'

export type AsyncStatus = 'loading' | 'ready' | 'empty' | 'error'

interface AsyncState<T> {
  status: AsyncStatus
  data: T | undefined
  error: unknown
}

/** 載入非同步資料並回傳 loading / ready / empty / error 狀態 */
export function useAsyncData<T>(load: () => Promise<T>, isEmpty?: (data: T) => boolean) {
  const [state, setState] = useState<AsyncState<T>>({ status: 'loading', data: undefined, error: undefined })
  const [attempt, setAttempt] = useState(0)

  useEffect(() => {
    let cancelled = false

    load()
      .then((data) => {
        if (cancelled) return
        setState({ status: isEmpty?.(data) ? 'empty' : 'ready', data, error: undefined })
      })
      .catch((error: unknown) => {
        if (cancelled) return
        setState({ status: 'error', data: undefined, error })
      })

    return () => {
      cancelled = true
    }
    // load / isEmpty 視為穩定參考；重新載入由 attempt 觸發
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [attempt])

  const reload = useCallback(() => {
    setState((prev) => ({ ...prev, status: 'loading' }))
    setAttempt((n) => n + 1)
  }, [])

  return { ...state, reload }
}
