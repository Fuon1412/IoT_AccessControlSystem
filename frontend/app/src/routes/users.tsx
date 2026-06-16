import { createFileRoute } from '@tanstack/react-router'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '../lib/api'
import type { UserDto } from '../lib/types'
import { Panel, Table, Th, Td, StatusLED, Badge, Button, Input, Select, StateLine, type Signal } from '../components/ui'
import { stamp } from '../lib/utils'

export const Route = createFileRoute('/users')({ component: Users })

const ROLES = ['Employee', 'Admin', 'Device']

function roleSignal(role: string): Signal {
  if (role === 'Admin') return 'amber'
  if (role === 'Device') return 'cyan'
  if (role === 'Employee') return 'green'
  return 'dim'
}

function Users() {
  const qc = useQueryClient()
  const { data, isLoading, isError } = useQuery({ queryKey: ['users'], queryFn: api.users })
  const [form, setForm] = useState({ username: '', fullName: '', password: '', role: 'Employee', rfidUid: '' })
  const [open, setOpen] = useState(false)
  const [editId, setEditId] = useState<number | null>(null)
  const [pwId, setPwId] = useState<number | null>(null)

  const invalidate = () => {
    qc.invalidateQueries({ queryKey: ['users'] })
    qc.invalidateQueries({ queryKey: ['cards'] })
  }

  const create = useMutation({
    mutationFn: () => {
      const body = {
        username: form.username, fullName: form.fullName || undefined,
        password: form.password, role: form.role, rfidUid: form.rfidUid || undefined,
      }
      return form.role === 'Employee' ? api.createEmployee(body) : api.createUser(body)
    },
    onSuccess: () => { invalidate(); setForm({ username: '', fullName: '', password: '', role: 'Employee', rfidUid: '' }); setOpen(false) },
  })
  const del = useMutation({ mutationFn: (id: number) => api.deleteUser(id), onSuccess: invalidate })
  const toggle = useMutation({
    mutationFn: ({ id, isActive }: { id: number; isActive: boolean }) => api.updateUser(id, { isActive }),
    onSuccess: invalidate,
  })

  return (
    <div className="space-y-6">
      <Panel
        title="Users & Employees"
        subtitle="Accounts and access roles. Employees can only view their own access history."
        right={<Button variant="primary" onClick={() => setOpen((o) => !o)}>{open ? 'Cancel' : '+ New account'}</Button>}
      >
        {open && (
          <form onSubmit={(e) => { e.preventDefault(); create.mutate() }}
            className="mb-5 grid grid-cols-1 gap-3 rounded-[var(--radius-md)] border border-[var(--color-line)] bg-[var(--color-surface-2)] p-4 sm:grid-cols-2 lg:grid-cols-6">
            <Input placeholder="Full name" value={form.fullName} onChange={(e) => setForm({ ...form, fullName: e.target.value })} required />
            <Input placeholder="Username" value={form.username} onChange={(e) => setForm({ ...form, username: e.target.value })} required />
            <Input type="password" placeholder="Password" value={form.password} onChange={(e) => setForm({ ...form, password: e.target.value })} required />
            <Select value={form.role} onChange={(e) => setForm({ ...form, role: e.target.value })}>
              {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
            </Select>
            <Input placeholder="RFID UID (optional)" value={form.rfidUid} onChange={(e) => setForm({ ...form, rfidUid: e.target.value })} />
            <Button type="submit" variant="primary" disabled={create.isPending}>{create.isPending ? 'Saving…' : 'Create'}</Button>
            {create.isError && <p className="col-span-full text-sm text-[var(--color-bad)]">{(create.error as Error).message}</p>}
          </form>
        )}

        {isLoading && <StateLine kind="loading" msg="Loading accounts…" />}
        {isError && <StateLine kind="error" msg="Unreachable — Admin role required" />}
        {data && data.length === 0 && <StateLine kind="empty" msg="No accounts yet" />}

        {data && data.length > 0 && (
          <Table>
            <thead><tr><Th>Status</Th><Th>Full name</Th><Th>Username</Th><Th>Role</Th><Th>Created</Th><Th /></tr></thead>
            <tbody>
              {data.map((u) => (
                <UserRow key={u.id} user={u}
                  editing={editId === u.id} onEdit={() => { setEditId(editId === u.id ? null : u.id); setPwId(null) }}
                  pwOpen={pwId === u.id} onPw={() => { setPwId(pwId === u.id ? null : u.id); setEditId(null) }}
                  onSaved={() => { setEditId(null); setPwId(null) }}
                  onToggle={() => toggle.mutate({ id: u.id, isActive: !u.isActive })}
                  onDelete={() => { if (confirm(`Delete ${u.username}?`)) del.mutate(u.id) }} />
              ))}
            </tbody>
          </Table>
        )}
      </Panel>
    </div>
  )
}

// ─── User row with inline edit + reset-password ──────────────────────────────────
function UserRow({
  user, editing, onEdit, pwOpen, onPw, onSaved, onToggle, onDelete,
}: {
  user: UserDto; editing: boolean; onEdit: () => void; pwOpen: boolean; onPw: () => void
  onSaved: () => void; onToggle: () => void; onDelete: () => void
}) {
  const qc = useQueryClient()
  const [f, setF] = useState({ fullName: user.fullName, username: user.username, role: user.role })
  const [pw, setPw] = useState('')

  const save = useMutation({
    mutationFn: () => api.updateUser(user.id, f),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ['users'] }); onSaved() },
  })
  const reset = useMutation({
    mutationFn: () => api.resetPassword(user.id, pw),
    onSuccess: () => { setPw(''); onSaved() },
  })

  return (
    <>
      <tr>
        <Td><StatusLED signal={user.isActive ? 'green' : 'dim'} label={user.isActive ? 'Active' : 'Disabled'} /></Td>
        <Td className="font-medium text-[var(--color-ink)]">{user.fullName || '—'}</Td>
        <Td className="text-[var(--color-ink-2)]">{user.username}</Td>
        <Td><Badge signal={roleSignal(user.role)}>{user.role}</Badge></Td>
        <Td className="text-[var(--color-ink-3)]">{stamp(user.createdAt)}</Td>
        <Td>
          <div className="flex justify-end gap-1.5">
            <Button onClick={onEdit}>{editing ? 'Close' : 'Edit'}</Button>
            <Button onClick={onPw}>Password</Button>
            <Button onClick={onToggle}>{user.isActive ? 'Disable' : 'Enable'}</Button>
            <Button variant="danger" onClick={onDelete}>Delete</Button>
          </div>
        </Td>
      </tr>

      {editing && (
        <tr>
          <Td className="bg-[var(--color-surface-2)]" />
          <td colSpan={5} className="border-b border-[var(--color-line)] bg-[var(--color-surface-2)] px-3 py-3">
            <form onSubmit={(e) => { e.preventDefault(); save.mutate() }} className="flex flex-wrap items-center gap-2">
              <Input className="max-w-[12rem]" placeholder="Full name" value={f.fullName} onChange={(e) => setF({ ...f, fullName: e.target.value })} />
              <Input className="max-w-[12rem]" placeholder="Username" value={f.username} onChange={(e) => setF({ ...f, username: e.target.value })} />
              <Select className="max-w-[10rem]" value={f.role} onChange={(e) => setF({ ...f, role: e.target.value })}>
                {ROLES.map((r) => <option key={r} value={r}>{r}</option>)}
              </Select>
              <Button type="submit" variant="primary" disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save'}</Button>
              {save.isError && <span className="text-sm text-[var(--color-bad)]">{(save.error as Error).message}</span>}
            </form>
          </td>
        </tr>
      )}

      {pwOpen && (
        <tr>
          <Td className="bg-[var(--color-surface-2)]" />
          <td colSpan={5} className="border-b border-[var(--color-line)] bg-[var(--color-surface-2)] px-3 py-3">
            <form onSubmit={(e) => { e.preventDefault(); reset.mutate() }} className="flex flex-wrap items-center gap-2">
              <span className="text-sm text-[var(--color-ink-2)]">Reset password for <b>{user.username}</b>:</span>
              <Input className="max-w-[14rem]" type="password" placeholder="New password (min 6)" value={pw} onChange={(e) => setPw(e.target.value)} required minLength={6} />
              <Button type="submit" variant="primary" disabled={reset.isPending}>{reset.isPending ? 'Saving…' : 'Set password'}</Button>
              {reset.isSuccess && <span className="text-sm text-[var(--color-ok)]">Updated</span>}
              {reset.isError && <span className="text-sm text-[var(--color-bad)]">{(reset.error as Error).message}</span>}
            </form>
          </td>
        </tr>
      )}
    </>
  )
}

