// Light SaaS UI primitives. Soft cards, neutral grays, indigo accent.
import { useEffect, type ReactNode, type ButtonHTMLAttributes, type InputHTMLAttributes } from 'react'
import { Search, X, AlertTriangle } from 'lucide-react'
import { cn } from '../lib/utils'

export type Signal = 'green' | 'amber' | 'red' | 'cyan' | 'dim'

const dot: Record<Signal, string> = {
  green: 'bg-[var(--color-ok)] text-[var(--color-ok)]',
  amber: 'bg-[var(--color-warn)] text-[var(--color-warn)]',
  red: 'bg-[var(--color-bad)] text-[var(--color-bad)]',
  cyan: 'bg-[var(--color-info)] text-[var(--color-info)]',
  dim: 'bg-[var(--color-ink-3)] text-[var(--color-ink-3)]',
}
const text: Record<Signal, string> = {
  green: 'text-[var(--color-ok)]',
  amber: 'text-[var(--color-warn)]',
  red: 'text-[var(--color-bad)]',
  cyan: 'text-[var(--color-info)]',
  dim: 'text-[var(--color-ink-3)]',
}

// ─── StatusLED ───────────────────────────────────────────────────────────────
export function StatusLED({ signal, pulse, label }: { signal: Signal; pulse?: boolean; label?: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn('relative inline-block h-2 w-2 rounded-full', dot[signal])}>
        {pulse && <span className="absolute inset-0 rounded-full" style={{ animation: 'pulseRing 1.8s ease-out infinite' }} />}
      </span>
      {label && <span className={cn('text-xs font-medium', text[signal])}>{label}</span>}
    </span>
  )
}

// ─── Panel ───────────────────────────────────────────────────────────────────
export function Panel({
  title, subtitle, right, children, className,
}: { title?: string; subtitle?: string; right?: ReactNode; children: ReactNode; className?: string }) {
  return (
    <section className={cn('rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] card-shadow', className)}>
      {(title || right) && (
        <header className="flex items-center justify-between gap-3 border-b border-[var(--color-line)] px-5 py-4">
          <div>
            {title && <h2 className="text-base font-semibold text-[var(--color-ink)]">{title}</h2>}
            {subtitle && <p className="mt-0.5 text-sm text-[var(--color-ink-3)]">{subtitle}</p>}
          </div>
          {right}
        </header>
      )}
      <div className="p-5">{children}</div>
    </section>
  )
}

// ─── StatCard ────────────────────────────────────────────────────────────────
export function StatCard({
  label, value, signal = 'green', unit, foot,
}: { label: string; value: ReactNode; signal?: Signal; unit?: string; foot?: ReactNode }) {
  const ring: Record<Signal, string> = {
    green: 'bg-[var(--color-ok-so)] text-[var(--color-ok)]',
    amber: 'bg-[var(--color-warn-so)] text-[var(--color-warn)]',
    red: 'bg-[var(--color-bad-so)] text-[var(--color-bad)]',
    cyan: 'bg-[var(--color-accent-so)] text-[var(--color-info)]',
    dim: 'bg-[var(--color-surface-2)] text-[var(--color-ink-3)]',
  }
  return (
    <div className="enter rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-5 card-shadow">
      <div className="flex items-start justify-between">
        <span className="label">{label}</span>
        <span className={cn('inline-block h-2 w-2 rounded-full', dot[signal])} />
      </div>
      <div className="mt-3 flex items-baseline gap-1.5">
        <span className="text-3xl font-bold tabular-nums leading-none text-[var(--color-ink)]">{value}</span>
        {unit && <span className="text-sm text-[var(--color-ink-3)]">{unit}</span>}
      </div>
      {foot && <div className={cn('mt-3 inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium', ring[signal])}>{foot}</div>}
    </div>
  )
}

// ─── Button ──────────────────────────────────────────────────────────────────
interface BtnProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'ghost' | 'danger'
}
export function Button({ variant = 'ghost', className, children, ...rest }: BtnProps) {
  const variants = {
    primary: 'bg-[var(--color-accent)] text-white hover:bg-[var(--color-accent-d)] border-transparent',
    ghost: 'bg-[var(--color-surface)] text-[var(--color-ink-2)] border-[var(--color-line-2)] hover:bg-[var(--color-surface-2)]',
    danger: 'bg-[var(--color-surface)] text-[var(--color-bad)] border-[var(--color-line-2)] hover:bg-[var(--color-bad-so)] hover:border-[var(--color-bad)]/40',
  }
  return (
    <button
      className={cn(
        'inline-flex items-center justify-center gap-1.5 rounded-[var(--radius-sm)] border px-3 py-1.5 text-sm font-medium transition-colors disabled:cursor-not-allowed disabled:opacity-50',
        variants[variant], className,
      )}
      {...rest}
    >
      {children}
    </button>
  )
}

// ─── Input ───────────────────────────────────────────────────────────────────
export function Input({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <input
      className={cn(
        'w-full rounded-[var(--radius-sm)] border border-[var(--color-line-2)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-ink)] outline-none transition-shadow placeholder:text-[var(--color-ink-3)] focus:border-[var(--color-accent)] focus:ring-2 focus:ring-[var(--color-accent-so)]',
        className,
      )}
      {...rest}
    />
  )
}

// ─── Select (styled native) ────────────────────────────────────────────────────
export function Select({ className, children, ...rest }: React.SelectHTMLAttributes<HTMLSelectElement>) {
  return (
    <select
      className={cn(
        'w-full rounded-[var(--radius-sm)] border border-[var(--color-line-2)] bg-[var(--color-surface)] px-3 py-2 text-sm text-[var(--color-ink)] outline-none focus:border-[var(--color-accent)] focus:ring-2 focus:ring-[var(--color-accent-so)]',
        className,
      )}
      {...rest}
    >
      {children}
    </select>
  )
}

// ─── Badge ───────────────────────────────────────────────────────────────────
export function Badge({ signal, children }: { signal: Signal; children: ReactNode }) {
  const styles: Record<Signal, string> = {
    green: 'bg-[var(--color-ok-so)] text-[var(--color-ok)]',
    amber: 'bg-[var(--color-warn-so)] text-[var(--color-warn)]',
    red: 'bg-[var(--color-bad-so)] text-[var(--color-bad)]',
    cyan: 'bg-[var(--color-accent-so)] text-[var(--color-accent-d)]',
    dim: 'bg-[var(--color-surface-2)] text-[var(--color-ink-2)]',
  }
  return (
    <span className={cn('inline-flex items-center rounded-full px-2 py-0.5 text-xs font-semibold', styles[signal])}>
      {children}
    </span>
  )
}

// ─── Table shells ──────────────────────────────────────────────────────────────
export function Table({ children }: { children: ReactNode }) {
  return <table className="w-full border-collapse text-sm">{children}</table>
}
export function Th({ children, className }: { children?: ReactNode; className?: string }) {
  return (
    <th className={cn('border-b border-[var(--color-line)] px-3 py-2.5 text-left text-xs font-semibold uppercase tracking-wide text-[var(--color-ink-3)]', className)}>
      {children}
    </th>
  )
}
export function Td({ children, className }: { children?: ReactNode; className?: string }) {
  return <td className={cn('border-b border-[var(--color-line)] px-3 py-3 text-[var(--color-ink-2)]', className)}>{children}</td>
}

// ─── State line ────────────────────────────────────────────────────────────────
export function StateLine({ kind, msg }: { kind: 'loading' | 'empty' | 'error'; msg: string }) {
  const sig: Signal = kind === 'error' ? 'red' : kind === 'loading' ? 'amber' : 'dim'
  return (
    <div className="flex items-center gap-2 px-3 py-8 text-sm text-[var(--color-ink-3)]">
      <StatusLED signal={sig} pulse={kind === 'loading'} />
      <span>{msg}</span>
    </div>
  )
}

// ─── Search input (icon-prefixed) ────────────────────────────────────────────────
export function SearchInput({ className, ...rest }: InputHTMLAttributes<HTMLInputElement>) {
  return (
    <div className={cn('relative', className)}>
      <Search className="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--color-ink-3)]" />
      <input
        {...rest}
        className="w-full rounded-[var(--radius-sm)] border border-[var(--color-line-2)] bg-[var(--color-surface)] py-2 pl-9 pr-3 text-sm text-[var(--color-ink)] outline-none transition-shadow placeholder:text-[var(--color-ink-3)] focus:border-[var(--color-accent)] focus:ring-2 focus:ring-[var(--color-accent-so)]"
      />
    </div>
  )
}

// ─── Skeleton (loading placeholder) ──────────────────────────────────────────────
export function Skeleton({ className }: { className?: string }) {
  return <div className={cn('skeleton rounded-[var(--radius-sm)]', className)} />
}

export function TableSkeleton({ rows = 5, cols = 4 }: { rows?: number; cols?: number }) {
  return (
    <div className="space-y-2 py-2">
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="flex gap-3">
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} className={cn('h-5 flex-1', c === 0 && 'max-w-[7rem]')} />
          ))}
        </div>
      ))}
    </div>
  )
}

// ─── Modal ───────────────────────────────────────────────────────────────────────
export function Modal({
  open, onClose, title, children, footer,
}: { open: boolean; onClose: () => void; title?: string; children: ReactNode; footer?: ReactNode }) {
  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose() }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null
  return (
    <div className="fixed inset-0 z-[90] flex items-center justify-center p-4">
      <div className="modal-overlay absolute inset-0 bg-black/40" onClick={onClose} />
      <div role="dialog" aria-modal="true"
        className="modal-in relative w-full max-w-md rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] card-shadow-lg">
        {title && (
          <div className="flex items-center justify-between border-b border-[var(--color-line)] px-5 py-4">
            <h2 className="text-base font-semibold text-[var(--color-ink)]">{title}</h2>
            <button onClick={onClose} aria-label="Close"
              className="text-[var(--color-ink-3)] transition-colors hover:text-[var(--color-ink)]">
              <X className="h-4 w-4" />
            </button>
          </div>
        )}
        <div className="px-5 py-4 text-sm text-[var(--color-ink-2)]">{children}</div>
        {footer && <div className="flex justify-end gap-2 border-t border-[var(--color-line)] px-5 py-4">{footer}</div>}
      </div>
    </div>
  )
}

// ─── Confirm dialog ────────────────────────────────────────────────────────────────
export function ConfirmDialog({
  open, onConfirm, onCancel, title = 'Are you sure?', message, confirmLabel = 'Confirm', danger = true, busy = false,
}: {
  open: boolean; onConfirm: () => void; onCancel: () => void
  title?: string; message: ReactNode; confirmLabel?: string; danger?: boolean; busy?: boolean
}) {
  return (
    <Modal open={open} onClose={onCancel} title={title}
      footer={
        <>
          <Button variant="ghost" onClick={onCancel} disabled={busy}>Cancel</Button>
          <Button variant={danger ? 'danger' : 'primary'} onClick={onConfirm} disabled={busy}>
            {busy ? 'Working…' : confirmLabel}
          </Button>
        </>
      }>
      <div className="flex items-start gap-3">
        {danger && <AlertTriangle className="mt-0.5 h-5 w-5 shrink-0 text-[var(--color-bad)]" />}
        <div>{message}</div>
      </div>
    </Modal>
  )
}
