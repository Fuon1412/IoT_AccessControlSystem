// Global toast notifications. Stacked top-right, auto-dismiss, themed.
import { createContext, useCallback, useContext, useMemo, useRef, useState, type ReactNode } from 'react'
import { CheckCircle2, AlertCircle, Info, X } from 'lucide-react'
import { cn } from '../lib/utils'

export type ToastKind = 'success' | 'error' | 'info'
interface Toast { id: number; kind: ToastKind; msg: string }

interface ToastApi {
  push: (kind: ToastKind, msg: string) => void
  success: (msg: string) => void
  error: (msg: string) => void
  info: (msg: string) => void
}

const ToastCtx = createContext<ToastApi | null>(null)

const META: Record<ToastKind, { icon: typeof Info; cls: string }> = {
  success: { icon: CheckCircle2, cls: 'text-[var(--color-ok)]' },
  error: { icon: AlertCircle, cls: 'text-[var(--color-bad)]' },
  info: { icon: Info, cls: 'text-[var(--color-info)]' },
}

export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([])
  const seq = useRef(0)

  const remove = useCallback((id: number) => {
    setToasts((prev) => prev.filter((t) => t.id !== id))
  }, [])

  const push = useCallback((kind: ToastKind, msg: string) => {
    const id = ++seq.current
    setToasts((prev) => [...prev, { id, kind, msg }])
    setTimeout(() => remove(id), kind === 'error' ? 6000 : 3500)
  }, [remove])

  const api = useMemo<ToastApi>(() => ({
    push,
    success: (m) => push('success', m),
    error: (m) => push('error', m),
    info: (m) => push('info', m),
  }), [push])

  return (
    <ToastCtx.Provider value={api}>
      {children}
      <div className="pointer-events-none fixed right-4 top-4 z-[100] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
        {toasts.map((t) => {
          const { icon: Icon, cls } = META[t.kind]
          return (
            <div key={t.id}
              className="toast-in pointer-events-auto flex items-start gap-2.5 rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface)] px-3.5 py-3 text-sm card-shadow-lg">
              <Icon className={cn('mt-0.5 h-4 w-4 shrink-0', cls)} />
              <span className="flex-1 text-[var(--color-ink)]">{t.msg}</span>
              <button onClick={() => remove(t.id)}
                className="shrink-0 text-[var(--color-ink-3)] transition-colors hover:text-[var(--color-ink)]">
                <X className="h-3.5 w-3.5" />
              </button>
            </div>
          )
        })}
      </div>
    </ToastCtx.Provider>
  )
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastCtx)
  if (!ctx) throw new Error('useToast must be used within ToastProvider')
  return ctx
}
