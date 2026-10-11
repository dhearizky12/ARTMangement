import type { ReactNode } from 'react'
import { IconTile } from './IconTile'

export function ErrorState({
  message,
  onRetry,
  retryLabel = 'Coba Lagi',
  inline = false,
}: {
  message: string
  onRetry?: () => void
  retryLabel?: string
  inline?: boolean
}) {
  return (
    <div className={inline ? 'px-20 py-24 text-center' : 'px-30 pt-34 pb-40 text-center'}>
      <IconTile
        icon="ph ph-cloud-warning"
        tone="danger"
        size={inline ? 48 : 58}
        radius={inline ? 14 : 18}
        glyph={inline ? 22 : 26}
        className="inline-flex"
      />
      <div className="mt-14 text-14-5 font-extrabold text-ink">Gagal memuat</div>
      <div className="mt-5 text-12-5 leading-relaxed text-ink-soft">{message}</div>
      {onRetry && (
        <button
          onClick={onRetry}
          className="mt-16 rounded-14 bg-pri-tint px-20 py-12 text-13 font-extrabold text-pri"
        >
          {retryLabel}
        </button>
      )}
    </div>
  )
}

export function NotFoundState({
  title = 'Tidak ditemukan',
  body,
  action,
}: {
  title?: string
  body: string
  action?: ReactNode
}) {
  return (
    <div className="px-30 pt-34 pb-40 text-center">
      <IconTile
        icon="ph ph-magnifying-glass"
        tone="sunk"
        glyphTone="text-glyph-1"
        size={58}
        radius={18}
        glyph={26}
        className="inline-flex"
      />
      <div className="mt-14 text-14-5 font-extrabold text-ink">{title}</div>
      <div className="mt-5 text-12-5 leading-relaxed text-ink-soft">{body}</div>
      {action && <div className="mt-16">{action}</div>}
    </div>
  )
}
