// JWT token store — localStorage + lightweight pub/sub for React.
import { useSyncExternalStore } from 'react'

const TOKEN_KEY = 'iot.jwt'
const ROLE_KEY = 'iot.role'
const EXP_KEY = 'iot.exp'

type Listener = () => void
const listeners = new Set<Listener>()
function emit() { listeners.forEach((l) => l()) }

export function setAuth(token: string, role: string, expires: string) {
  localStorage.setItem(TOKEN_KEY, token)
  localStorage.setItem(ROLE_KEY, role)
  localStorage.setItem(EXP_KEY, expires)
  emit()
}

export function clearAuth() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(ROLE_KEY)
  localStorage.removeItem(EXP_KEY)
  emit()
}

export function getToken(): string | null {
  const token = localStorage.getItem(TOKEN_KEY)
  if (!token) return null
  const exp = localStorage.getItem(EXP_KEY)
  // expire client-side if past expiry
  if (exp && new Date(exp).getTime() < Date.now()) {
    clearAuth()
    return null
  }
  return token
}

export function getRole(): string | null {
  return localStorage.getItem(ROLE_KEY)
}

interface AuthState {
  token: string | null
  role: string | null
  isAuthed: boolean
}

// stable snapshot to satisfy useSyncExternalStore
let cache: AuthState = compute()
function compute(): AuthState {
  const token = getToken()
  return { token, role: getRole(), isAuthed: !!token }
}
function getSnapshot(): AuthState {
  const next = compute()
  if (next.token !== cache.token || next.role !== cache.role) cache = next
  return cache
}
function subscribe(l: Listener) {
  listeners.add(l)
  window.addEventListener('storage', l) // cross-tab
  return () => { listeners.delete(l); window.removeEventListener('storage', l) }
}

export function useAuth(): AuthState {
  return useSyncExternalStore(subscribe, getSnapshot)
}
