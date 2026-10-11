import type { ReactNode } from 'react'

export type AvatarSize = 34 | 38 | 42 | 44 | 48 | 50 | 52 | 56 | 60 | 64 | 88
export type AvatarRadius = 'full' | 12 | 13 | 14 | 15 | 16 | 20 | 26
export type AvatarText = 12 | 13 | 14 | 16 | 18 | 19 | 20 | 21 | 30

const SIZE: Record<AvatarSize, string> = {
  34: 'h-34 w-34',
  38: 'h-38 w-38',
  42: 'h-42 w-42',
  44: 'h-44 w-44',
  48: 'h-48 w-48',
  50: 'h-50 w-50',
  52: 'h-52 w-52',
  56: 'h-56 w-56',
  60: 'h-60 w-60',
  64: 'h-64 w-64',
  88: 'h-88 w-88',
}

const RADIUS: Record<AvatarRadius, string> = {
  full: 'rounded-half',
  12: 'rounded-12',
  13: 'rounded-13',
  14: 'rounded-14',
  15: 'rounded-15',
  16: 'rounded-16',
  20: 'rounded-20',
  26: 'rounded-26',
}

const TEXT: Record<AvatarText, string> = {
  12: 'text-12',
  13: 'text-13',
  14: 'text-14',
  16: 'text-16',
  18: 'text-18',
  19: 'text-19',
  20: 'text-20',
  21: 'text-21',
  30: 'text-30',
}

export function Avatar({
  tone,
  initials,
  size,
  radius,
  text,
  weight = 800,
  className = '',
}: {
  tone: string
  initials: string
  size: AvatarSize
  radius: AvatarRadius
  text: AvatarText
  weight?: 700 | 800
  className?: string
}) {
  return (
    <div
      data-tone={tone}
      className={`bg-tone flex shrink-0 items-center justify-center text-white ${
        weight === 700 ? 'font-bold' : 'font-extrabold'
      } ${SIZE[size]} ${RADIUS[radius]} ${TEXT[text]} ${className}`.trim()}
    >
      {initials}
    </div>
  )
}

export function BrandTile({
  children,
  size,
  radius,
  text,
  className = '',
}: {
  children: ReactNode
  size: AvatarSize
  radius: AvatarRadius
  text: AvatarText
  className?: string
}) {
  return (
    <div
      className={`flex shrink-0 items-center justify-center bg-pri font-extrabold text-white ${SIZE[size]} ${RADIUS[radius]} ${TEXT[text]} ${className}`.trim()}
    >
      {children}
    </div>
  )
}
