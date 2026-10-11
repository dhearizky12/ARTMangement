import type { ReactNode } from 'react'

export type PillTone = 'brand' | 'promo' | 'danger' | 'info' | 'muted' | 'status'

const TONE: Record<PillTone, string> = {
  brand: 'bg-pri-tint text-pri',
  promo: 'bg-promo-tint text-promo-ink-soft',
  danger: 'bg-danger-tint text-danger',
  info: 'bg-info-tint text-info',
  muted: 'bg-hairline-faint text-ink-soft',
  status: 'bg-status text-status',
}

export function Pill({
  children,
  tone = 'brand',
  size = 'md',
  ...rest
}: {
  children: ReactNode
  tone?: PillTone
  size?: 'xs' | 'sm' | 'md'
} & { 'data-status'?: string }) {
  const s =
    size === 'xs'
      ? 'px-9 py-4 text-11-5'
      : size === 'sm'
        ? 'px-11 py-5 text-10-5'
        : 'px-12 py-6 text-10-5'
  return (
    <span
      {...rest}
      className={`rounded-20 font-extrabold whitespace-nowrap ${s} ${TONE[tone]}`}
    >
      {children}
    </span>
  )
}
