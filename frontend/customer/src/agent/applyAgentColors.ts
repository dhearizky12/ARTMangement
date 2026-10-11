// Derives the --pri ramp from the agency colour and writes it to :root.

type Hsl = [h: number, s: number, l: number]

export function toHsl(hex: string): Hsl {
  const h6 = hex.replace('#', '')
  const r = parseInt(h6.substring(0, 2), 16) / 255
  const g = parseInt(h6.substring(2, 4), 16) / 255
  const b = parseInt(h6.substring(4, 6), 16) / 255
  const mx = Math.max(r, g, b)
  const mn = Math.min(r, g, b)
  let h = 0
  let s = 0
  const l = (mx + mn) / 2
  if (mx !== mn) {
    const d = mx - mn
    s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn)
    if (mx === r) h = (g - b) / d + (g < b ? 6 : 0)
    else if (mx === g) h = (b - r) / d + 2
    else h = (r - g) / d + 4
    h *= 60
  }
  return [h, s, l]
}

export function toHex(hDeg: number, s: number, l: number): string {
  const h = hDeg / 360
  const f = (p: number, q: number, t: number): number => {
    if (t < 0) t += 1
    if (t > 1) t -= 1
    if (t < 1 / 6) return p + (q - p) * 6 * t
    if (t < 1 / 2) return q
    if (t < 2 / 3) return p + (q - p) * (2 / 3 - t) * 6
    return p
  }
  let r: number
  let g: number
  let b: number
  if (s === 0) {
    r = g = b = l
  } else {
    const q = l < 0.5 ? l * (1 + s) : l + s - l * s
    const p = 2 * l - q
    r = f(p, q, h + 1 / 3)
    g = f(p, q, h)
    b = f(p, q, h - 1 / 3)
  }
  const to = (x: number) => `0${Math.round(x * 255).toString(16)}`.slice(-2)
  return `#${to(r)}${to(g)}${to(b)}`
}

export function applyAgentColors(color: string, root: HTMLElement = document.documentElement): void {
  const [h, s, l] = toHsl(color)
  root.style.setProperty('--pri', color)
  root.style.setProperty('--pri-dark', toHex(h, s, Math.max(l - 0.08, 0.05)))
  root.style.setProperty('--pri-tint', toHex(h, Math.min(s * 0.55, 0.4), 0.93))
  root.style.setProperty('--pri-border', toHex(h, Math.min(s * 0.55, 0.45), 0.85))
  root.style.setProperty('--pri-soft', toHex(h, s, Math.min(l, 0.32)))
}
