import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useRef, useState } from 'react'
import { api } from '../lib/api'
import { useAccessFeed, useDoorFeed, type ConnState } from '../lib/signalr'
import type { AccessLogDto, DoorStateEvent } from '../lib/types'
import { Panel, StatCard, StatusLED, Badge, StateLine, type Signal } from '../components/ui'
import { clock, timeAgo, cn } from '../lib/utils'

export const Route = createFileRoute('/')({ component: Dashboard })

const FEED_MAX = 40

// Feed mixes RFID scans and door servo events — tagged union.
type FeedItem =
  | ({ kind: 'access' } & AccessLogDto)
  | ({ kind: 'door' } & DoorStateEvent)

function Dashboard() {
  const devices = useQuery({ queryKey: ['devices'], queryFn: api.devices })
  const logs = useQuery({ queryKey: ['logs'], queryFn: api.accessLogs })
  const users = useQuery({ queryKey: ['users'], queryFn: api.users })

  // live feed buffer — seeded from REST, prepended via SignalR
  const [feed, setFeed] = useState<FeedItem[]>([])
  const seeded = useRef(false)

  useEffect(() => {
    if (!seeded.current && logs.data) {
      setFeed(logs.data.slice(0, FEED_MAX).map((l) => ({ kind: 'access' as const, ...l })))
      seeded.current = true
    }
  }, [logs.data])

  const onLog = useCallback((log: AccessLogDto) => {
    setFeed((prev) => [{ kind: 'access' as const, ...log }, ...prev].slice(0, FEED_MAX))
  }, [])
  const onDoor = useCallback((e: DoorStateEvent) => {
    setFeed((prev) => [{ kind: 'door' as const, ...e }, ...prev].slice(0, FEED_MAX))
  }, [])
  const conn: ConnState = useAccessFeed(onLog)
  useDoorFeed(onDoor)

  const online = devices.data?.filter((d) =>
    d.lastHeartbeat && new Date(d.lastHeartbeat).getTime() > Date.now() - 5 * 60_000).length ?? 0
  const totalDevices = devices.data?.length ?? 0
  const granted = feed.filter((l) => l.kind === 'access' && l.accessGranted).length
  const denied = feed.filter((l) => l.kind === 'access' && !l.accessGranted).length

  const connSig: Record<ConnState, Signal> = { connected: 'green', connecting: 'amber', disconnected: 'red' }
  const connLabel: Record<ConnState, string> = { connected: 'LIVE', connecting: 'SYNC…', disconnected: 'OFFLINE' }

  return (
    <div className="space-y-6">
      {/* ─── Stat row ─────────────────────────────────────────── */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
        <StatCard label="Devices Online" signal={online === totalDevices && totalDevices > 0 ? 'green' : 'amber'}
          value={online} unit={`/ ${totalDevices}`}
          foot={<span><StatusLED signal={online > 0 ? 'green' : 'dim'} /> sensors reporting</span>} />
        <StatCard label="Grants (session)" signal="green" value={granted}
          foot="access permitted" />
        <StatCard label="Denials (session)" signal={denied > 0 ? 'red' : 'dim'} value={denied}
          foot="access rejected" />
        <StatCard label="Users" signal="cyan" value={users.data?.length ?? '—'}
          foot="registered accounts" />
      </div>

      {/* ─── Live feed ────────────────────────────────────────── */}
      <Panel
        title="Live Access Feed"
        subtitle="Real-time RFID scan + door servo events"
        right={<StatusLED signal={connSig[conn]} pulse={conn !== 'disconnected'} label={connLabel[conn]} />}
        className="min-h-[60vh]"
      >
        {logs.isLoading && <StateLine kind="loading" msg="Connecting to live feed…" />}
        {logs.isError && <StateLine kind="error" msg="Feed unreachable — check backend + token" />}
        {!logs.isLoading && !logs.isError && feed.length === 0 && (
          <StateLine kind="empty" msg="No events yet — awaiting first scan" />
        )}

        <div className="space-y-1.5">
          {feed.map((item, i) => item.kind === 'door'
            ? <DoorRow key={`door-${item.deviceId}-${item.timestamp}`} ev={item} fresh={i === 0 && seeded.current} />
            : <FeedRow key={`${item.id}-${item.requestId}`} log={item} fresh={i === 0 && seeded.current} />)}
        </div>
      </Panel>
    </div>
  )
}

function FeedRow({ log, fresh }: { log: AccessLogDto; fresh: boolean }) {
  const ok = log.accessGranted
  return (
    <div className={cn(
      'flex items-center gap-3 rounded-[var(--radius-sm)] border border-[var(--color-line)] px-3 py-2.5 text-sm',
      fresh && 'row-in',
    )}>
      <span className="tabular-nums text-xs text-[var(--color-ink-3)]">{clock(log.timestamp)}</span>
      <StatusLED signal={ok ? 'green' : 'red'} />
      <Badge signal={ok ? 'green' : 'red'}>{ok ? 'GRANT' : 'DENY'}</Badge>
      <Badge signal={ok ? 'green' : 'red'}>{ok ? 'Granted' : 'Denied'}</Badge>
      <span className="text-[var(--color-info)]">{log.deviceName}</span>
      <span className="font-mono text-xs font-medium tabular-nums text-[var(--color-ink)]">{log.rfidUid}</span>
      <span className="ml-auto truncate text-[var(--color-ink-2)]">
        {log.username ?? <span className="text-[var(--color-ink-3)]">{log.denyReason ?? 'unknown card'}</span>}
      </span>
      <span className="hidden text-xs text-[var(--color-ink-3)] sm:inline">{timeAgo(log.timestamp)}</span>
    </div>
  )
}

function DoorRow({ ev, fresh }: { ev: DoorStateEvent; fresh: boolean }) {
  const open = ev.doorState === 'open'
  const sig: Signal = open ? 'amber' : 'cyan'
  return (
    <div className={cn(
      'flex items-center gap-3 rounded-[var(--radius-sm)] border border-[var(--color-line)] px-3 py-2.5 text-sm',
      fresh && 'row-in',
    )}>
      <span className="tabular-nums text-xs text-[var(--color-ink-3)]">{clock(ev.timestamp)}</span>
      <StatusLED signal={sig} pulse={open} />
      <Badge signal={sig}>DOOR</Badge>
      <Badge signal={sig}>{open ? 'Opened' : 'Closed'}</Badge>
      <span className="text-[var(--color-info)]">{ev.deviceName}</span>
      <span className="ml-auto truncate text-[var(--color-ink-3)]">servo {open ? 'unlocked' : 'locked'}</span>
      <span className="hidden text-xs text-[var(--color-ink-3)] sm:inline">{timeAgo(ev.timestamp)}</span>
    </div>
  )
}
