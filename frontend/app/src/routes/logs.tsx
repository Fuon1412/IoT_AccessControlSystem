import { createFileRoute } from '@tanstack/react-router'
import { useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useState } from 'react'
import { api } from '../lib/api'
import { useAccessFeed, useEventFeed } from '../lib/signalr'
import { Panel, Table, Th, Td, StatusLED, Badge, Button, StateLine, Input, Select, type Signal } from '../components/ui'
import { stamp } from '../lib/utils'
import type { EventLogDto } from '../lib/types'

export const Route = createFileRoute('/logs')({ component: Logs })

type Tab = 'access' | 'events'
type Result = 'all' | 'grant' | 'deny'

// Inclusive day-range filter. Empty bound = open-ended.
function inRange(iso: string, from: string, to: string): boolean {
  const t = new Date(iso).getTime()
  if (from && t < new Date(`${from}T00:00:00`).getTime()) return false
  if (to && t > new Date(`${to}T23:59:59.999`).getTime()) return false
  return true
}

// Event detail → colour + label.
const evSignal: Record<string, Signal> = {
  online: 'green', offline: 'red', open: 'amber', closed: 'dim', unlock: 'amber', lock: 'red',
}
const evTypeLabel: Record<string, string> = {
  door: 'Door', connectivity: 'Link', emergency: 'Emergency',
}

function Logs() {
  const qc = useQueryClient()
  const [tab, setTab] = useState<Tab>('access')

  // Shared filters
  const [deviceId, setDeviceId] = useState('all')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  // Access-only
  const [result, setResult] = useState<Result>('all')
  const [userId, setUserId] = useState('all')   // 'all' | 'none' | <id>
  // Events-only
  const [evType, setEvType] = useState('all')    // 'all' | door | connectivity | emergency

  const logs = useQuery({ queryKey: ['logs'], queryFn: api.accessLogs, refetchInterval: 20_000 })
  const events = useQuery({ queryKey: ['events'], queryFn: api.eventLogs, refetchInterval: 20_000 })
  const devices = useQuery({ queryKey: ['devices'], queryFn: api.devices })
  const users = useQuery({ queryKey: ['users'], queryFn: api.users })

  // Live: refetch the active list on each push.
  const refetchLogs = useCallback(() => { qc.invalidateQueries({ queryKey: ['logs'] }) }, [qc])
  const refetchEvents = useCallback((_e: EventLogDto) => { qc.invalidateQueries({ queryKey: ['events'] }) }, [qc])
  useAccessFeed(refetchLogs)
  useEventFeed(refetchEvents)

  const resetFilters = () => {
    setDeviceId('all'); setFrom(''); setTo(''); setResult('all'); setUserId('all'); setEvType('all')
  }

  const accessRows = (logs.data ?? []).filter((l) =>
    (result === 'all' || (result === 'grant' ? l.accessGranted : !l.accessGranted)) &&
    (deviceId === 'all' || l.deviceId === Number(deviceId)) &&
    (userId === 'all' || (userId === 'none' ? l.userId == null : l.userId === Number(userId))) &&
    inRange(l.timestamp, from, to))

  const eventRows = (events.data ?? []).filter((e) =>
    (evType === 'all' || e.eventType === evType) &&
    (deviceId === 'all' || e.deviceId === Number(deviceId)) &&
    inRange(e.timestamp, from, to))

  const isAccess = tab === 'access'
  const q = isAccess ? logs : events
  const shown = isAccess ? accessRows.length : eventRows.length
  const total = (isAccess ? logs.data?.length : events.data?.length) ?? 0

  const fld = 'flex flex-col gap-1'
  const lbl = 'text-xs font-medium text-[var(--color-ink-3)]'

  return (
    <Panel
      title="Log Archive"
      subtitle={`${shown} shown · ${total} total`}
      right={
        <div className="flex gap-1">
          <Button variant={isAccess ? 'primary' : 'ghost'} onClick={() => setTab('access')}>Access</Button>
          <Button variant={!isAccess ? 'primary' : 'ghost'} onClick={() => setTab('events')}>Device Events</Button>
        </div>
      }
    >
      {/* ─── Filter bar ─────────────────────────────────────────── */}
      <div className="mb-4 flex flex-wrap items-end gap-3">
        {isAccess && (
          <div className={fld}>
            <span className={lbl}>Result</span>
            <div className="flex gap-1">
              {(['all', 'grant', 'deny'] as Result[]).map((f) => (
                <Button key={f} variant={result === f ? 'primary' : 'ghost'} onClick={() => setResult(f)} className="capitalize">{f}</Button>
              ))}
            </div>
          </div>
        )}

        {!isAccess && (
          <div className={fld}>
            <span className={lbl}>Event type</span>
            <div className="w-44">
              <Select value={evType} onChange={(e) => setEvType(e.target.value)}>
                <option value="all">All events</option>
                <option value="door">Door</option>
                <option value="connectivity">Connectivity</option>
                <option value="emergency">Emergency</option>
              </Select>
            </div>
          </div>
        )}

        <div className={fld}>
          <span className={lbl}>Device</span>
          <div className="w-48">
            <Select value={deviceId} onChange={(e) => setDeviceId(e.target.value)}>
              <option value="all">All devices</option>
              {(devices.data ?? []).map((d) => (
                <option key={d.id} value={d.id}>{d.name}</option>
              ))}
            </Select>
          </div>
        </div>

        {isAccess && (
          <div className={fld}>
            <span className={lbl}>Holder</span>
            <div className="w-48">
              <Select value={userId} onChange={(e) => setUserId(e.target.value)}>
                <option value="all">All holders</option>
                <option value="none">Unknown card</option>
                {(users.data ?? []).map((u) => (
                  <option key={u.id} value={u.id}>{u.username}</option>
                ))}
              </Select>
            </div>
          </div>
        )}

        <div className={fld}>
          <span className={lbl}>From</span>
          <Input type="date" value={from} max={to || undefined} onChange={(e) => setFrom(e.target.value)} className="w-40" />
        </div>
        <div className={fld}>
          <span className={lbl}>To</span>
          <Input type="date" value={to} min={from || undefined} onChange={(e) => setTo(e.target.value)} className="w-40" />
        </div>

        <Button variant="ghost" onClick={resetFilters}>Reset</Button>
      </div>

      {/* ─── States ─────────────────────────────────────────────── */}
      {q.isLoading && <StateLine kind="loading" msg="Loading archive…" />}
      {q.isError && <StateLine kind="error" msg="Archive unreachable" />}
      {q.data && shown === 0 && <StateLine kind="empty" msg="No matching records" />}

      {/* ─── Access table ───────────────────────────────────────── */}
      {isAccess && accessRows.length > 0 && (
        <Table>
          <thead>
            <tr><Th>Result</Th><Th>Timestamp</Th><Th>Device</Th><Th>UID</Th><Th>Holder</Th><Th>Reason</Th></tr>
          </thead>
          <tbody>
            {accessRows.map((l) => (
              <tr key={l.id} className="hover:bg-[var(--color-surface-2)]">
                <Td>
                  <span className="flex items-center gap-2">
                    <StatusLED signal={l.accessGranted ? 'green' : 'red'} />
                    <Badge signal={l.accessGranted ? 'green' : 'red'}>{l.accessGranted ? 'Granted' : 'Denied'}</Badge>
                  </span>
                </Td>
                <Td className="tabular-nums text-[var(--color-ink-3)]">{stamp(l.timestamp)}</Td>
                <Td className="text-[var(--color-info)]">{l.deviceName}</Td>
                <Td className="font-mono text-xs font-medium tabular-nums text-[var(--color-ink)]">{l.rfidUid}</Td>
                <Td className="text-[var(--color-ink)]">{l.username ?? <span className="text-[var(--color-ink-3)]">—</span>}</Td>
                <Td className="text-[var(--color-ink-3)]">{l.denyReason ?? ''}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}

      {/* ─── Events table ───────────────────────────────────────── */}
      {!isAccess && eventRows.length > 0 && (
        <Table>
          <thead>
            <tr><Th>Event</Th><Th>State</Th><Th>Timestamp</Th><Th>Device</Th><Th>By</Th></tr>
          </thead>
          <tbody>
            {eventRows.map((e) => {
              const sig = evSignal[e.detail] ?? 'dim'
              return (
                <tr key={e.id} className="hover:bg-[var(--color-surface-2)]">
                  <Td><Badge signal="cyan">{evTypeLabel[e.eventType] ?? e.eventType}</Badge></Td>
                  <Td>
                    <span className="flex items-center gap-2">
                      <StatusLED signal={sig} />
                      <span className="font-medium capitalize text-[var(--color-ink)]">{e.detail}</span>
                    </span>
                  </Td>
                  <Td className="tabular-nums text-[var(--color-ink-3)]">{stamp(e.timestamp)}</Td>
                  <Td className="text-[var(--color-info)]">{e.deviceName}</Td>
                  <Td className="text-[var(--color-ink-2)]">{e.actor ?? <span className="text-[var(--color-ink-3)]">—</span>}</Td>
                </tr>
              )
            })}
          </tbody>
        </Table>
      )}
    </Panel>
  )
}
