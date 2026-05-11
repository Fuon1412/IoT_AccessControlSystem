# IoT Access Control System Setup

## Backend (.NET Web API)

### Prerequisites
- .NET 8.0 SDK
- SQL Server (or configure another database provider)

### Quick Start
```bash
cd backend/IoTAccessAPI
dotnet restore
dotnet run
```

The API will be available at `https://localhost:7114` (or the port shown in console)

### API Endpoints
- `GET /api/health` - Health check
- `GET /api/devices` - Get all devices
- `GET /api/users` - Get all users
- `GET /api/access-logs` - Get access logs

### CORS Configuration
CORS is enabled for development at:
- `http://localhost:5173` (Vite dev server)
- `http://localhost:3000`

## Frontend (React + TanStack Router + Tailwind + shadcn)

### Prerequisites
- Node.js 18+ 
- npm or yarn

### Quick Start
```bash
cd frontend/app
npm install
npm run dev
```

The frontend will be available at `http://localhost:5173`

### Project Structure
```
src/
├── routes/           # TanStack Router routes
│   ├── __root.tsx   # Root layout
│   ├── index.tsx    # Home page
│   └── dashboard.tsx # Dashboard page
├── index.css        # Tailwind + shadcn styles
└── main.tsx         # App entry point
```

### Available Scripts
- `npm run dev` - Start development server
- `npm run build` - Build for production
- `npm run preview` - Preview production build
- `npm run lint` - Run ESLint

## Development Tips

### Adding New Routes
Create a new file in `src/routes/`:
```tsx
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/newpage')({
  component: NewPage,
})

function NewPage() {
  return <div>New Page Content</div>
}
```

### Using Tailwind + shadcn Components
All components use Tailwind CSS classes. Import as needed:
```tsx
<div className="rounded-lg border border-border bg-card p-4">
  Content here
</div>
```

### Environment Variables
Create `.env` files in respective directories:

**Backend** - `backend/IoTAccessAPI/.env`:
```
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=https://localhost:7114
ConnectionString=Server=.;Database=IoTAccessDb;Integrated Security=True;
```

**Frontend** - `frontend/app/.env`:
```
VITE_API_URL=https://localhost:7114
```

## Running Both Services
1. Terminal 1: `cd backend/IoTAccessAPI && dotnet run`
2. Terminal 2: `cd frontend/app && npm run dev`

Access the frontend at http://localhost:5173
