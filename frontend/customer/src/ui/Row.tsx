import type { ReactNode } from 'react'

export function RowBody({
  children,
  last = false,
}: {
  children: ReactNode
  last?: boolean
}) {
  return (
    <span
      className={`flex w-full items-center gap-13 p-15 text-left ${
        last ? '' : 'border-b border-hairline-faint'
      }`}
    >
      {children}
    </span>
  )
}

export function RowLabel({ children }: { children: ReactNode }) {
  return <span className="flex-1 text-13-5 font-bold text-ink">{children}</span>
}

export function RowChevron() {
  return <i className="ph ph-caret-right text-ink-ghost" />
}

export type RowActionTone = 'plain' | 'brand' | 'danger'

const ACTION_TONE: Record<RowActionTone, string> = {
  plain: 'text-ink-prose',
  brand: 'text-pri',
  danger: 'text-danger',
}

export function RowAction({
  icon,
  label,
  tone = 'plain',
  divider = false,
  onClick,
}: {
  icon: string
  label: string
  tone?: RowActionTone
  divider?: boolean
  onClick?: () => void
}) {
  return (
    <button onClick={onClick} className={actionClass(tone, divider)}>
      <i className={`${icon} text-15`} />
      {label}
    </button>
  )
}

export function actionClass(tone: RowActionTone = 'plain', divider = false): string {
  return `flex flex-1 items-center justify-center gap-6 px-6 py-12 text-12 font-bold ${
    ACTION_TONE[tone]
  } ${divider ? 'border-r border-hairline-faint' : ''}`
}
