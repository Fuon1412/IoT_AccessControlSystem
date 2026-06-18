// Mirror of backend DTOs (IoTAccessAPI/DTOs).

export interface LoginResponse {
  token: string
  expires: string
  role: string
}

export type DoorState = 'open' | 'closed' | 'unknown'

export interface DeviceDto {
  id: number
  name: string
  macAddress: string
  location: string
  isActive: boolean
  lastHeartbeat: string | null
  doorState: DoorState
  lastDoorStateChange: string | null
  createdAt: string
}

// SignalR "DoorStateChanged" payload — servo open/close event from firmware.
export interface DoorStateEvent {
  deviceId: number
  deviceName: string
  doorState: 'open' | 'closed'
  timestamp: string
}

export interface DeviceStatusDto {
  id: number
  name: string
  lastHeartbeat: string | null
  isOnline: boolean
  status: 'online' | 'offline' | 'never_seen' | 'decommissioned'
}

export interface AccessLogDto {
  id: number
  requestId: string
  rfidUid: string
  accessGranted: boolean
  denyReason: string | null
  timestamp: string
  deviceId: number
  deviceName: string
  userId: number | null
  username: string | null
}

export type EventType = 'door' | 'connectivity' | 'emergency'

export interface EventLogDto {
  id: number
  eventType: string   // door | connectivity | emergency
  detail: string      // open|closed · online|offline · lock|unlock
  actor: string | null
  timestamp: string
  deviceId: number
  deviceName: string
}

export interface UserDto {
  id: number
  username: string
  fullName: string
  role: string
  isActive: boolean
  createdAt: string
}

export interface RfidCardDto {
  id: number
  uid: string
  isActive: boolean
  isAssigned: boolean
  registeredAt: string
  userId: number | null
  username: string | null
}

// request payloads
export interface CreateDeviceRequest { name: string; macAddress: string; location: string }
export interface CreateUserRequest { username: string; password: string; fullName?: string; role: string; rfidUid?: string }
export interface UpdateUserRequest { username?: string; fullName?: string; role?: string; isActive?: boolean }
export interface RegisterCardRequest { uid: string; userId: number }
