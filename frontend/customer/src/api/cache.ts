
interface Entry {
  value: unknown
  at: number
}

const store = new Map<string, Entry>()

const TTL_MS = 5 * 60 * 1000

export function readCache<T>(key: string): T | undefined {
  const hit = store.get(key)
  if (!hit) return undefined
  if (Date.now() - hit.at > TTL_MS) {
    store.delete(key)
    return undefined
  }
  return hit.value as T
}

export function writeCache(key: string, value: unknown): void {
  store.set(key, { value, at: Date.now() })
}

export function clearCache(): void {
  store.clear()
}
