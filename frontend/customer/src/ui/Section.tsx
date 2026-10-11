import type { ReactNode } from 'react'

export function Heading({
  children,
  top = false,
  gap = 'sm',
}: {
  children: ReactNode
  top?: boolean
  gap?: 'sm' | 'md'
}) {
  const m =
    gap === 'sm'
      ? top
        ? 'mt-16 mb-9'
        : 'mb-7'
      : top
        ? 'mt-18 mb-10'
        : 'mb-10'
  return <div className={`text-14 font-extrabold text-ink ${m}`}>{children}</div>
}

export function SectionLabel({ children }: { children: ReactNode }) {
  return (
    <div className="px-20 pt-22 pb-8 text-13 font-extrabold tracking-wide text-ink-soft uppercase">
      {children}
    </div>
  )
}

export function FieldLabel({ children }: { children: ReactNode }) {
  return <div className="mb-7 text-12-5 font-extrabold text-ink">{children}</div>
}

export function SectionTitle({
  children,
  action,
}: {
  children: ReactNode
  action?: ReactNode
}) {
  return (
    <div
      className={`flex items-baseline justify-between px-20 ${
        action ? 'pt-24 pb-6' : 'pt-22 pb-4'
      }`}
    >
      <div className="text-17 font-extrabold text-ink">{children}</div>
      {action}
    </div>
  )
}

export function FormError({ children }: { children: ReactNode }) {
  return <div className="text-12-5 font-bold text-danger">{children}</div>
}
