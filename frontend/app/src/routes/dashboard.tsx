import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/dashboard')({
  component: Dashboard,
})

function Dashboard() {
  return (
    <div className="p-2">
      <h1 className="text-3xl font-bold mb-4">Dashboard</h1>
      <div className="grid grid-cols-3 gap-4">
        <div className="rounded-lg border border-border bg-card p-4">
          <h2 className="font-semibold mb-2">Active Devices</h2>
          <p className="text-2xl font-bold text-primary">0</p>
        </div>
        <div className="rounded-lg border border-border bg-card p-4">
          <h2 className="font-semibold mb-2">Total Access Logs</h2>
          <p className="text-2xl font-bold text-primary">0</p>
        </div>
        <div className="rounded-lg border border-border bg-card p-4">
          <h2 className="font-semibold mb-2">Users</h2>
          <p className="text-2xl font-bold text-primary">0</p>
        </div>
      </div>
    </div>
  )
}
