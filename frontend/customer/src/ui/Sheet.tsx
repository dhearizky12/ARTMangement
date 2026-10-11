import type { ReactNode } from 'react'

export function Sheet({ children }: { children: ReactNode }) {
  return <div className="rounded-16 border border-hairline bg-card p-16">{children}</div>
}

export function SheetActions({
  cancelLabel,
  confirmLabel,
  onCancel,
  onConfirm,
  destructive = false,
  disabled = false,
}: {
  cancelLabel: string
  confirmLabel: string
  onCancel: () => void
  onConfirm: () => void
  destructive?: boolean
  disabled?: boolean
}) {
  return (
    <div className="mt-14 flex gap-10">
      <button
        onClick={onCancel}
        className="flex-1 rounded-12 border border-control-idle bg-card p-12 text-13 font-bold text-ink-soft"
      >
        {cancelLabel}
      </button>
      <button
        onClick={onConfirm}
        disabled={disabled}
        className={`flex-1 rounded-12 p-12 text-13 font-extrabold text-white ${
          disabled ? 'bg-control-off' : destructive ? 'bg-danger' : 'bg-pri'
        }`}
      >
        {confirmLabel}
      </button>
    </div>
  )
}
