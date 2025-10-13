<!-- db70ae00-9ac9-435d-89a2-5e98a430f6ee 7a044f14-345e-4bdf-bbed-2cd82dc11064 -->
# FinalTest Log Ingest — Task-Based Plan

### Conventions

- Use .NET 9 (C# 13) console host with `BackgroundService` and built-in console logging.
- Use Marten v8 on Postgres 16 (schema `gen4finaltest_testlogs`).
- Dev DB on port 5433; Prod DB on 5432.
- Culture for parsing is `sv-SE`; identity timestamp interpreted in local time.

## Phase 0 — Initialize Repository

- [x] Phase 0 complete
- [x] Create test branch before implementing: `chore/repo-setup` (from `main`)
- [x] Tasks
  - [x] Initialize repo: `git init`, set default branch `main`
  - [x] Add `.gitignore` (for .NET) and commit
  - [ ] Add remote (if applicable) and push initial commit
- [x] Acceptance criteria: repo initialized; `git status` clean; if solution exists, `dotnet build` has 0 errors
- [x] Merge after completion: merge `chore/repo-setup` into `main` (PR), delete branch

## Phase 1 — Project scaffolding

- [ ] Phase 1 complete
- [ ] Create test branch before implementing: `feature/phase-1-scaffolding`
- [ ] Tasks
  - [ ] Create/verify console host in `Program.cs` with `Host.CreateDefaultBuilder` and DI
  - [ ] Add `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`
  - [ ] Add `Options/`: `WatcherOptions`, `ProcessingOptions`, `ArchiveOptions`, `DatabaseOptions`, `ParsingOptions`
  - [ ] Bind options with validation in `Program.cs`
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-1-scaffolding` into `main`, delete branch

## Phase 2 — Infrastructure: Postgres & Marten

- [ ] Phase 2 complete
- [ ] Create test branch before implementing: `feature/phase-2-infra-marten`
- [ ] Tasks
  - [ ] Add `docker-compose.dev.yml` (Postgres 16 @ 5433, volume, healthcheck)
  - [ ] Add `docker-compose.prod.yml` (Postgres 16 @ 5432, healthcheck)
  - [ ] Add `docker-compose.pgadmin.yml` (optional pgAdmin)
  - [ ] Create `Persistence/MartenConfig.cs` (connection string, schema, AutoCreate dev)
  - [ ] Add indexes for `DeviceSerial` and `TimestampLocal`
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-2-infra-marten` into `main`, delete branch

## Phase 3 — Data model & repository

- [ ] Phase 3 complete
- [ ] Create test branch before implementing: `feature/phase-3-models-repo`
- [ ] Tasks
  - [ ] Create `Parsing/Models/FinalTestLog.cs` and nested types
  - [ ] Include: `Id`, source metadata, identity fields, header fields, test summary, `CurrentResult`, repeating blocks, `RawText`, replacement fields
  - [ ] Create `Persistence/Repositories/FinalTestLogRepository.cs` with insert/find/upsert logic
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-3-models-repo` into `main`, delete branch

## Phase 4 — Parser

- [ ] Phase 4 complete
- [ ] Create test branch before implementing: `feature/phase-4-parser`
- [ ] Tasks
  - [ ] Create `Parsing/FinalTestLogParser.cs`
  - [ ] Robust section parsing using `sv-SE` culture (decimal comma), tolerant whitespace
  - [ ] Parse headers, current result, repeated “Signal Strength Data” blocks, arrays, matrices
  - [ ] Build deterministic `Id` from DeviceSerial + Date + Time (local time)
  - [ ] Return parsed `FinalTestLog` + raw text
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-4-parser` into `main`, delete branch

## Phase 5 — Ingestion pipeline

- [ ] Phase 5 complete
- [ ] Create test branch before implementing: `feature/phase-5-ingestion`
- [ ] Tasks
  - [ ] Create `Ingestion/FileQueue.cs` and `Ingestion/DebounceTracker.cs`
  - [ ] Create `Ingestion/FileWatcherService.cs` (BackgroundService) using `FileSystemWatcher`
  - [ ] Initial non-recursive scan for `*.log` on startup
  - [ ] Filter Created/Changed/Renamed; enqueue files
  - [ ] Debounce until size stable (`Processing.StableWaitMs`)
  - [ ] Create `Ingestion/FileIngestor.cs` to orchestrate: read → parse → compute hash → persist → archive
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-5-ingestion` into `main`, delete branch

## Phase 6 — Persistence rules

- [ ] Phase 6 complete
- [ ] Create test branch before implementing: `feature/phase-6-persistence-rules`
- [ ] Tasks
  - [ ] Compute `ContentSha256` for raw text
  - [ ] Dedupe: if existing doc has same hash, skip as duplicate
  - [ ] Replacement: if same `Id` but different hash, update latest, increment `Version`, push prior to `ReplacedHistory`
  - [ ] Ensure additional indexes exist; tune Marten session usage
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-6-persistence-rules` into `main`, delete branch

## Phase 7 — Archiving & error handling

- [ ] Phase 7 complete
- [ ] Create test branch before implementing: `feature/phase-7-archiving-errors`
- [ ] Tasks
  - [ ] On success: move to `Archive.SuccessPath` using `{name}-{yyyyMMdd_HHmmss}{ext}`
  - [ ] On parse/persist error: move to `Archive/ErrorPath`
  - [ ] Respect `Archive.PreserveSubfolders`
  - [ ] Use shared read file IO and careful exception handling
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-7-archiving-errors` into `main`, delete branch

## Phase 8 — Logging & observability

- [ ] Phase 8 complete
- [ ] Create test branch before implementing: `feature/phase-8-logging`
- [ ] Tasks
  - [ ] Structured logs with event IDs for: queued, stable, parsed, inserted, updated, archived, duplicate, error
  - [ ] Startup/shutdown summaries; log counts
- [ ] Acceptance criteria: `dotnet build` completes with no errors
- [ ] Merge after completion: merge `feature/phase-8-logging` into `main`, delete branch

## Phase 9 — Docs & runbook

- [ ] Phase 9 complete
- [ ] Create test branch before implementing: `docs/phase-9-readme`
- [ ] Tasks
  - [ ] Write `README.md` with prerequisites, compose up/down, config keys, how to run, layout, troubleshooting
  - [ ] Add notes on production overrides (DB 5432, database name, credentials)
- [ ] Acceptance criteria: project still builds with 0 errors; docs added
- [ ] Merge after completion: merge `docs/phase-9-readme` into `main`, delete branch

## Phase 10 — Manual verification

- [ ] Phase 10 complete
- [ ] Create test branch before implementing: `release/phase-10-verification`
- [ ] Tasks
  - [ ] Start dev DB via `docker-compose.dev.yml`
  - [ ] Run the app; drop sample `.log` files in the watch folder
  - [ ] Verify documents in Postgres via Marten/pgAdmin; confirm archive/error moves
- [ ] Acceptance criteria: `dotnet build` completes with no errors; verification steps executed
- [ ] Merge after completion: merge `release/phase-10-verification` into `main`; tag release if desired

### Minimal file map (key files)

- `Program.cs`
- `appsettings*.json`
- `Options/*.cs`
- `Ingestion/FileWatcherService.cs`, `Ingestion/FileQueue.cs`, `Ingestion/DebounceTracker.cs`, `Ingestion/FileIngestor.cs`
- `Parsing/FinalTestLogParser.cs`, `Parsing/Models/*.cs`
- `Persistence/MartenConfig.cs`, `Persistence/Repositories/FinalTestLogRepository.cs`
- `docker-compose.dev.yml`, `docker-compose.prod.yml`, `docker-compose.pgadmin.yml`
- `README.md`


