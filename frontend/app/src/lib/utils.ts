import { clsx, type ClassValue } from 'clsx'
import { twMerge } from 'tailwind-merge'
import { formatDistanceToNow, format } from 'date-fns'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}

export function timeAgo(iso: string | null): string {
  if (!iso) return 'never'
  try { return formatDistanceToNow(new Date(iso), { addSuffix: true }) }
  catch { return '—' }
}

export function clock(iso: string): string {
  try { return format(new Date(iso), 'HH:mm:ss') }
  catch { return '--:--:--' }
}

export function stamp(iso: string): string {
  try { return format(new Date(iso), 'yyyy-MM-dd HH:mm:ss') }
  catch { return '—' }
}
