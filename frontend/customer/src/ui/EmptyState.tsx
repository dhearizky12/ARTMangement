import type { ReactNode } from 'react'
import { IconTile } from './IconTile'

export type EmptyVariant = 'search' | 'orders' | 'addresses'

export function EmptyState({
  icon,
  title,
  body,
  variant = 'search',
}: {
  icon: string
  title: string
  body: ReactNode
  variant?: EmptyVariant
}) {
  if (variant === 'orders') {
    return (
      <div className="mx-2 my-4 rounded-card border border-dashed border-divider-strong bg-card p-28 text-center">
        <i className={`${icon} text-34 text-glyph-2`} />
        <div className="mt-8 text-14-5 font-extrabold text-ink">{title}</div>
        <div className="mt-4 text-12-5 text-ink-soft">{body}</div>
      </div>
    )
  }

  if (variant === 'addresses') {
    return (
      <div className="px-20 pt-26 text-center">
        <IconTile
          icon={icon}
          tone="faint"
          size={70}
          radius="full"
          glyph={33}
          className="inline-flex"
        />
        <div className="mt-14 text-15-5 font-extrabold text-ink">{title}</div>
        <div className="mt-6 text-12-5 leading-prose text-ink-soft">{body}</div>
      </div>
    )
  }

  return (
    <div className="px-30 pt-34 pb-40 text-center">
      <IconTile icon={icon} tone="sunk" size={58} radius={18} glyph={26} className="inline-flex" />
      <div className="mt-14 text-14-5 font-extrabold text-ink">{title}</div>
      <div className="mt-5 text-12-5 leading-relaxed text-ink-soft">{body}</div>
    </div>
  )
}
