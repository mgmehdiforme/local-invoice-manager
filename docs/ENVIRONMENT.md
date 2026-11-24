# Development Environment

Checked on 2026-11-24.

## System
| Item | Value | Status |
|---|---|---|
| OS | Ubuntu 26.04.1 LTS, x86_64, 4 CPU, 22 GB RAM | OK |
| Disk `/media/Projects` (code) | 7.2 GB free (81% used) | OK, but watch it |
| Disk `/` (Docker images) | 138 GB free | OK |

## Tools
| Tool | Version | Status |
|---|---|---|
| .NET SDK / ASP.NET runtime | 10.0.402 / 10.0.13 | OK; test webapi + EF Core SQLite build passed |
| dotnet-ef | 10.0.13 | OK |
| csharp-ls (C# language server) | 0.28.0 | Installed (0.29.0 package is broken upstream) |
| Node / npm / pnpm | 24.22 / 11.19 / 12.14 | OK; test Vite React-TS build passed |
| Git | 2.53 | OK |
| Docker / Compose | 29.1.3 / 2.40.3 | OK; user is in `docker` group (no sudo) |
| Docker buildx | 0.30.1 | Installed to `~/.docker/cli-plugins` (checksum verified) |
| sqlite3 CLI | — | Not installed (needs sudo). Not required: the app does backups itself |

Pre-pulled base images: `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `node:24-alpine`.

## Port 8080 is already taken
`n8n-sandbox-api-1` publishes `0.0.0.0:8080`. Decision (2026-11-24): the app uses host port **8090** (`APP_PORT=8090` in `.env`), bound to `127.0.0.1:8090`. The container still listens on 8080 internally. App URL: `http://localhost:8090`.

## Free tools, reachable from Iran
Every tool and library is free and open source and needs no account. Some registries (Docker Hub, MCR, parts of GitHub) may be blocked in some countries. To limit that risk:

- The app runs fully offline: fonts (Vazirmatn), icons and shadcn/ui components are copied into the repo, with no CDN at runtime.
- Dependencies are pinned with lock files (`package-lock.json`, `packages.lock.json`).
- Base images are cached locally after the first pull, so `docker compose build` works offline afterwards.
- Network access is needed only while adding new packages.

## Project drive is NTFS (fuseblk)
`/media/Projects` is an NTFS drive mounted through FUSE. Files on it have no execute permission. Consequences:
- Run the API with `dotnet <dll>` (`UseAppHost=false` is set), not with a native launcher.
- npm scripts call tools through `node node_modules/...`; TypeScript is pinned to 6.x, because TypeScript 7 is a native binary.
- The Dockerfile copies files with `--chmod=755`.
- The SQLite database for Docker lives on a Docker volume (ext4), not on this drive, because WAL locking on FUSE/NTFS is unreliable.
