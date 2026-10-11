import { useCallback, useEffect, useRef, useState, type ReactNode } from 'react'
import { useScrollArea } from './ScrollArea'

export function useCondense(): {
  condensed: boolean
  sentinelRef: (el: HTMLElement | null) => void
} {
  const [condensed, setCondensed] = useState(false)
  const scroller = useScrollArea()
  const node = useRef<HTMLElement | null>(null)

  const sentinelRef = useCallback((el: HTMLElement | null) => {
    node.current = el
  }, [])

  useEffect(() => {
    const el = node.current
    if (!el || !scroller) return
    const io = new IntersectionObserver(([entry]) => setCondensed(!entry.isIntersecting), {
      root: scroller,
    })
    io.observe(el)
    return () => io.disconnect()
  }, [scroller])

  return { condensed, sentinelRef }
}

export function ScrollHeader({
  back,
  title,
  subtitle,
  right,
  tone = 'onSurface',
  condensed = false,
}: {
  back: ReactNode
  title?: string
  subtitle?: string
  right?: ReactNode
  tone?: 'onSurface' | 'onHero'
  condensed?: boolean
}) {
  return (
    <div
      data-condensed={condensed}
      data-header-tone={tone}
      className="sticky top-0 z-20 border-b bg-header px-18 pt-screen-top pb-10 border-header"
    >
      <div className="flex items-center gap-12">
        {back}
        <span className="min-w-0 flex-1 header-title">
          <span
            className={`block truncate text-16 font-extrabold ${
              tone === 'onHero' ? 'text-white' : 'text-ink'
            }`}
          >
            {title ?? ''}
          </span>
          {subtitle && (
            <span
              className={`block truncate text-11-5 ${
                tone === 'onHero' ? 'text-white opacity-85' : 'text-ink-soft'
              }`}
            >
              {subtitle}
            </span>
          )}
        </span>
        {right}
      </div>
    </div>
  )
}
