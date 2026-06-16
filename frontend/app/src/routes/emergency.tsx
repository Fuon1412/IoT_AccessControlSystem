import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../lib/api'
import { Panel, Select, Input, Button, StatusLED, StateLine } from '../components/ui'

export const Route = createFileRoute('/emergency')({ component: Emergency })

function Emergency() {
  const devices = useQuery({ queryKey: ['devices'], queryFn: api.devices })
  const [deviceId, setDeviceId] = useState(0)
  const [action, setAction] = useState<'lock' | 'unlock'>('lock')
  const [password, setPassword] = useState('')

  const send = useMutation({
    mutationFn: () => api.emergency(deviceId, action, password),
    onSuccess: () => setPassword(''),
  })

  return (
    <div className="mx-auto max-w-2xl">
      <Panel
        title="Emergency Door Control"
        subtitle="Force-lock or force-open a door. Admin password required."
      >
        {/* warning banner */}
        <div className="mb-5 flex items-start gap-3 rounded-[var(--radius-md)] border border-[var(--color-warn)]/30 bg-[var(--color-warn-so)] p-3">
          <StatusLED signal="amber" pulse />
          <p className="text-sm text-[var(--color-ink-2)]">
            This sends an immediate command to the device. The buzzer will alarm for 5 seconds,
            then the display shows the forced state. Use only in emergencies.
          </p>
        </div>

        {devices.isLoading && <StateLine kind="loading" msg="Loading devices…" />}
        {devices.isError && <StateLine kind="error" msg="Could not load devices" />}

        {devices.data && (
          <form onSubmit={(e) => { e.preventDefault(); if (deviceId) send.mutate() }} className="space-y-4">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Target device</label>
              <Select value={deviceId} onChange={(e) => setDeviceId(Number(e.target.value))} required>
                <option value={0} disabled>select device…</option>
                {devices.data.map((d) => <option key={d.id} value={d.id}>{d.name} — {d.location || 'no location'}</option>)}
              </Select>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Action</label>
              <div className="flex gap-2">
                <Button type="button" variant={action === 'lock' ? 'primary' : 'ghost'} onClick={() => setAction('lock')}>
                  🔒 Force lock
                </Button>
                <Button type="button" variant={action === 'unlock' ? 'primary' : 'ghost'} onClick={() => setAction('unlock')}>
                  🔓 Force open
                </Button>
              </div>
            </div>

            <div>
              <label className="mb-1.5 block text-sm font-medium text-[var(--color-ink-2)]">Confirm your password</label>
              <Input type="password" value={password} onChange={(e) => setPassword(e.target.value)}
                placeholder="Admin password" autoComplete="current-password" required />
            </div>

            <div className="flex items-center gap-3 pt-1">
              <Button type="submit" variant="danger" disabled={!deviceId || !password || send.isPending}>
                {send.isPending ? 'Sending…' : `Send ${action === 'lock' ? 'LOCK' : 'OPEN'} command`}
              </Button>
              {send.isSuccess && (
                <span className="text-sm font-medium text-[var(--color-ok)]">
                  Command sent to {send.data?.device} — {send.data?.action}
                </span>
              )}
              {send.isError && (
                <span className="text-sm text-[var(--color-bad)]">{(send.error as Error).message}</span>
              )}
            </div>
          </form>
        )}
      </Panel>
    </div>
  )
}
