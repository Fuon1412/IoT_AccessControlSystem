import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/')({
  component: Index,
})

function Index() {
  return (
    <div className="p-2">
      <h1 className="text-3xl font-bold mb-4">Welcome to IoT Access Control System</h1>
      <p className="text-muted-foreground">
        A modern access control management system built with .NET and React.
      </p>
    </div>
  )
}
