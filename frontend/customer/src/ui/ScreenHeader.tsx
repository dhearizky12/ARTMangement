import type { ReactNode } from 'react'
import { IconButton } from './IconButton'

export function ScreenHeader({
  title,
  right,
  onBack,
  icon = 'ph-arrow-left',
  bare = false,
  ground = 'surface',
  sticky = true,
  size = 40,
}: {
  title: string
  right?: ReactNode
  onBack: () => void
  icon?: string
  bare?: boolean
  ground?: 'surface' | 'sunk'
  sticky?: boolean
  size?: 38 | 40
}) {
  return (
    <div
      className={`flex items-center gap-12 px-20 pt-screen-top pb-12 ${
        sticky ? 'sticky top-0 z-20' : ''
      } ${bare ? '' : 'border-b border-hairline-soft'} ${
        ground === 'sunk' ? 'bg-hairline-soft' : 'bg-surface'
      }`}
    >
      <IconButton icon={icon} label="Kembali" onClick={onBack} size={size} />
      <span className="flex-1 text-18 font-extrabold text-ink">{title}</span>
      {right}
    </div>
  )
}
