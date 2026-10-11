
export function Skeleton({
  h,
  w = 'full',
  radius = 6,
}: {
  h: 8 | 10 | 12 | 14 | 16 | 18 | 20
  w?: 'full' | '3/4' | '1/2' | '1/3' | '1/4'
  radius?: 2 | 6 | 8
}) {
  const H = { 8: 'h-8', 10: 'h-10', 12: 'h-12', 14: 'h-14', 16: 'h-16', 18: 'h-18', 20: 'h-20' }
  const W = { full: 'w-full', '3/4': 'w-3/4', '1/2': 'w-1/2', '1/3': 'w-1/3', '1/4': 'w-1/4' }
  const R = { 2: 'rounded-2', 6: 'rounded-6', 8: 'rounded-8' }
  return <div className={`bg-hairline ${H[h]} ${W[w]} ${R[radius]}`} />
}

export function HelperCardSkeleton({
  variant,
}: {
  variant: 'featured' | 'browse' | 'compact'
}) {
  const avatar =
    variant === 'browse' ? 'h-64 w-64 rounded-16' : variant === 'featured' ? 'h-60 w-60 rounded-16' : 'h-56 w-56 rounded-15'

  return (
    <div
      className={`flex gap-12 rounded-card border border-hairline bg-card ${
        variant === 'browse'
          ? 'items-start p-13'
          : variant === 'compact'
            ? 'items-center p-12'
            : 'items-stretch p-12'
      }`}
    >
      <div className={`shrink-0 bg-hairline ${avatar}`} />

      <div className="min-w-0 flex-1">
        <Skeleton h={16} w="3/4" />
        <div className="mt-4">
          <Skeleton h={12} w="1/2" />
        </div>
        <div className={variant === 'compact' ? 'mt-5' : variant === 'browse' ? 'mt-7' : 'mt-6'}>
          <Skeleton h={12} w="3/4" />
        </div>
      </div>

      {variant !== 'compact' && (
        <div className="flex w-90 shrink-0 flex-col items-end gap-6">
          <Skeleton h={14} w="3/4" />
          <Skeleton h={10} w="1/2" />
        </div>
      )}
      {variant === 'compact' && <div className="h-14 w-8 shrink-0 rounded-2 bg-hairline" />}
    </div>
  )
}

export function OrderCardSkeleton() {
  return (
    <div className="rounded-card border border-hairline bg-card p-14">
      <div className="flex items-center gap-12">
        <div className="h-50 w-50 shrink-0 rounded-14 bg-hairline" />
        <div className="min-w-0 flex-1">
          <Skeleton h={14} w="1/2" />
          <div className="mt-5">
            <Skeleton h={12} w="3/4" />
          </div>
        </div>
        <Skeleton h={18} w="1/4" radius={8} />
      </div>
    </div>
  )
}

export function CardSkeleton({ rows = 3 }: { rows?: number }) {
  return (
    <div className="rounded-card border border-hairline bg-card p-16">
      <Skeleton h={14} w="1/3" />
      <div className="mt-12 flex flex-col gap-10">
        {Array.from({ length: rows }).map((_, i) => (
          <Skeleton key={i} h={12} w={i % 2 ? '3/4' : 'full'} />
        ))}
      </div>
    </div>
  )
}
