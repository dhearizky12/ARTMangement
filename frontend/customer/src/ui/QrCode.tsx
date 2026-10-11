import { useMemo } from 'react'
import qr from 'qrcode-generator'

export function QrCode({ value, label }: { value: string; label: string }) {
  const { path, size } = useMemo(() => {
    const code = qr(0, 'M')
    code.addData(value)
    code.make()
    const count = code.getModuleCount()
    let d = ''
    for (let row = 0; row < count; row++) {
      for (let col = 0; col < count; col++) {
        if (code.isDark(row, col)) d += `M${col} ${row}h1v1h-1z`
      }
    }
    // The quiet zone the spec requires, in modules, on all four sides.
    const quiet = 4
    return { path: d, size: count + quiet * 2 }
  }, [value])

  return (
    <svg
      viewBox={`-4 -4 ${size} ${size}`}
      role="img"
      aria-label={label}
      shapeRendering="crispEdges"
      className="h-full w-full fill-ink"
    >
      <path d={path} />
    </svg>
  )
}
