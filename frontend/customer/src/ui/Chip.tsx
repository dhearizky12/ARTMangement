import type { ReactNode } from 'react'

export type ChipVariant = 'option' | 'filter' | 'block' | 'tag'

const TONE = {
  option: {
    on: 'border-pri bg-pri-tint text-pri',
    off: 'border-control-idle bg-card text-ink-label',
  },
  filter: {
    on: 'border-pri bg-pri text-white',
    off: 'border-control-idle bg-card text-ink-label',
  },
  block: {
    on: 'border-pri bg-pri-tint text-pri',
    off: 'border-control-idle bg-card text-ink-label',
  },
  tag: {
    on: 'border-pri bg-pri-tint text-pri',
    off: 'border-control-idle bg-card text-ink-label',
  },
} as const

const SHAPE = {
  option: 'rounded-chip border-[1.5px] px-14 py-9 text-12-5 font-bold',
  filter: 'shrink-0 rounded-pill border px-15 py-8 text-12-5 font-bold',
  block: 'flex-1 rounded-chip border-[1.5px] py-11 text-12-5 font-bold',
  tag: 'rounded-20 border-[1.5px] px-14 py-9 text-12-5 font-bold',
} as const

export function Chip({
  children,
  on,
  variant = 'option',
  onClick,
  className = '',
}: {
  children: ReactNode
  on: boolean
  variant?: ChipVariant
  onClick?: () => void
  className?: string
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`${SHAPE[variant]} ${on ? TONE[variant].on : TONE[variant].off} ${className}`.trim()}
    >
      {children}
    </button>
  )
}
