// Theme store — light/dark. Persists to localStorage, applies data-theme on <html>.
// Same useSyncExternalStore pattern as auth.ts.
import { useSyncExternalStore } from 'react'

export type Theme = 'light' | 'dark'
const THEME_KEY = 'iot.theme'

type Listener = () => void
const listeners = new Set<Listener>()

function systemPref(): Theme {
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
}

function read(): Theme {
  const saved = localStorage.getItem(THEME_KEY)
  return saved === 'dark' || saved === 'light' ? saved : systemPref()
}

function apply(theme: Theme) {
  document.documentElement.setAttribute('data-theme', theme)
}

// Apply immediately on module load so there's no flash before React mounts.
let current: Theme = read()
apply(current)

export function setTheme(theme: Theme) {
  current = theme
  localStorage.setItem(THEME_KEY, theme)
  apply(theme)
  listeners.forEach((l) => l())
}

export function toggleTheme() {
  setTheme(current === 'dark' ? 'light' : 'dark')
}

function subscribe(l: Listener) {
  listeners.add(l)
  return () => { listeners.delete(l) }
}
function getSnapshot(): Theme {
  return current
}

export function useTheme(): Theme {
  return useSyncExternalStore(subscribe, getSnapshot)
}
