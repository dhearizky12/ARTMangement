export type IconButtonTone = 'onSurface' | 'onHero'

export function IconButton({
  icon,
  label,
  onClick,
  tone = 'onSurface',
  size = 40,
  filled = false,
  active,
  disabled = false,
}: {
  icon: string
  label: string
  onClick: () => void
  tone?: IconButtonTone
  size?: 38 | 40
  filled?: boolean
  active?: boolean
  disabled?: boolean
}) {
  const onHero = tone === 'onHero'
  const box = size === 38 ? 'h-38 w-38' : 'h-40 w-40'
  const glyph = size === 38 ? 'text-18' : 'text-19'
  const ink = onHero ? 'text-white' : active ? 'text-pri' : 'text-ink'
  return (
    <button
      onClick={onClick}
      aria-label={label}
      aria-pressed={active}
      disabled={disabled}
      className={`flex shrink-0 items-center justify-center rounded-half ${box} ${
        onHero ? 'bg-overlay-16' : 'border border-hairline bg-card'
      } ${disabled ? 'opacity-50' : ''}`.trim()}
    >
      <i className={`${filled ? 'ph-fill' : 'ph'} ${icon} ${glyph} ${ink}`} />
    </button>
  )
}
