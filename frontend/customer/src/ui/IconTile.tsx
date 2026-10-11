import type { ReactNode } from 'react'

export type IconTileTone = 'brand' | 'muted' | 'promo' | 'danger' | 'info' | 'sunk' | 'faint' | 'onHero'

const TONE: Record<IconTileTone, { box: string; glyph: string }> = {
  brand: { box: 'bg-pri-tint', glyph: 'text-pri' },
  muted: { box: 'bg-surface-muted', glyph: 'text-ink-soft' },
  promo: { box: 'bg-promo', glyph: 'text-white' },
  danger: { box: 'bg-danger-tint', glyph: 'text-danger' },
  info: { box: 'bg-info-tint', glyph: 'text-info' },
  sunk: { box: 'bg-surface-sunk', glyph: 'text-glyph-1' },
  faint: { box: 'bg-hairline-faint', glyph: 'text-glyph-3' },
  onHero: { box: 'bg-overlay-16', glyph: 'text-white' },
}

export type TileSize = 30 | 34 | 38 | 42 | 46 | 48 | 56 | 58 | 70 | 78 | 104
export type GlyphSize = 15 | 17 | 18 | 19 | 21 | 22 | 24 | 26 | 28 | 30 | 33 | 34 | 56

const BOX: Record<TileSize, string> = {
  30: 'h-30 w-30',
  34: 'h-34 w-34',
  38: 'h-38 w-38',
  42: 'h-42 w-42',
  46: 'h-46 w-46',
  48: 'h-48 w-48',
  56: 'h-56 w-56',
  58: 'h-58 w-58',
  70: 'h-70 w-70',
  78: 'h-78 w-78',
  104: 'h-104 w-104',
}

const RADIUS = {
  9: 'rounded-9',
  10: 'rounded-10',
  11: 'rounded-11',
  12: 'rounded-12',
  13: 'rounded-13',
  14: 'rounded-14',
  17: 'rounded-17',
  18: 'rounded-18',
  full: 'rounded-half',
} as const

const GLYPH: Record<GlyphSize, string> = {
  15: 'text-15',
  17: 'text-17',
  18: 'text-18',
  19: 'text-19',
  21: 'text-21',
  22: 'text-22',
  24: 'text-24',
  26: 'text-26',
  28: 'text-28',
  30: 'text-30',
  33: 'text-33',
  34: 'text-34',
  56: 'text-56',
}

export function IconTile({
  icon,
  tone = 'brand',
  size = 42,
  radius = 12,
  glyph = 22,
  glyphTone,
  className = '',
}: {
  icon: string
  tone?: IconTileTone
  size?: TileSize
  radius?: keyof typeof RADIUS
  glyph?: GlyphSize
  glyphTone?: string
  className?: string
}) {
  const t = TONE[tone]
  return (
    <span
      className={`flex shrink-0 items-center justify-center ${BOX[size]} ${RADIUS[radius]} ${t.box} ${className}`.trim()}
    >
      <i className={`${icon} ${GLYPH[glyph]} ${glyphTone ?? t.glyph}`} />
    </span>
  )
}

export function Tile({
  children,
  tone = 'brand',
  size = 42,
  radius = 12,
  className = '',
}: {
  children: ReactNode
  tone?: IconTileTone
  size?: TileSize
  radius?: keyof typeof RADIUS
  className?: string
}) {
  return (
    <span
      className={`flex shrink-0 items-center justify-center ${BOX[size]} ${RADIUS[radius]} ${TONE[tone].box} ${className}`.trim()}
    >
      {children}
    </span>
  )
}
