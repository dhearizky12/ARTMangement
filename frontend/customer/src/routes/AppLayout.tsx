import { useEffect, useLayoutEffect, useRef } from 'react'
import { Outlet, useLocation, useNavigationType } from 'react-router-dom'
import { BottomNav } from '../components/BottomNav'
import { ScrollArea, useScrollArea } from '../ui'

// How long a restored scroll position keeps being re-applied
const RESTORE_WINDOW_MS = 600

// Kept outside the component because the hooks lint rejects writing to a value
// that came from a hook, and the scroll element does.
function setOffset(node: HTMLElement, top: number): number {
  node.scrollTop = top
  return node.scrollTop
}

// The app has one scroll element that outlives route changes, so a new screen
// would start at the old offset. Reset to the top on push, restore on back.
function ScrollMemory() {
  const el = useScrollArea()
  const { key } = useLocation()
  const navigationType = useNavigationType()

  const saved = useRef(new Map<string, number>())
  const current = useRef<string | null>(null)
  // Tracked from scroll events: by the time the route changes the new screen has
  // rendered and the browser may already have clamped scrollTop.
  const live = useRef(0)

  useEffect(() => {
    if (!el) return
    const track = () => {
      live.current = el.scrollTop
    }
    el.addEventListener('scroll', track, { passive: true })
    return () => el.removeEventListener('scroll', track)
  }, [el])

  useLayoutEffect(() => {
    if (!el) return
    if (current.current !== null) saved.current.set(current.current, live.current)
    current.current = key

    const target = navigationType === 'POP' ? (saved.current.get(key) ?? 0) : 0
    live.current = setOffset(el, target)
    if (target === 0) return

    // The list is usually still empty on first mount, so one assignment gets
    // clamped back to the top. Retry for a short while.
    const raf = { id: 0 }
    const until = performance.now() + RESTORE_WINDOW_MS
    const apply = () => {
      if (Math.abs(el.scrollTop - target) < 1 || performance.now() > until) return
      live.current = setOffset(el, target)
      raf.id = requestAnimationFrame(apply)
    }
    raf.id = requestAnimationFrame(apply)
    return () => cancelAnimationFrame(raf.id)
  }, [el, key, navigationType])

  return null
}

export function AppLayout({ withNav = false }: { withNav?: boolean }) {
  return (
    <div className="relative flex min-h-0 flex-1">
      <ScrollArea>
        <ScrollMemory />
        <Outlet />
      </ScrollArea>
      {withNav && <BottomNav />}
    </div>
  )
}

// Bottom clearance for the last element, since the tab bar overlays the scroll area
export const tabScreenPad = 'h-tab-clear'
export const pushedScreenPad = 'h-pushed-clear'
