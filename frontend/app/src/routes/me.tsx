import { createFileRoute } from '@tanstack/react-router'
import { useQuery } from '@tanstack/react-query'
import { api } from '../lib/api'
import { Panel, Table, Th, Td, StatusLED, Badge, StateLine } from '../components/ui'
import { stamp } from '../lib/utils'

export const Route = createFileRoute('/me')({ component: MyAccess })

function MyAccess() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['my-logs'], queryFn: api.myAccessLogs, refetchInterval: 20_000,
  })

  const total = data?.length ?? 0
  const grants = data?.filter((l) => l.accessGranted).length ?? 0

  return (
    <Panel
      title="My Access History"
      subtitle={`${total} events · ${grants} granted`}
    >
      {isLoading && <StateLine kind="loading" msg="Loading your history…" />}
      {isError && <StateLine kind="error" msg="Could not load your history" />}
      {data && data.length === 0 && <StateLine kind="empty" msg="No access events recorded for your card yet" />}

      {data && data.length > 0 && (
        <Table>
          <thead>
            <tr><Th>Result</Th><Th>Time</Th><Th>Device</Th><Th>Card</Th></tr>
          </thead>
          <tbody>
            {data.map((l) => (
              <tr key={l.id} className="hover:bg-[var(--color-surface-2)]">
                <Td>
                  <span className="flex items-center gap-2">
                    <StatusLED signal={l.accessGranted ? 'green' : 'red'} />
                    <Badge signal={l.accessGranted ? 'green' : 'red'}>{l.accessGranted ? 'Granted' : 'Denied'}</Badge>
                  </span>
                </Td>
                <Td className="tabular-nums text-[var(--color-ink-3)]">{stamp(l.timestamp)}</Td>
                <Td className="text-[var(--color-info)]">{l.deviceName}</Td>
                <Td className="font-mono text-xs tabular-nums text-[var(--color-ink)]">{l.rfidUid}</Td>
              </tr>
            ))}
          </tbody>
        </Table>
      )}
    </Panel>
  )
}
