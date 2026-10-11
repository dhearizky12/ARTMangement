// Mount path the app is served under, read from <base href> in index.html.
// Normalised to a leading slash and no trailing slash; "/" becomes "".
function readBaseHref(): string {
  const el = document.querySelector('base')
  if (!el) return ''

  // getAttribute, because el.href is resolved to an absolute URL
  const raw = el.getAttribute('href') ?? '/'
  const trimmed = raw.replace(/\/+$/, '')
  if (!trimmed) return ''
  return trimmed.startsWith('/') ? trimmed : `/${trimmed}`
}

export const basePath = readBaseHref()

export function apiUrl(path: string): string {
  const p = path.startsWith('/') ? path : `/${path}`
  return `${basePath}/api${p}`
}

export function publicUrl(path: string): string {
  const p = path.startsWith('/') ? path : `/${path}`
  return `${basePath}${p}`
}
