import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useRef,
  useState,
  type ReactNode,
} from 'react'

interface ScrollAreaValue {
  el: HTMLElement | null
  register: (fn: (() => Promise<unknown>) | null) => void
}

const Ctx = createContext<ScrollAreaValue>({ el: null, register: () => {} })

export function useScrollArea(): HTMLElement | null {
  return useContext(Ctx).el
}

export function useRefresh(fn: () => Promise<unknown>): void {
  const { register } = useContext(Ctx)
  const ref = useRef(fn)
  useEffect(() => {
    ref.current = fn
  })
  useEffect(() => {
    register(() => ref.current())
    return () => register(null)
  }, [register])
}

const ARMED = 12
const THRESHOLD = 64

type PullState = 'idle' | 'pulling' | 'ready' | 'busy'

export function ScrollArea({ children }: { children: ReactNode }) {
  const [el, setEl] = useState<HTMLElement | null>(null)
  const handler = useRef<(() => Promise<unknown>) | null>(null)
  const [pull, setPull] = useState<PullState>('idle')
  const [failed, setFailed] = useState('')

  const register = useCallback((fn: (() => Promise<unknown>) | null) => {
    handler.current = fn
  }, [])

  const g = useRef<{ from: number; state: PullState; failed: boolean }>({
    from: -1,
    state: 'idle',
    failed: false,
  })

  useEffect(() => {
    if (!el) return

    const to = (next: PullState) => {
      if (g.current.state === next) return
      g.current.state = next
      setPull(next)
    }

    const start = (e: TouchEvent) => {
      g.current.from =
        g.current.state === 'busy' || !handler.current || el.scrollTop > 0
          ? -1
          : e.touches[0].clientY
    }

    const move = (e: TouchEvent) => {
      if (g.current.from < 0) return
      const d = e.touches[0].clientY - g.current.from
      if (d <= 0) {
        to('idle')
        g.current.from = -1
        return
      }
      if (d < ARMED) return
      if (g.current.failed) {
        g.current.failed = false
        setFailed('')
      }
      if (e.cancelable) e.preventDefault()
      to(d >= THRESHOLD ? 'ready' : 'pulling')
    }

    const end = () => {
      if (g.current.from < 0) return
      g.current.from = -1
      if (g.current.state !== 'ready' || !handler.current) {
        to('idle')
        return
      }
      to('busy')
      // Settled, not resolved: one resource failing must not take out the
      // others, and the indicator has to come away either way.
      void Promise.allSettled([handler.current()]).then((results) => {
        to('idle')
        const bad = results.find((r) => r.status === 'rejected')
        if (!bad || bad.status !== 'rejected') return
        const reason: unknown = bad.reason
        g.current.failed = true
        setFailed(
          reason instanceof Error && reason.message
            ? reason.message
            : 'Gagal memuat ulang. Coba lagi.',
        )
      })
    }

    el.addEventListener('touchstart', start, { passive: true })
    el.addEventListener('touchmove', move, { passive: false })
    el.addEventListener('touchend', end)
    el.addEventListener('touchcancel', end)
    return () => {
      el.removeEventListener('touchstart', start)
      el.removeEventListener('touchmove', move)
      el.removeEventListener('touchend', end)
      el.removeEventListener('touchcancel', end)
    }
  }, [el])

  return (
    <Ctx.Provider value={{ el, register }}>
      <main
        ref={setEl}
        data-scroll
        className="relative flex-1 overflow-y-auto overscroll-contain bg-surface"
      >
        <div
          data-pull={pull}
          aria-hidden={pull === 'idle'}
          role="status"
          className="pointer-events-none absolute inset-x-0 top-0 z-10 flex justify-center pull-indicator"
        >
          <span className="flex h-32 w-32 items-center justify-center rounded-half border border-hairline bg-card shadow-card">
            <i
              className={`ph-bold ${
                pull === 'busy' ? 'ph-spinner animate-spin-slow' : 'ph-arrow-down'
              } text-16 text-pri pull-arrow`}
            />
          </span>
        </div>
        {failed && (
          <div className="sticky top-0 z-10 px-18 pt-8">
            <div
              role="alert"
              className="flex items-center gap-8 rounded-12 border border-danger-border bg-danger-tint px-12 py-9 text-12 font-bold text-danger"
            >
              <i className="ph-fill ph-warning-circle text-15" />
              {failed}
            </div>
          </div>
        )}
        {children}
      </main>
    </Ctx.Provider>
  )
}
