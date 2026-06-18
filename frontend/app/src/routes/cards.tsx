import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../lib/api'
import type { RfidCardDto } from '../lib/types'
import { Panel, Table, Th, Td, StatusLED, Badge, Button, Select, StateLine, ConfirmDialog } from '../components/ui'
import { useToast } from '../components/toast'
import { stamp } from '../lib/utils'

export const Route = createFileRoute('/cards')({ component: Cards })

function Cards() {
  const qc = useQueryClient()
  const toast = useToast()
  const cards = useQuery({ queryKey: ['cards'], queryFn: api.cards, refetchInterval: 15_000 })
  const users = useQuery({ queryKey: ['users'], queryFn: api.users })
  const [assignTo, setAssignTo] = useState<Record<number, number>>({})
  const [pendingRevoke, setPendingRevoke] = useState<RfidCardDto | null>(null)

  const assign = useMutation({
    mutationFn: ({ cardId, userId }: { cardId: number; userId: number }) => api.assignCard(cardId, userId),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cards'] }); toast.success('Card assigned') },
    onError: (e: Error) => toast.error(e.message),
  })
  const deactivate = useMutation({
    mutationFn: (id: number) => api.deactivateCard(id),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['cards'] }); setPendingRevoke(null); toast.success('Card revoked') },
    onError: (e: Error) => { setPendingRevoke(null); toast.error(e.message) },
  })

  const list = cards.data ?? []
  const unassigned = list.filter((c) => !c.isAssigned)
  const assigned = list.filter((c) => c.isAssigned)

  return (
   <>
    <div className="space-y-6">
      {/* ─── Unassigned (needs admin action) ─────────────────────── */}
      <Panel
        title="Unassigned Cards"
        subtitle="Cards scanned by readers but not yet linked to a user — assign one below"
        right={<Badge signal={unassigned.length > 0 ? 'amber' : 'dim'}>{unassigned.length} pending</Badge>}
      >
        {cards.isLoading && <StateLine kind="loading" msg="Loading cards…" />}
        {cards.isError && <StateLine kind="error" msg="Unreachable — Admin role required" />}
        {!cards.isLoading && unassigned.length === 0 && <StateLine kind="empty" msg="No unassigned cards" />}

        {unassigned.length > 0 && (
          <Table>
            <thead><tr><Th>UID</Th><Th>First seen</Th><Th>Assign to user</Th><Th /></tr></thead>
            <tbody>
              {unassigned.map((c) => (
                <tr key={c.id}>
                  <Td><span className="font-mono font-medium text-[var(--color-ink)]">{c.uid}</span></Td>
                  <Td className="text-[var(--color-ink-3)]">{stamp(c.registeredAt)}</Td>
                  <Td>
                    <Select value={assignTo[c.id] ?? 0}
                      onChange={(e) => setAssignTo({ ...assignTo, [c.id]: Number(e.target.value) })}
                      className="max-w-[14rem]">
                      <option value={0} disabled>select user…</option>
                      {users.data?.map((u) => <option key={u.id} value={u.id}>{u.username} ({u.role})</option>)}
                    </Select>
                  </Td>
                  <Td className="text-right">
                    <Button variant="primary" disabled={!assignTo[c.id] || assign.isPending}
                      onClick={() => assign.mutate({ cardId: c.id, userId: assignTo[c.id] })}>
                      Assign
                    </Button>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Panel>

      {/* ─── Assigned ────────────────────────────────────────────── */}
      <Panel title="Assigned Cards" subtitle="RFID credentials linked to users — revoke or reassign">
        {assigned.length === 0 && !cards.isLoading && <StateLine kind="empty" msg="No assigned cards yet" />}
        {assigned.length > 0 && (
          <Table>
            <thead><tr><Th>Status</Th><Th>UID</Th><Th>Holder</Th><Th>Registered</Th><Th /></tr></thead>
            <tbody>
              {assigned.map((c) => (
                <tr key={c.id}>
                  <Td><StatusLED signal={c.isActive ? 'green' : 'red'} label={c.isActive ? 'Active' : 'Revoked'} /></Td>
                  <Td><span className="font-mono font-medium text-[var(--color-ink)]">{c.uid}</span></Td>
                  <Td className="text-[var(--color-ink)]">{c.username ?? '—'}</Td>
                  <Td className="text-[var(--color-ink-3)]">{stamp(c.registeredAt)}</Td>
                  <Td>
                    <div className="flex flex-wrap items-center justify-end gap-1.5">
                      {/* Reassign (also reactivates revoked cards) */}
                      <Select value={assignTo[c.id] ?? 0}
                        onChange={(e) => setAssignTo({ ...assignTo, [c.id]: Number(e.target.value) })}
                        className="max-w-[12rem]">
                        <option value={0} disabled>reassign to…</option>
                        {users.data?.map((u) => <option key={u.id} value={u.id}>{u.username} ({u.role})</option>)}
                      </Select>
                      <Button variant="primary" disabled={!assignTo[c.id] || assign.isPending}
                        onClick={() => assign.mutate({ cardId: c.id, userId: assignTo[c.id] })}>
                        {c.isActive ? 'Reassign' : 'Reassign & activate'}
                      </Button>
                      {c.isActive && (
                        <Button variant="danger" onClick={() => setPendingRevoke(c)}>
                          Revoke
                        </Button>
                      )}
                    </div>
                  </Td>
                </tr>
              ))}
            </tbody>
          </Table>
        )}
      </Panel>
    </div>

    <ConfirmDialog
      open={pendingRevoke !== null}
      title="Revoke card"
      message={<>Revoke card <b className="font-mono">{pendingRevoke?.uid}</b>? The holder loses access immediately.</>}
      confirmLabel="Revoke"
      busy={deactivate.isPending}
      onConfirm={() => pendingRevoke && deactivate.mutate(pendingRevoke.id)}
      onCancel={() => setPendingRevoke(null)}
    />
   </>
  )
}
