import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { api } from '../lib/api'
import { Panel, Input, Button, Badge, StateLine, type Signal } from '../components/ui'
import { useToast } from '../components/toast'

export const Route = createFileRoute('/profile')({ component: Profile })

function roleSignal(role: string): Signal {
  if (role === 'Admin') return 'amber'
  if (role === 'Device') return 'cyan'
  if (role === 'Employee') return 'green'
  return 'dim'
}

function Profile() {
  const qc = useQueryClient()
  const toast = useToast()
  const me = useQuery({ queryKey: ['me-profile'], queryFn: api.me })
  const [fullName, setFullName] = useState('')

  useEffect(() => { if (me.data) setFullName(me.data.fullName) }, [me.data])

  const saveProfile = useMutation({
    mutationFn: () => api.updateMe(fullName),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['me-profile'] }); toast.success('Profile saved') },
    onError: (e: Error) => toast.error(e.message),
  })

  const [cur, setCur] = useState('')
  const [next, setNext] = useState('')
  const changePw = useMutation({
    mutationFn: () => api.changePassword(cur, next),
    onSuccess: () => { setCur(''); setNext(''); toast.success('Password changed') },
    onError: (e: Error) => toast.error(e.message),
  })

  return (
    <div className="mx-auto max-w-2xl space-y-6">
      {/* ─── Account info ─────────────────────────────────────────── */}
      <Panel title="My Profile" subtitle="Your account details">
        {me.isLoading && <StateLine kind="loading" msg="Loading profile…" />}
        {me.isError && <StateLine kind="error" msg="Could not load profile" />}
        {me.data && (
          <div className="space-y-4">
            <div className="flex items-center gap-4">
              <div className="flex h-12 w-12 items-center justify-center rounded-full bg-[var(--color-accent-so)] text-lg font-bold text-[var(--color-accent-d)]">
                {(me.data.fullName || me.data.username).charAt(0).toUpperCase()}
              </div>
              <div>
                <div className="font-semibold text-[var(--color-ink)]">{me.data.username}</div>
                <Badge signal={roleSignal(me.data.role)}>{me.data.role}</Badge>
              </div>
            </div>

            <form onSubmit={(e) => { e.preventDefault(); saveProfile.mutate() }} className="space-y-3">
              <div>
                <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Full name</label>
                <Input value={fullName} onChange={(e) => setFullName(e.target.value)} placeholder="Your name" />
              </div>
              <div className="flex items-center gap-2">
                <Button type="submit" variant="primary" disabled={saveProfile.isPending}>
                  {saveProfile.isPending ? 'Saving…' : 'Save profile'}
                </Button>
                {saveProfile.isSuccess && <span className="text-sm text-[var(--color-ok)]">Saved</span>}
                {saveProfile.isError && <span className="text-sm text-[var(--color-bad)]">{(saveProfile.error as Error).message}</span>}
              </div>
            </form>
          </div>
        )}
      </Panel>

      {/* ─── Change password ──────────────────────────────────────── */}
      <Panel title="Change Password" subtitle="Update your account password">
        <form onSubmit={(e) => { e.preventDefault(); changePw.mutate() }} className="space-y-3">
          <div>
            <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Current password</label>
            <Input type="password" value={cur} onChange={(e) => setCur(e.target.value)} required />
          </div>
          <div>
            <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">New password (min 6)</label>
            <Input type="password" value={next} onChange={(e) => setNext(e.target.value)} required minLength={6} />
          </div>
          <div className="flex items-center gap-2">
            <Button type="submit" variant="primary" disabled={changePw.isPending}>
              {changePw.isPending ? 'Saving…' : 'Change password'}
            </Button>
            {changePw.isSuccess && <span className="text-sm text-[var(--color-ok)]">Password changed</span>}
            {changePw.isError && <span className="text-sm text-[var(--color-bad)]">{(changePw.error as Error).message}</span>}
          </div>
        </form>
      </Panel>
    </div>
  )
}
