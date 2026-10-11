import type { ReactNode } from 'react'

export type CardVariant = 'card' | 'panel'

const RADIUS: Record<CardVariant, string> = {
  card: 'rounded-card',
  panel: 'rounded-card-sm',
}

export function Card({
  children,
  variant = 'card',
  className = '',
}: {
  children: ReactNode
  variant?: CardVariant
  className?: string
}) {
  return (
    <div
      className={`border border-hairline bg-card p-16 ${RADIUS[variant]} ${className}`.trim()}
    >
      {children}
    </div>
  )
}

export function CardTitle({ children }: { children: ReactNode }) {
  return <div className="mb-12 text-13-5 font-extrabold text-ink">{children}</div>
}
