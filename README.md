# ephemeral-devpods

Paste a public GitHub repo, get an auto-expiring dev environment. Parses the repo's
`.devcontainer/devcontainer.json`, spins up a container, and gives you a forwarded dev-server URL
plus a VS Code Remote Tunnel for coding into it.

**Status**: v1. Local development runs on Docker; the Azure Container Instances (ACI) backend is implemented
but not yet deployed anywhere — see `docs/spec.md` for the full design and [Compute setup](#compute-setup) below.

## Stack

- **Backend**: Azure Functions (C#, .NET 10, isolated worker)
- **Frontend**: React + Vite + MUI
- **State**: Azure Table Storage (Azurite locally)
- **Compute**: Docker (via Docker.DotNet) locally, Azure Container Instances in the cloud

## Auth setup

Sign-in is SSO handled by the backend: **Microsoft** (personal and work/school accounts) or **GitHub**.
Both resolve to one user, and each user only sees their own environments. Accounts are created by
signing in with Microsoft; GitHub can sign in only after it has been linked from **Account** settings
while signed in with Microsoft.

You need one OAuth app per provider (both free). The redirect URIs go through the Vite dev server so
the session cookie is set on the same origin as the SPA.

**Microsoft** — [Entra admin center](https://entra.microsoft.com) → App registrations → New registration:
- Supported account types: *Accounts in any organizational directory and personal Microsoft accounts*
- Redirect URI (Web): `http://localhost:5173/api/auth/callback/microsoft`
- Certificates & secrets → New client secret

**GitHub** — [Developer settings](https://github.com/settings/developers) → OAuth Apps → New OAuth App:
- Authorization callback URL: `http://localhost:5173/api/auth/callback/github`
- Generate a client secret

Then add the settings to `backend/EphemeralDevpods.Functions/local.settings.json` (gitignored) under `Values`.
Use `__` in place of `:` in these names:

```json
"Auth__SessionSigningKey": "<random string, 32+ characters>",
"Auth__Microsoft__ClientId": "<application (client) id>",
"Auth__Microsoft__ClientSecret": "<secret value>",
"Auth__GitHub__ClientId": "<client id>",
"Auth__GitHub__ClientSecret": "<client secret>"
```

Optionally `Auth__PublicBaseUrl` (default `http://localhost:5173`) is the origin browsers use to reach the
app; OAuth redirect URIs are built from it. The backend refuses to start if any of the above is missing.

Environments used to be owned by a placeholder `local-dev-user`. Those rows belong to nobody now; to
start clean, reset Azurite: `docker compose -f docker-compose.dev.yml down -v`.

## Compute setup

`Compute__Provider` picks where environments run. It defaults to `Docker`, which needs no configuration.

Creating an environment returns `202` straight away; a queue-triggered worker (`ProvisionEnvironment`) does the
pulling/building and moves it from *Provisioning* to *Running*. This needs the Storage queue endpoint (Azurite exposes it).

**`Aci`** runs each environment as one Azure Container Instances container group with no public IP — you reach it
through the VS Code tunnel. Azure is accessed as a service principal (no managed identity). The app refuses to start
if any of these is missing (and in this mode `AzureWebJobsStorage` must point at real storage, not the emulator):

```json
"Compute__Provider": "Aci",
"Compute__Aci__TenantId": "<tenant id>",
"Compute__Aci__ClientId": "<service principal client id>",
"Compute__Aci__ClientSecret": "<service principal secret>",
"Compute__Aci__SubscriptionId": "<subscription id>",
"Compute__Aci__ResourceGroup": "<runtime resource group holding the container groups>",
"Compute__Aci__Location": "westeurope",
"Compute__Aci__RegistryName": "<container registry name, without .azurecr.io>",
"Compute__Aci__RegistryResourceGroup": "<resource group of the registry>",
"Compute__Aci__PullClientId": "<optional: AcrPull-only service principal>",
"Compute__Aci__PullClientSecret": "<its secret>"
```

The main service principal needs Contributor on the runtime resource group and permission to schedule runs on the
registry. The pull principal is only needed for repos whose devcontainer builds from a Dockerfile (ACR Tasks builds the
image and the container group pulls it with these credentials); give it `AcrPull` on the registry and nothing else.

### Admin access

Sign-up is open, and every ACI environment costs money, so in `Aci` mode only admins can create, start, restart or
extend environments (everyone can still list, stop and delete their own). There is no in-app way to grant it: sign in
once, then set `IsAdmin` to `true` on your row in the `users` table (partition `user`, row key = your `userId`, which
`GET /api/me` returns). Local Docker mode ignores the flag.

## Running locally

Prerequisites: .NET 10 SDK, Docker Desktop, Node + pnpm, Azure Functions Core Tools (`func`), and the
[auth setup](#auth-setup) above.

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

Open `http://localhost:5173`, sign in, paste a public repo URL with a `.devcontainer/devcontainer.json`,
and go.

## Tests

```bash
cd backend
dotnet test
```

Some tests are integration tests requiring Azurite and/or Docker to be running locally.
