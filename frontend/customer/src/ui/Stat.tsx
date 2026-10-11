import type { ReactNode } from 'react'

export function Stat({
  value,
  label,
  divider = false,
}: {
  value: ReactNode
  label: string
  divider?: boolean
}) {
  return (
    <div className={`flex-1 text-center ${divider ? 'border-r border-hairline-soft' : ''}`}>
      <div className="text-18 font-extrabold text-ink">{value}</div>
      <div className="mt-2 text-11 font-semibold text-ink-faint">{label}</div>
    </div>
  )
}

export function HeroStat({ children, label }: { children: ReactNode; label: string }) {
  return (
    <div className="flex-1 rounded-13 bg-overlay-14 p-11 text-center">
      <div className="text-16 font-extrabold">{children}</div>
      <div className="mt-1 text-10 opacity-85">{label}</div>
    </div>
  )
}
