import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../lib/api'
import { Panel, Table, Th, Td, StatusLED, Badge, Button, StateLine } from '../components/ui'
import { stamp } from '../lib/utils'

export const Route = createFileRoute('/logs')({ component: Logs })

type Filter = 'all' | 'grant' | 'deny'

function Logs() {
  const { data, isLoading, isError } = useQuery({ queryKey: ['logs'], queryFn: api.accessLogs, refetchInterval: 20_000 })
  const [filter, setFilter] = useState<Filter>('all')

  const rows = (data ?? []).filter((l) =>
    filter === 'all' ? true : filter === 'grant' ? l.accessGranted : !l.accessGranted)

  const total = data?.length ?? 0
  const grants = data?.filter((l) => l.accessGranted).length ?? 0
  const denies = total - grants

  return (
    <Panel
      title="Access Log Archive"
      subtitle={`${total} total · ${grants} granted · ${denies} denied`}
      right={
        <div className="flex gap-1">
          {(['all', 'grant', 'deny'] as Filter[]).map((f) => (
            <Button key={f} variant={filter === f ? 'primary' : 'ghost'} onClick={() => setFilter(f)} className="capitalize">{f}</Button>
          ))}
        </div>
      }
    >
      {isLoading && <StateLine kind="loading" msg="Loading archive…" />}
      {isError && <StateLine kind="error" msg="Archive unreachable" />}
      {data && rows.length === 0 && <StateLine kind="empty" msg="No matching records" />}

      {rows.length > 0 && (
        <Table>
          <thead>
            <tr><Th>Result</Th><Th>Timestamp</Th><Th>Device</Th><Th>UID</Th><Th>Holder</Th><Th>Reason</Th></tr>
          </thead>
          <tbody>
            {rows.map((l) => (
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
    </Panel>
  )
}
