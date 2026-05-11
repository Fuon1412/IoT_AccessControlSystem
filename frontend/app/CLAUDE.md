# Frontend - CLAUDE.md

## Project Info
- **Type**: React + TypeScript
- **Build Tool**: Vite
- **Router**: TanStack Router (file-based routing)
- **Styling**: Tailwind CSS + shadcn components
- **Port**: http://localhost:5173

## Quick Commands
```bash
npm run dev              # Start dev server
npm run build            # Build for production
npm run preview          # Preview production build
npm run lint             # Run ESLint
npm install              # Install dependencies
```

## Key Files
- `src/main.tsx` - App entry point with Router setup
- `src/routes/__root.tsx` - Root layout with navigation
- `src/routes/index.tsx` - Home page
- `src/routes/dashboard.tsx` - Dashboard page
- `vite.config.ts` - Vite configuration with TanStack Router plugin
- `tailwind.config.js` - Tailwind CSS configuration
- `tsconfig.json` - TypeScript configuration

## Directory Structure
```
src/
├── routes/              # TanStack Router file-based routes
│   ├── __root.tsx       # Root layout
│   ├── index.tsx        # Home (/)
│   └── dashboard.tsx    # Dashboard (/dashboard)
├── main.tsx             # Entry point
└── index.css            # Tailwind + CSS variables
```

## Adding Routes
1. Create file in `src/routes/`
2. Use TanStack Router pattern:
```tsx
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/newpage')({
  component: NewPage,
})

function NewPage() {
  return <div>Content</div>
}
```
3. Auto-generated routing via plugin

## Styling
- **Tailwind CSS**: Utility classes
- **Color Variables**: CSS custom properties in tailwind.config.js
- **Dark Mode**: Configured in tailwind.config.js
- **shadcn Components**: Use Tailwind + Radix UI

## Environment Variables
Create `.env` file:
```
VITE_API_URL=https://localhost:7114
```

## Development Tips
1. Dev server hot-reloads automatically
2. Routes are file-based - no manual routing needed
3. Tailwind IntelliSense works in most editors
4. Use TanStack Router DevTools in dev mode
5. Check console for TypeScript errors

## Common Issues
- **Routes not working**: Ensure file is in `src/routes/` directory
- **Tailwind not applying**: Check `tailwind.config.js` content paths
- **API errors**: Verify CORS is enabled in backend
- **Dark mode**: Add `dark` class to root element

## Build
```bash
npm run build           # Creates dist/
npm run preview         # Test production build locally
```

## Deployment
- Built files in `dist/` folder
- Serve as static files from web server
- Set `VITE_API_URL` env var to production backend URL
