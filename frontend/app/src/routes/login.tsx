import { createFileRoute, useNavigate, redirect } from '@tanstack/react-router'
import { useState } from 'react'
import { ShieldCheck } from 'lucide-react'
import { api, ApiError } from '../lib/api'
import { setAuth, getToken } from '../lib/auth'
import { Button, Input } from '../components/ui'

export const Route = createFileRoute('/login')({
  beforeLoad: () => {
    if (getToken()) throw redirect({ to: '/' })
  },
  component: Login,
})

function Login() {
  const nav = useNavigate()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [err, setErr] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function submit(e: React.FormEvent) {
    e.preventDefault()
    setErr(null)
    setBusy(true)
    try {
      const res = await api.login(username, password)
      setAuth(res.token, res.role, res.expires)
      nav({ to: '/' })
    } catch (ex) {
      const m = ex instanceof ApiError
        ? (ex.status === 0 ? ex.message : 'Invalid username or password')
        : 'Login failed'
      setErr(m)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-[var(--color-bg)] p-4">
      {/* soft accent glow backdrop */}
      <div className="pointer-events-none absolute inset-0" style={{
        background: 'radial-gradient(60rem 40rem at 50% -10%, var(--color-accent-so), transparent 70%)',
      }} />
      <div className="enter relative w-full max-w-sm">
        <div className="mb-6 flex flex-col items-center text-center">
          <div className="mb-3 flex h-12 w-12 items-center justify-center rounded-[var(--radius-md)] bg-[var(--color-accent)] text-white card-shadow-lg">
            <ShieldCheck className="h-6 w-6" />
          </div>
          <h1 className="text-xl font-bold text-[var(--color-ink)]">AccessCtrl</h1>
          <p className="mt-1 text-sm text-[var(--color-ink-3)]">Sign in to the access control panel</p>
        </div>

        <div className="rounded-[var(--radius-lg)] border border-[var(--color-line)] bg-[var(--color-surface)] p-6 card-shadow-lg">
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Username</label>
              <Input value={username} onChange={(e) => setUsername(e.target.value)}
                placeholder="admin" autoComplete="username" autoFocus required />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Password</label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••" autoComplete="current-password" required />
            </div>

            {err && (
              <div className="rounded-[var(--radius-sm)] border border-[var(--color-bad)]/30 bg-[var(--color-bad-so)] px-3 py-2 text-sm text-[var(--color-bad)]">
                {err}
              </div>
            )}

            <Button type="submit" variant="primary" disabled={busy} className="w-full py-2.5">
              {busy ? 'Signing in…' : 'Sign in'}
            </Button>
          </form>

          <div className="mt-4 rounded-[var(--radius-sm)] border border-dashed border-[var(--color-line-2)] bg-[var(--color-surface-2)] px-3 py-2 text-center text-xs text-[var(--color-ink-3)]">
            Demo · <span className="font-mono text-[var(--color-ink-2)]">admin</span> / <span className="font-mono text-[var(--color-ink-2)]">admin123</span>
          </div>
        </div>
      </div>
    </div>
  )
}
