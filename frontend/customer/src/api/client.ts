import { agent } from '../agent/current'
import { apiUrl } from '../lib/basePath'
import { ApiError, networkError, malformedError, toApiError } from './errors'

let bearerToken: string | null = null

export function setAuthToken(token: string | null): void {
  bearerToken = token
}

export function getAuthToken(): string | null {
  return bearerToken
}

type AuthFailureHandler = (error: ApiError) => void
let onAuthFailure: AuthFailureHandler | null = null

// A callback, so the client doesn't depend on the session
export function setAuthFailureHandler(handler: AuthFailureHandler | null): void {
  onAuthFailure = handler
}

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  query?: Record<string, string | number | boolean | undefined | null>
  signal?: AbortSignal
}

function buildUrl(path: string, query?: RequestOptions['query']): string {
  const url = apiUrl(path)
  if (!query) return url
  const params = new URLSearchParams()
  for (const [k, v] of Object.entries(query)) {
    if (v !== undefined && v !== null && v !== '') params.set(k, String(v))
  }
  const qs = params.toString()
  return qs ? `${url}?${qs}` : url
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, signal } = options

  const headers: Record<string, string> = {
    'X-Agency': agent.slug,
    Accept: 'application/json',
  }
  // FormData sets its own multipart boundary, so it gets no Content-Type
  const isForm = typeof FormData !== 'undefined' && body instanceof FormData
  if (body !== undefined && !isForm) headers['Content-Type'] = 'application/json'
  if (bearerToken) headers.Authorization = `Bearer ${bearerToken}`

  let response: Response
  try {
    response = await fetch(buildUrl(path, query), {
      method,
      headers,
      body: body === undefined ? undefined : isForm ? (body as FormData) : JSON.stringify(body),
      signal,
    })
  } catch (cause) {
    // A cancelled request isn't a failure; callers ignore it by name
    if (cause instanceof DOMException && cause.name === 'AbortError') throw cause
    throw networkError(cause)
  }

  if (!response.ok) {
    const error = await toApiError(response)
    if (error.isAuthFailure) onAuthFailure?.(error)
    throw error
  }

  if (response.status === 204) return undefined as T

  try {
    return (await response.json()) as T
  } catch {
    throw malformedError(response.status, 'success body was not JSON')
  }
}

export function isAbort(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}
