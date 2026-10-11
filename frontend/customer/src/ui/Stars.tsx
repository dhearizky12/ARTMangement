export function Stars({
  rating,
  size = 'sm',
}: {
  rating: number
  size?: 'xs' | 'sm' | 'md' | 'lg'
}) {
  const glyph =
    size === 'lg' ? 'text-34' : size === 'md' ? 'text-15' : size === 'xs' ? 'text-11' : 'text-12'
  const off = size === 'lg' ? 'text-star-off-lg' : 'text-star-off'
  const gap = size === 'lg' ? 'gap-6' : size === 'xs' ? 'gap-1' : 'gap-2'
  return (
    <div className={`flex ${gap}`}>
      {[1, 2, 3, 4, 5].map((i) => (
        <i
          key={i}
          className={`ph-fill ph-star ${glyph} ${i <= rating ? 'text-star' : off}`}
        />
      ))}
    </div>
  )
}
