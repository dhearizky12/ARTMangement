import { request } from './client'
import type { components, operations } from './schema'

type S = components['schemas']

export type Agency = S['Agency']
export type Reference = S['Reference']
export type Helper = S['Helper']
export type HelperDetail = S['HelperDetail']
export type Review = S['Review']
export type User = S['User']
export type Gender = S['Gender']
export type Address = S['Address']
export type AddressInput = S['AddressInput']
export type Quote = S['Quote']
export type QuoteInput = S['QuoteInput']
export type Voucher = S['Voucher']
export type Order = S['Order']
export type OrderInput = S['OrderInput']
export type OrderReview = S['OrderReview']
export type ReviewInput = S['ReviewInput']
export type Payment = S['Payment']
export type OrderStatus = S['OrderStatus']
export type AvatarTone = S['AvatarTone']
export type Schedule = S['Schedule']

export interface Page<T> {
  items: T[]
  total: number
  limit: number
  offset: number
}

interface Signal {
  signal?: AbortSignal
}

export const getAgency = (o: Signal = {}) => request<Agency>('/agency', o)

export const getReference = (o: Signal = {}) => request<Reference>('/reference', o)

export type HelperQuery = NonNullable<operations['listHelpers']['parameters']['query']>

export const listHelpers = (query: HelperQuery = {}, o: Signal = {}) =>
  request<Page<Helper>>('/helpers', { ...o, query })

export const getHelper = (helperId: string, o: Signal = {}) =>
  request<HelperDetail>(`/helpers/${encodeURIComponent(helperId)}`, o)

export const listHelperReviews = (
  helperId: string,
  query: { limit?: number; offset?: number } = {},
  o: Signal = {},
) => request<Page<Review>>(`/helpers/${encodeURIComponent(helperId)}/reviews`, { ...o, query })

export type FavoriteState = S['FavoriteState']

// Both verbs return the state after the call, so a double tap ends the same way
export const saveHelper = (helperId: string, o: Signal = {}) =>
  request<FavoriteState>(`/helpers/${encodeURIComponent(helperId)}/favorite`, {
    ...o,
    method: 'PUT',
  })

export const unsaveHelper = (helperId: string, o: Signal = {}) =>
  request<FavoriteState>(`/helpers/${encodeURIComponent(helperId)}/favorite`, {
    ...o,
    method: 'DELETE',
  })

export interface OtpChallenge {
  expiresIn: number
  resendAfter: number
}

export const requestOtp = (body: { phone: string; name?: string | null }, o: Signal = {}) =>
  request<OtpChallenge>('/auth/otp', { ...o, method: 'POST', body })

export interface Session {
  token: string
  expiresAt: string
  user: User
  isNewAccount: boolean
}

export const verifyOtp = (
  body: { phone: string; code: string; name?: string | null },
  o: Signal = {},
) => request<Session>('/auth/verify', { ...o, method: 'POST', body })

export const logout = (o: Signal = {}) =>
  request<void>('/auth/logout', { ...o, method: 'POST' })

export const getMe = (o: Signal = {}) => request<User>('/me', o)

export const updateMe = (
  body: {
    name?: string
    phone?: string
    email?: string
    birthDate?: string | null
    gender?: Gender | null
    photoUrl?: string | null
  },
  o: Signal = {},
) => request<User>('/me', { ...o, method: 'PATCH', body })

export const listAddresses = (o: Signal = {}) => request<Page<Address>>('/addresses', o)

export const createAddress = (body: AddressInput, o: Signal = {}) =>
  request<Address>('/addresses', { ...o, method: 'POST', body })

export const updateAddress = (
  addressId: string,
  body: Partial<AddressInput>,
  o: Signal = {},
) => request<Address>(`/addresses/${encodeURIComponent(addressId)}`, { ...o, method: 'PATCH', body })

export const deleteAddress = (addressId: string, o: Signal = {}) =>
  request<void>(`/addresses/${encodeURIComponent(addressId)}`, { ...o, method: 'DELETE' })

export const makeAddressPrimary = (addressId: string, o: Signal = {}) =>
  request<Address>(`/addresses/${encodeURIComponent(addressId)}/primary`, {
    ...o,
    method: 'POST',
  })

export const quoteBooking = (body: QuoteInput, o: Signal = {}) =>
  request<Quote>('/bookings/quote', { ...o, method: 'POST', body })

export const listVouchers = (
  query: { helperId?: string; categoryId?: string } = {},
  o: Signal = {},
) => request<Page<Voucher>>('/vouchers', { ...o, query })

export const validateVoucher = (
  body: { code: string; helperId?: string; categoryId?: string },
  o: Signal = {},
) => request<Voucher>('/vouchers/validate', { ...o, method: 'POST', body })

export const listOrders = (query: { status?: string } = {}, o: Signal = {}) =>
  request<Page<Order>>('/orders', { ...o, query })

export interface CreatedOrder {
  order: Order
  payment: Payment
}

export const createOrder = (body: OrderInput, o: Signal = {}) =>
  request<CreatedOrder>('/orders', { ...o, method: 'POST', body })

export const getOrder = (orderId: string, o: Signal = {}) =>
  request<Order>(`/orders/${encodeURIComponent(orderId)}`, o)

export const cancelOrder = (orderId: string, body: { reason: string }, o: Signal = {}) =>
  request<Order>(`/orders/${encodeURIComponent(orderId)}/cancel`, {
    ...o,
    method: 'POST',
    body,
  })

export const createReview = (orderId: string, body: ReviewInput, o: Signal = {}) =>
  request<OrderReview>(`/orders/${encodeURIComponent(orderId)}/review`, {
    ...o,
    method: 'POST',
    body,
  })

export const updateReview = (orderId: string, body: ReviewInput, o: Signal = {}) =>
  request<OrderReview>(`/orders/${encodeURIComponent(orderId)}/review`, {
    ...o,
    method: 'PATCH',
    body,
  })

export const getPayment = (paymentId: string, o: Signal = {}) =>
  request<Payment>(`/payments/${encodeURIComponent(paymentId)}`, o)

export const createUpload = (file: File, o: Signal = {}) => {
  const form = new FormData()
  form.append('file', file)
  return request<{ url: string }>('/uploads', { ...o, method: 'POST', body: form })
}
