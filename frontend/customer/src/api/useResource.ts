import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { ApiError, networkError } from './errors'
import { isAbort } from './client'
import { readCache, writeCache } from './cache'

export interface Resource<T> {
  data: T | null
  error: ApiError | null
  loading: boolean
  retry: () => void
  refresh: () => Promise<void>
  refreshing: boolean
}

interface Settled<T> {
  key: symbol
  data: T | null
  error: ApiError | null
}

interface Attempt {
  n: number
  preserve: boolean
}

interface Waiter {
  ok: () => void
  fail: (e: ApiError) => void
}

export function useResource<T>(
  fetcher: (signal: AbortSignal) => Promise<T>,
  deps: readonly unknown[] = [],
  options: {
    // Names the resource so a later mount can reuse it. Explicit, not derived from
    // deps: two lists with no deps would otherwise share a cache entry.
    cacheKey?: string
  } = {},
): Resource<T> {
  const [attempt, setAttempt] = useState<Attempt>({ n: 0, preserve: false })

  // deps flattened to a string so the hooks lint can check it. Pass ids, not objects.
  const depsKey = deps.map((d) => String(d)).join('\u0000')

  const key = useMemo(
    () => Symbol(`request:${attempt.n}:${depsKey}`),
    [attempt.n, depsKey],
  )

  const cacheKey = options.cacheKey ? `${options.cacheKey}|${depsKey}` : null

  const [seed] = useState(() => (cacheKey ? readCache<T>(cacheKey) : undefined))
  const [settled, setSettled] = useState<Settled<T>>(() => ({
    key: Symbol(seed === undefined ? 'unstarted' : 'cached'),
    data: seed ?? null,
    error: null,
  }))

  const seededFromCache = seed !== undefined

  const waiting = useRef<Waiter | null>(null)

  const preserving = useRef(false)
  useEffect(() => {
    preserving.current = attempt.preserve || (attempt.n === 0 && seededFromCache)
  }, [attempt.preserve, attempt.n, seededFromCache])

  const fetcherRef = useRef(fetcher)
  useEffect(() => {
    fetcherRef.current = fetcher
  })

  useEffect(() => {
    const controller = new AbortController()
    let live = true

    const mine = waiting.current

    const done = (e?: ApiError) => {
      if (waiting.current !== mine || !mine) return
      waiting.current = null
      if (e) mine.fail(e)
      else mine.ok()
    }

    fetcherRef.current(controller.signal).then(
      (value) => {
        if (!live) return
        setSettled({ key, data: value, error: null })
        if (cacheKey) writeCache(cacheKey, value)
        done()
      },
      (cause) => {
        if (!live || isAbort(cause)) return
        const error = cause instanceof ApiError ? cause : networkError(cause)
        setSettled((prev) =>
          preserving.current && prev.data !== null
            ? { key, data: prev.data, error: null }
            : { key, data: null, error },
        )
        done(error)
      },
    )

    return () => {
      live = false
      controller.abort()
      done()
    }
  }, [key, cacheKey])

  const retry = useCallback(() => setAttempt((a) => ({ n: a.n + 1, preserve: false })), [])

  const refresh = useCallback(
    () =>
      new Promise<void>((resolve, reject) => {
        waiting.current?.ok()
        waiting.current = { ok: resolve, fail: reject }
        setAttempt((a) => ({ n: a.n + 1, preserve: true }))
      }),
    [],
  )

  const current = settled.key === key
  const keepsLast = attempt.preserve || (attempt.n === 0 && seededFromCache)
  const show = current || keepsLast

  return {
    data: show ? settled.data : null,
    error: show ? settled.error : null,
    loading: !current && !keepsLast,
    refreshing: !current && keepsLast,
    retry,
    refresh,
  }
}
