import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import { api } from '../lib/api'
import type { DeviceDto, DoorState, DoorStateEvent } from '../lib/types'
import { useDoorFeed } from '../lib/signalr'
import { Panel, Table, Th, Td, StatusLED, Badge, Button, Input, StateLine, type Signal } from '../components/ui'
import { timeAgo } from '../lib/utils'

export const Route = createFileRoute('/devices')({ component: Devices })

const ONLINE_WINDOW_MS = 5 * 60_000

function deviceSignal(d: DeviceDto): { sig: Signal; label: string; pulse: boolean } {
  if (!d.isActive) return { sig: 'dim', label: 'DECOMMISSIONED', pulse: false }
  if (!d.lastHeartbeat) return { sig: 'amber', label: 'NEVER SEEN', pulse: false }
  const online = new Date(d.lastHeartbeat).getTime() > Date.now() - ONLINE_WINDOW_MS
  return online ? { sig: 'green', label: 'ONLINE', pulse: true } : { sig: 'red', label: 'OFFLINE', pulse: false }
}

function doorSignal(state: DoorState): { sig: Signal; label: string; pulse: boolean } {
  if (state === 'open') return { sig: 'amber', label: 'OPEN', pulse: true }
  if (state === 'closed') return { sig: 'green', label: 'CLOSED', pulse: false }
  return { sig: 'dim', label: '—', pulse: false }
}

function Devices() {
  const qc = useQueryClient()
  const { data, isLoading, isError } = useQuery({ queryKey: ['devices'], queryFn: api.devices, refetchInterval: 15_000 })
  const [form, setForm] = useState({ name: '', macAddress: '', location: '' })
  const [open, setOpen] = useState(false)

  const create = useMutation({
    mutationFn: () => api.createDevice(form),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['devices'] }); setForm({ name: '', macAddress: '', location: '' }); setOpen(false) },
  })
  const del = useMutation({
    mutationFn: (id: number) => api.deleteDevice(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['devices'] }),
  })

  // Live door state — patch the cached device list as servo events arrive.
  const onDoor = useCallback((e: DoorStateEvent) => {
    qc.setQueryData<DeviceDto[]>(['devices'], (prev) =>
      prev?.map((d) => d.id === e.deviceId
        ? { ...d, doorState: e.doorState, lastDoorStateChange: e.timestamp }
        : d))
  }, [qc])
  useDoorFeed(onDoor)

  return (
    <Panel
      title="Device Registry"
      subtitle="RFID terminals — device name must match firmware DEVICE_ID"
      right={<Button variant="primary" onClick={() => setOpen((o) => !o)}>{open ? 'Cancel' : '+ Register device'}</Button>}
    >
      {open && (
        <form
          onSubmit={(e) => { e.preventDefault(); create.mutate() }}
          className="mb-5 grid grid-cols-1 gap-3 rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface-2)] p-4 sm:grid-cols-4"
        >
          <Input placeholder="Name (esp32-door-01)" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} required />
          <Input placeholder="MAC address" value={form.macAddress} onChange={(e) => setForm({ ...form, macAddress: e.target.value })} required />
          <Input placeholder="Location" value={form.location} onChange={(e) => setForm({ ...form, location: e.target.value })} required />
          <Button type="submit" variant="primary" disabled={create.isPending}>{create.isPending ? 'Saving…' : 'Create'}</Button>
          {create.isError && <p className="col-span-full text-sm text-[var(--color-bad)]">{(create.error as Error).message}</p>}
        </form>
      )}

      {isLoading && <StateLine kind="loading" msg="Loading devices…" />}
      {isError && <StateLine kind="error" msg="Registry unreachable" />}
      {data && data.length === 0 && <StateLine kind="empty" msg="No devices — register the first one" />}

      {data && data.length > 0 && (
        <Table>
          <thead>
            <tr><Th>Status</Th><Th>Door</Th><Th>Name</Th><Th>Location</Th><Th>MAC</Th><Th>Last seen</Th><Th /></tr>
          </thead>
          <tbody>
            {data.map((d) => {
              const s = deviceSignal(d)
              const door = doorSignal(d.doorState)
              return (
                <tr key={d.id} className="hover:bg-[var(--color-surface-2)]">
                  <Td><StatusLED signal={s.sig} pulse={s.pulse} label={s.label} /></Td>
                  <Td><StatusLED signal={door.sig} pulse={door.pulse} label={door.label} /></Td>
                  <Td className="font-medium text-[var(--color-ink)]">{d.name}</Td>
                  <Td>{d.location || '—'}</Td>
                  <Td className="font-mono text-xs text-[var(--color-ink-3)]">{d.macAddress || '—'}</Td>
                  <Td className="text-[var(--color-ink-3)]">{timeAgo(d.lastHeartbeat)}</Td>
                  <Td className="text-right">
                    {d.isActive
                      ? <Button variant="danger" onClick={() => { if (confirm(`Decommission ${d.name}?`)) del.mutate(d.id) }}>Remove</Button>
                      : <Badge signal="dim">Retired</Badge>}
                  </Td>
                </tr>
              )
            })}
          </tbody>
        </Table>
      )}
    </Panel>
  )
}
