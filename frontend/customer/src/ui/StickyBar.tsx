import type { ReactNode } from 'react'

export function StickyBar({ children }: { children: ReactNode }) {
  return (
    <div
      data-sticky-bar
      className="fixed right-0 bottom-0 left-0 flex items-center gap-14 border-t border-hairline bg-card px-20 pt-13 pb-bar-bottom shadow-sticky"
    >
      {children}
    </div>
  )
}

export type ActionTone = 'brand' | 'whatsapp' | 'danger'

export type ActionPad = 14 | 15 | 16
export type ActionGap = 0 | 7 | 8 | 9

const PAD: Record<ActionPad, string> = { 14: 'p-14', 15: 'p-15', 16: 'p-16' }
const GAP: Record<ActionGap, string> = { 0: '', 7: 'gap-7', 8: 'gap-8', 9: 'gap-9' }

export function actionButtonClass(
  tone: ActionTone = 'brand',
  disabled = false,
  pad: ActionPad = 16,
  gap: ActionGap = 9,
): string {
  const bg = disabled
    ? 'bg-control-off'
    : tone === 'whatsapp'
      ? 'bg-whatsapp'
      : tone === 'danger'
        ? 'bg-danger'
        : 'bg-pri'
  return `flex w-full flex-1 items-center justify-center rounded-15 text-15 font-extrabold text-white ${PAD[pad]} ${GAP[gap]} ${bg}`
}

export function ActionButton({
  label,
  onClick,
  tone = 'brand',
  disabled = false,
  pad = 16,
  gap = 9,
  type = 'button',
  form,
}: {
  label: ReactNode
  onClick?: () => void
  tone?: ActionTone
  disabled?: boolean
  pad?: ActionPad
  gap?: ActionGap
  type?: 'button' | 'submit'
  form?: string
}) {
  return (
    <button
      type={type}
      form={form}
      onClick={onClick}
      disabled={disabled}
      className={actionButtonClass(tone, disabled, pad, gap)}
    >
      {label}
    </button>
  )
}

export function StickyAction({
  label,
  onClick,
  tone = 'brand',
  disabled = false,
  pad = 16,
  gap = 0,
  type = 'button',
  form,
}: {
  label: ReactNode
  onClick?: () => void
  tone?: ActionTone
  disabled?: boolean
  pad?: ActionPad
  gap?: ActionGap
  type?: 'button' | 'submit'
  form?: string
}) {
  return (
    <StickyBar>
      <ActionButton
        label={label}
        onClick={onClick}
        tone={tone}
        disabled={disabled}
        pad={pad}
        gap={gap}
        type={type}
        form={form}
      />
    </StickyBar>
  )
}
