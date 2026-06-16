// SignalR connection to /hubs/access. Pushes "NewAccessLog" events.
import {
  HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel,
} from '@microsoft/signalr'
import { useEffect, useState } from 'react'
import { API_URL } from './api'
import { getToken } from './auth'
import type { AccessLogDto } from './types'

export type ConnState = 'connecting' | 'connected' | 'disconnected'

let connection: HubConnection | null = null
let starting: Promise<void> | null = null

// Module-level subscriber set. The "NewAccessLog" handler is registered ONCE
// on the singleton connection; components add/remove callbacks here.
// This avoids the StrictMode on/off race that left the connection handlerless.
const subscribers = new Set<(log: AccessLogDto) => void>()

function getConnection(): HubConnection {
  if (connection) return connection
  connection = new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/access`, {
      accessTokenFactory: () => getToken() ?? '',
    })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 15000])
    .configureLogging(LogLevel.Warning)
    .build()

  // Register handler once for the lifetime of the connection.
  connection.on('NewAccessLog', (log: AccessLogDto) => {
    subscribers.forEach((cb) => cb(log))
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
