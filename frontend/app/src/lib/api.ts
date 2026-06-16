// Typed fetch wrapper. Injects JWT, handles 401 → logout.
import { getToken, clearAuth } from './auth'
import type {
  LoginResponse, DeviceDto, DeviceStatusDto, AccessLogDto, UserDto, RfidCardDto,
  CreateDeviceRequest, CreateUserRequest, UpdateUserRequest, RegisterCardRequest,
} from './types'

export const API_URL = import.meta.env.VITE_API_URL ?? 'https://localhost:7114'

export class ApiError extends Error {
  status: number
  constructor(status: number, message: string) {
    super(message)
    this.status = status
  }
}

async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  const token = getToken()
  const headers = new Headers(init.headers)
  headers.set('Content-Type', 'application/json')
  if (token) headers.set('Authorization', `Bearer ${token}`)

  let res: Response
  try {
    res = await fetch(`${API_URL}${path}`, { ...init, headers })
  } catch {
    throw new ApiError(0, 'NETWORK_DOWN — backend unreachable')
  }

  if (res.status === 401) {
    clearAuth()
    throw new ApiError(401, 'UNAUTHORIZED — session expired')
  }
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    // Backend errors come as { "error": "..." } — surface the message, not raw JSON.
    let msg = text || `Error ${res.status}`
    try {
      const j = JSON.parse(text)
      if (j?.error) msg = j.error
    } catch { /* not JSON — keep raw text */ }
    throw new ApiError(res.status, msg)
  }
  if (res.status === 204) return undefined as T
  const ct = res.headers.get('content-type') ?? ''
  if (!ct.includes('application/json')) return undefined as T
  return res.json() as Promise<T>
}

// List endpoints: always resolve to an array. If the backend returns a non-array
// (error object, paged wrapper, null) the UI shows empty instead of crashing on .filter/.map.
async function requestList<T>(path: string): Promise<T[]> {
  const data = await request<unknown>(path)
  return Array.isArray(data) ? (data as T[]) : []
}

export const api = {
  // auth
  login: (username: string, password: string) =>
    request<LoginResponse>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password }),
    }),

  // devices
  devices: () => requestList<DeviceDto>('/api/devices'),
  deviceStatus: (id: number) => request<DeviceStatusDto>(`/api/devices/${id}/status`),
  createDevice: (b: CreateDeviceRequest) =>
    request<DeviceDto>('/api/devices', { method: 'POST', body: JSON.stringify(b) }),
  deleteDevice: (id: number) =>
    request<void>(`/api/devices/${id}`, { method: 'DELETE' }),

  // access logs
  accessLogs: () => requestList<AccessLogDto>('/api/access-logs'),

  // access logs — employee self-history
  myAccessLogs: () => requestList<AccessLogDto>('/api/access-logs/mine'),

  // users
  users: () => requestList<UserDto>('/api/users'),
  createUser: (b: CreateUserRequest) =>
    request<UserDto>('/api/users', { method: 'POST', body: JSON.stringify(b) }),
  createEmployee: (b: CreateUserRequest) =>
    request<UserDto>('/api/users/employees', { method: 'POST', body: JSON.stringify(b) }),
  updateUser: (id: number, b: UpdateUserRequest) =>
    request<UserDto>(`/api/users/${id}`, { method: 'PUT', body: JSON.stringify(b) }),
  resetPassword: (id: number, newPassword: string) =>
    request<void>(`/api/users/${id}/password`, { method: 'PUT', body: JSON.stringify({ newPassword }) }),
  changePassword: (currentPassword: string, newPassword: string) =>
    request<void>('/api/auth/change-password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) }),
  me: () => request<UserDto>('/api/auth/me'),
  updateMe: (fullName: string) =>
    request<UserDto>('/api/auth/me', { method: 'PUT', body: JSON.stringify({ fullName }) }),
  deleteUser: (id: number) =>
    request<void>(`/api/users/${id}`, { method: 'DELETE' }),

  // cards
  cards: () => requestList<RfidCardDto>('/api/cards'),
  unassignedCards: () => requestList<RfidCardDto>('/api/cards/unassigned'),
  registerCard: (b: RegisterCardRequest) =>
    request<RfidCardDto>('/api/cards', { method: 'POST', body: JSON.stringify(b) }),
  assignCard: (id: number, userId: number) =>
    request<RfidCardDto>(`/api/cards/${id}/assign`, { method: 'PUT', body: JSON.stringify({ userId }) }),
  deactivateCard: (id: number) =>
    request<void>(`/api/cards/${id}`, { method: 'DELETE' }),

  // emergency door command
  emergency: (deviceId: number, action: 'lock' | 'unlock', password: string) =>
    request<{ sent: boolean; device: string; action: string }>(
      `/api/devices/${deviceId}/emergency`,
      { method: 'POST', body: JSON.stringify({ action, password }) }),
}
