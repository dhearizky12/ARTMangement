import type { ReactNode } from 'react'

export type MoneyRowVariant = 'plain' | 'accent' | 'small' | 'onHero'

export function MoneyRow({
  label,
  value,
  variant = 'plain',
  gap = 16,
}: {
  label: ReactNode
  value: ReactNode
  variant?: MoneyRowVariant
  gap?: 12 | 16 | 20
}) {
  const gapCls = gap === 12 ? 'gap-12' : gap === 20 ? 'gap-20' : 'gap-16'

  if (variant === 'onHero') {
    return (
      <div className={`flex justify-between py-3 text-12-5 ${gapCls}`}>
        <span className="opacity-82">{label}</span>
        <b className="text-right">{value}</b>
      </div>
    )
  }

  return (
    <div className={`flex justify-between ${gapCls}`}>
      <span className="shrink-0 font-semibold text-ink-faint">{label}</span>
      <span
        className={`text-right ${
          variant === 'accent'
            ? 'font-bold text-pri'
            : variant === 'small'
              ? 'text-12 font-semibold text-ink-body'
              : 'font-bold text-ink'
        }`}
      >
        {value}
      </span>
    </div>
  )
}

export function DetailRow({
  icon,
  label,
  value,
  sub,
}: {
  icon: string
  label: string
  value: string
  sub?: string
}) {
  return (
    <div className="flex gap-11">
      <i className={`ph ${icon} mt-1 text-17 text-pri`} />
      <span className="flex-1">
        <span className="block text-11-5 font-semibold text-ink-faint">{label}</span>
        <span className="mt-1 block text-13 leading-normal font-bold text-ink">{value}</span>
        {sub && <span className="mt-2 block text-11-5 text-ink-soft">{sub}</span>}
      </span>
    </div>
  )
}
