import { createRootRoute, Link, Outlet, useNavigate, useRouterState } from '@tanstack/react-router'
import { useEffect } from 'react'
import {
  LayoutDashboard, ScrollText, Cpu, CreditCard, Users, ShieldAlert,
  History, UserCircle, LogOut, Sun, Moon, type LucideIcon,
} from 'lucide-react'
import { useAuth, clearAuth, getRole } from '../lib/auth'
import { useTheme, toggleTheme } from '../lib/theme'
import { StatusLED } from '../components/ui'
import { cn } from '../lib/utils'

export const Route = createRootRoute({ component: RootShell })

// Nav filtered by role. Employee sees only their own history.
const NAV: { to: string; label: string; icon: LucideIcon; roles: readonly string[] }[] = [
  { to: '/', label: 'Overview', icon: LayoutDashboard, roles: ['Admin'] },
  { to: '/logs', label: 'Access Logs', icon: ScrollText, roles: ['Admin'] },
  { to: '/devices', label: 'Devices', icon: Cpu, roles: ['Admin'] },
  { to: '/cards', label: 'Cards', icon: CreditCard, roles: ['Admin'] },
  { to: '/users', label: 'Users', icon: Users, roles: ['Admin'] },
  { to: '/emergency', label: 'Emergency', icon: ShieldAlert, roles: ['Admin'] },
  { to: '/me', label: 'My Access', icon: History, roles: ['Employee', 'Admin'] },
  { to: '/profile', label: 'My Profile', icon: UserCircle, roles: ['Employee', 'Admin'] },
]

function RootShell() {
  const { isAuthed } = useAuth()
  const nav = useNavigate()
  const pathname = useRouterState({ select: (s) => s.location.pathname })
  const role = getRole() ?? ''
  const theme = useTheme()

  useEffect(() => {
    if (!isAuthed && pathname !== '/login') nav({ to: '/login' })
  }, [isAuthed, pathname, nav])

  if (pathname === '/login') return <Outlet />
  if (!isAuthed) return null

  const items = NAV.filter((n) => n.roles.includes(role))

  return (
    <div className="flex min-h-screen">
      {/* ─── Sidebar ─────────────────────────────────────────────── */}
      <aside className="flex w-60 flex-col border-r border-[var(--color-line)] bg-[var(--color-surface)]">
        <div className="flex items-center gap-2.5 border-b border-[var(--color-line)] px-5 py-4">
          <div className="flex h-8 w-8 items-center justify-center rounded-[var(--radius-sm)] bg-[var(--color-accent)] text-sm font-bold text-white">A</div>
          <div>
            <div className="text-sm font-bold text-[var(--color-ink)]">AccessCtrl</div>
            <div className="text-xs text-[var(--color-ink-3)]">IoT Access</div>
          </div>
        </div>

        <nav className="flex-1 space-y-1 p-3">
          {items.map((item) => {
            const active = pathname === item.to
            const Icon = item.icon
            return (
              <Link key={item.to} to={item.to}
                className={cn(
                  'flex items-center gap-2.5 rounded-[var(--radius-sm)] px-3 py-2 text-sm font-medium transition-colors',
                  active
                    ? 'bg-[var(--color-accent-so)] text-[var(--color-accent-d)]'
                    : 'text-[var(--color-ink-2)] hover:bg-[var(--color-surface-2)]',
                )}>
                <Icon className="h-4 w-4 shrink-0" />
                {item.label}
              </Link>
            )
          })}
        </nav>

        <div className="border-t border-[var(--color-line)] p-3">
          <div className="mb-2 flex items-center justify-between px-2">
            <span className="text-xs text-[var(--color-ink-3)]">Role</span>
            <span className="text-xs font-semibold text-[var(--color-accent-d)]">{role || '—'}</span>
          </div>
          <button onClick={() => { clearAuth(); nav({ to: '/login' }) }}
            className="flex w-full items-center justify-center gap-2 rounded-[var(--radius-sm)] border border-[var(--color-line-2)] px-3 py-2 text-sm font-medium text-[var(--color-ink-2)] transition-colors hover:bg-[var(--color-surface-2)]">
            <LogOut className="h-4 w-4" /> Sign out
          </button>
        </div>
      </aside>

      {/* ─── Main ────────────────────────────────────────────────── */}
      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-[var(--color-line)] bg-[var(--color-surface)] px-6 py-3">
          <h1 className="text-sm font-semibold capitalize text-[var(--color-ink)]">
            {pathname === '/' ? 'Overview' : pathname.slice(1).replace('-', ' ')}
          </h1>
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1.5 text-xs text-[var(--color-ink-3)]">
              <StatusLED signal="green" pulse /> System online
            </span>
            <button onClick={toggleTheme}
              aria-label={theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode'}
              className="flex h-8 w-8 items-center justify-center rounded-[var(--radius-sm)] border border-[var(--color-line-2)] text-[var(--color-ink-2)] transition-colors hover:bg-[var(--color-surface-2)]">
              {theme === 'dark' ? <Sun className="h-4 w-4" /> : <Moon className="h-4 w-4" />}
            </button>
          </div>
        </header>

        <main className="flex-1 overflow-y-auto bg-[var(--color-bg)] p-6">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
