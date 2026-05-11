import { createRootRoute, Link, Outlet } from '@tanstack/react-router'
import { TanStackRouterDevtools } from '@tanstack/router-devtools'

export const Route = createRootRoute({
  component: () => (
    <>
      <div className="flex gap-2 border-b border-border p-2">
        <Link to="/" className="[&.active]:font-bold hover:underline">
          Home
        </Link>
        <Link to="/dashboard" className="[&.active]:font-bold hover:underline">
          Dashboard
        </Link>
      </div>
      <Outlet />
      <TanStackRouterDevtools />
    </>
  ),
})
