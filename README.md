# ephemeral-devpods

Paste a public GitHub repo, get an auto-expiring dev environment. Parses the repo's
`.devcontainer/devcontainer.json`, spins up a container, and gives you a forwarded dev-server URL
plus a VS Code Remote Tunnel for coding into it.

**Status**: v1, local development only (Docker as the compute backend). Cloud deployment (Azure
Container Instances) is a stub — see `docs/spec.md` for the full design.

## Stack

- **Backend**: Azure Functions (C#, .NET 10, isolated worker)
- **Frontend**: React + Vite + MUI
- **State**: Azure Table Storage (Azurite locally)
- **Compute**: Docker (via Docker.DotNet)

## Running locally

Prerequisites: .NET 10 SDK, Docker Desktop, Node + pnpm, Azure Functions Core Tools (`func`).

```bash
# 1. State store
docker compose -f docker-compose.dev.yml up -d

# 2. Backend
cd backend/EphemeralDevpods.Functions
func start

# 3. Frontend (separate terminal)
cd frontend
pnpm install
pnpm dev
```

Open `http://localhost:5173`, paste a public repo URL with a `.devcontainer/devcontainer.json`,
and go.

## Tests

```bash
cd backend
dotnet test
```

Some tests are integration tests requiring Azurite and/or Docker to be running locally.
