import { useEffect, useRef, type ReactNode } from 'react'

export function BottomSheet({
  title,
  onDismiss,
  children,
}: {
  title: string
  onDismiss: () => void
  children: ReactNode
}) {
  const panel = useRef<HTMLDivElement>(null)
  // Where focus came from, so it can go back. Losing it to <body> on close
  // leaves a keyboard or screen-reader user at the top of the document.
  const opener = useRef<HTMLElement | null>(null)

  useEffect(() => {
    opener.current = document.activeElement as HTMLElement | null
    panel.current?.focus({ preventScroll: true })
    return () => opener.current?.focus?.({ preventScroll: true })
  }, [])

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        e.stopPropagation()
        onDismiss()
        return
      }
      if (e.key !== 'Tab' || !panel.current) return
      // Focus stays inside while the sheet is open. Without this, tabbing walks
      // into the screen behind the scrim, which cannot be interacted with.
      const focusable = panel.current.querySelectorAll<HTMLElement>(
        'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])',
      )
      if (focusable.length === 0) return
      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      const active = document.activeElement
      if (e.shiftKey && (active === first || active === panel.current)) {
        e.preventDefault()
        last.focus()
      } else if (!e.shiftKey && active === last) {
        e.preventDefault()
        first.focus()
      }
    }
    document.addEventListener('keydown', onKey, true)
    return () => document.removeEventListener('keydown', onKey, true)
  }, [onDismiss])

  return (
    <div className="fixed inset-0 z-40 flex flex-col justify-end overflow-clip">
      <button
        type="button"
        aria-label="Tutup"
        onClick={onDismiss}
        className="absolute inset-0 animate-scrim-in bg-scrim"
      />
      <div
        ref={panel}
        role="dialog"
        aria-modal="true"
        aria-label={title}
        tabIndex={-1}
        data-scroll
        className="relative max-h-4/5 animate-sheet-up overflow-y-auto rounded-t-22 bg-card px-20 pt-8 pb-sheet-bottom shadow-raise-deep"
      >
        <div className="mx-auto mb-14 h-4 w-40 rounded-pill bg-control-idle" />
        <div className="flex items-center justify-between">
          <span className="text-16 font-extrabold text-ink">{title}</span>
          <button
            type="button"
            aria-label="Tutup"
            onClick={onDismiss}
            className="flex h-32 w-32 items-center justify-center rounded-half bg-surface-muted text-ink-soft"
          >
            <i className="ph ph-x text-16" />
          </button>
        </div>
        <div className="pb-20">{children}</div>
      </div>
    </div>
  )
}
