// SignalR connection to /hubs/access. Pushes "NewAccessLog" + "DoorStateChanged".
import {
  HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel,
} from '@microsoft/signalr'
import { useEffect, useState } from 'react'
import { API_URL } from './api'
import { getToken } from './auth'
import type { AccessLogDto, DoorStateEvent, EventLogDto } from './types'

export type ConnState = 'connecting' | 'connected' | 'disconnected'

let connection: HubConnection | null = null
let starting: Promise<void> | null = null

// Module-level subscriber sets. Handlers are registered ONCE on the singleton
// connection; components add/remove callbacks here. This avoids the StrictMode
// on/off race that left the connection handlerless.
const subscribers = new Set<(log: AccessLogDto) => void>()
const doorSubscribers = new Set<(e: DoorStateEvent) => void>()
const eventSubscribers = new Set<(e: EventLogDto) => void>()

function getConnection(): HubConnection {
  if (connection) return connection
  connection = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/access`, {
      accessTokenFactory: () => getToken() ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
    .configureLogging(LogLevel.Warning)
    .build()

  // Register handlers once for the lifetime of the connection.
  connection.on('NewAccessLog', (log: AccessLogDto) => {
    subscribers.forEach((cb) => cb(log))
  })
  connection.on('DoorStateChanged', (e: DoorStateEvent) => {
    doorSubscribers.forEach((cb) => cb(e))
  })
  connection.on('NewEventLog', (e: EventLogDto) => {
    eventSubscribers.forEach((cb) => cb(e))
  })

  return connection
}

async function ensureStarted(conn: HubConnection): Promise<void> {
  if (conn.state === HubConnectionState.Connected) return
  if (starting) return starting
  starting = conn.start().finally(() => { starting = null })
  return starting
}

/**
 * Subscribe to live access-log events. Returns connection state.
 * onLog fires for each NewAccessLog broadcast from the backend.
 */
export function useAccessFeed(onLog: (log: AccessLogDto) => void): ConnState {
  const [state, setState] = useState<ConnState>('connecting')

  useEffect(() => {
    const conn = getConnection()
    let mounted = true

    subscribers.add(onLog)

    conn.onreconnecting(() => mounted && setState('connecting'))
    conn.onreconnected(() => mounted && setState('connected'))
    conn.onclose(() => mounted && setState('disconnected'))

    const sync = () =>
      mounted && setState(conn.state === HubConnectionState.Connected ? 'connected' : 'connecting')

    ensureStarted(conn)
      .then(() => sync())
      .catch(() => mounted && setState('disconnected'))

    sync()

    return () => {
      mounted = false
      subscribers.delete(onLog)
      // NOTE: do NOT off() the "NewAccessLog" handler or stop() the connection —
      // it is a shared singleton kept alive across route changes / remounts.
    }
  }, [onLog])

  return state
}

/**
 * Subscribe to live door-state events ("DoorStateChanged"). Shares the singleton
 * connection with useAccessFeed; ensures it is started for door-only pages.
 */
export function useDoorFeed(onDoor: (e: DoorStateEvent) => void): void {
  useEffect(() => {
    const conn = getConnection()
    doorSubscribers.add(onDoor)
    ensureStarted(conn).catch(() => { /* state surfaced via useAccessFeed */ })

    return () => { doorSubscribers.delete(onDoor) }
  }, [onDoor])
}

/**
 * Subscribe to live device events ("NewEventLog" — door/connectivity/emergency).
 * Shares the singleton connection.
 */
export function useEventFeed(onEvent: (e: EventLogDto) => void): void {
  useEffect(() => {
    const conn = getConnection()
    eventSubscribers.add(onEvent)
    ensureStarted(conn).catch(() => { /* state surfaced via useAccessFeed */ })

    return () => { eventSubscribers.delete(onEvent) }
  }, [onEvent])
}
