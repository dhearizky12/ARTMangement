import type { components } from './schema'

export type ErrorEnvelope = components['schemas']['Error']

export const ERROR_CODES = {
  unauthenticated: 'unauthenticated',
  sessionExpired: 'session_expired',
  notFound: 'not_found',
  network: 'network_unreachable',
  malformed: 'malformed_response',
} as const

export class ApiError extends Error {
  readonly status: number
  readonly code: string
  readonly message: string
  readonly field?: string
  readonly details?: Record<string, unknown>

  constructor(init: {
    status: number
    code: string
    message: string
    field?: string
    details?: Record<string, unknown>
  }) {
    super(init.message)
    this.name = 'ApiError'
    this.status = init.status
    this.code = init.code
    this.message = init.message
    this.field = init.field
    this.details = init.details
  }

  get isAuthFailure(): boolean {
    return (
      this.status === 401 ||
      this.code === ERROR_CODES.unauthenticated ||
      this.code === ERROR_CODES.sessionExpired
    )
  }

  get isNotFound(): boolean {
    return this.status === 404
  }

  get isTransportFailure(): boolean {
    return this.code === ERROR_CODES.network || this.code === ERROR_CODES.malformed
  }
}

const CLIENT_MESSAGES = {
  network: 'Tidak dapat terhubung. Periksa koneksi internet Anda.',
  malformed: 'Terjadi gangguan pada server. Coba lagi sebentar lagi.',
} as const

export function networkError(cause?: unknown): ApiError {
  return new ApiError({
    status: 0,
    code: ERROR_CODES.network,
    message: CLIENT_MESSAGES.network,
    details: cause instanceof Error ? { cause: cause.message } : undefined,
  })
}

export function malformedError(status: number, detail?: string): ApiError {
  return new ApiError({
    status,
    code: ERROR_CODES.malformed,
    message: CLIENT_MESSAGES.malformed,
    details: detail ? { detail } : undefined,
  })
}

export async function toApiError(response: Response): Promise<ApiError> {
  let body: unknown
  try {
    body = await response.json()
  } catch {
    return malformedError(response.status, 'response body was not JSON')
  }

  const envelope = (body as ErrorEnvelope | undefined)?.error
  if (!envelope || typeof envelope.code !== 'string' || typeof envelope.message !== 'string') {
    return malformedError(response.status, 'response body was not the error envelope')
  }

  return new ApiError({
    status: response.status,
    code: envelope.code,
    message: envelope.message,
    field: envelope.field,
    details: envelope.details as Record<string, unknown> | undefined,
  })
}
