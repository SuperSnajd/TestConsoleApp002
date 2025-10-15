# FinalTest Log Ingest - Implementation Plan

### Tech baseline

- .NET 9 (C# 13) console with `Host` + `BackgroundService`
- Built-in logging only (Console logger)
- Marten v8 to Postgres 16 (schema: `gen4finaltest_testlogs`)
- App runs locally; Postgres/pgAdmin via Docker Compose (separate dev/prod files; optional pgAdmin file)
- No tests (per requirement)

### Project structure

- `Program.cs`: Host bootstrapping, DI, logging, configuration binding
- `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`
- `Options/` configuration classes: `WatcherOptions`, `ProcessingOptions`, `ArchiveOptions`, `DatabaseOptions`, `ParsingOptions`
- `Ingestion/`
  - `FileWatcherService` (BackgroundService): FileSystemWatcher + initial scan
  - `FileQueue`/`DebounceTracker`: ensure stable file before processing
  - `FileIngestor`: orchestrates parse + persist + archive/error routing
- `Parsing/`
  - `FinalTestLogParser`: parses the specific format (decimal comma, culture `sv-SE`)
  - `Models/` data model for parsed content
- `Persistence/`
  - `MartenConfig`: store setup, schema, indexes
  - `Repositories/FinalTestLogRepository`
- `README.md`
- `docker-compose.dev.yml`, `docker-compose.prod.yml`, `docker-compose.pgadmin.yml`

### Configuration (appsettings)

```json
{
  "Watcher": {
    "Path": "C:/cursor/data/incoming",
    "Filter": "*.log",
    "IncludeSubdirectories": false,
    "NotifyFilters": ["FileName", "LastWrite"],
    "InitialScan": true
  },
  "Processing": {
    "StableWaitMs": 1500,
    "MaxConcurrency": 2
  },
  "Archive": {
    "SuccessPath": "C:/cursor/data/archive",
    "ErrorPath": "C:/cursor/data/error",
    "OnSuccess": "Move",
    "OnError": "Move",
    "ArchiveNamePatternOnConflict": "{name}-{yyyyMMdd_HHmmss}{ext}",
    "PreserveSubfolders": false
  },
  "Database": {
    "ConnectionString": "Host=localhost;Port=5433;Database=finaltest_dev;Username=postgres;Password=postgres;",
    "Schema": "gen4finaltest_testlogs",
    "AutoCreate": "All"
  },
  "Parsing": {
    "Culture": "sv-SE",
    "Timezone": "Local",
    "IdentityFormat": "{DeviceSerial}-{Date:yyyyMMdd}-{Time:HHmmss}"
  }
}
```

- Production appsettings overrides: DB port 5432; DB name `finaltest_prod`; watcher/paths as needed

### Docker Compose

- `docker-compose.dev.yml`: Postgres 16 at 5433, named volume, DB `finaltest_dev`
- `docker-compose.prod.yml`: Postgres 16 at 5432, DB `finaltest_prod`
- `docker-compose.pgadmin.yml`: pgAdmin4 service only; start on demand
- All compose files define healthchecks

### Marten setup

- Use schema `gen4finaltest_testlogs`
- AutoCreate during development; restricted in production (`CreateOrUpdate` or migrations later)
- Document type: `FinalTestLog` with deterministic string `Id` based on DeviceSerial + Date + Time (local time interpretation)
- Additional indexes: `DeviceSerial`, `TimestampLocal`
- Store `ContentSha256` for duplicate/content-change evaluation
- Replacement policy: if same Id but different content => keep latest; record replacement metadata (`ReplacedHistory`)

### Data model (key fields)

- `FinalTestLog` (document)
  - `Id` (string): `{DeviceSerial}-{yyyyMMdd}-{HHmmss}`
  - Source metadata: `SourceFileName`, `SourceFilePath`, `ContentSha256`, `FileCreatedUtc`, `IngestedAtUtc`, `ArchivedToPath`
  - Identity fields: `DeviceSerial`, `Date` (date), `Time` (time), `TimestampLocal` (DateTime)
  - Header fields: `OperatorId`, `McuSerial`, `RfUnitSerial`, `ReaderType`, `TagmodVersion`, `RfSweepBuildDate`, `ConfigLimitFileVersion`, `ReadLevel`
  - Test summary: `DurationSeconds`, `OverallPass`
  - `CurrentResult`: `{ MeasuredCurrentA, LimitLowA, LimitHighA, Pass }`
  - Repeating `SignalStrengthBlocks[]`:
    - `OutputPower`, `Pass`, `Comparison` (LimitLow/High/Measurement/Unit/Pass)
    - `FrequencyArray[]`
    - `SignalStrengthMatrix` (rows×cols ints)
    - `AverageSignalStrength[]`
    - `AttenuationArray[]`
  - `RawText` (full file contents)
  - Replacement: `Version` (int), `ReplacesDocumentId` (optional), `ReplacedHistory[]`

### Parsing rules

- Split into headers, current result, repeated blocks starting at "Signal Strength Data"
- Culture: `sv-SE` for numbers (decimal comma) and date/time parsing; times are interpreted in local timezone for identity
- Matrices and arrays parsed by sections; tolerate variable whitespace/tabs; ignore blank lines
- Robust line matching with anchors; log warnings for non-critical anomalies

### Ingest flow

1. On startup: initial scan of watch folder (non-recursive) for `*.log`; enqueue all
2. File events: Created/Changed/Renamed trigger; filtered by extension
3. Debounce: wait until file size stable for `StableWaitMs` before processing
4. Processing pipeline (bounded concurrency):

   - Open with shared read; read content
   - Parse into `FinalTestLog` + raw text
   - Build deterministic `Id` from DeviceSerial + Date + Time
   - Compute `ContentSha256`
   - Upsert logic:
     - If no existing doc: insert
     - If existing with same hash: skip (log as duplicate)
     - If existing with different hash: update to latest, increment `Version`, push prior hash/info to `ReplacedHistory`
   - On success: move file to `Archive.SuccessPath` with conflict suffix pattern
   - On parse error: move to `Archive.ErrorPath`; log error

### Logging

- Structured logs with event IDs for: queued, stable, parsed, inserted, updated, archived, duplicate, error
- Summaries at startup and graceful shutdown

### README.md

- Simple quickstart with: prerequisites, compose up/down for dev/prod DB, optional pgAdmin, configuration keys, how to run the app, folder layout, and troubleshooting

### Security/operational

- Postgres credentials from compose env (dev defaults ok); remind to change in prod
- Healthchecks in compose; backoff retries in app for DB connection on startup
- Graceful cancellation; flush pending operations on shutdown

### To-dos

- [ ] Create .NET 9 console host with BackgroundService and logging
- [ ] Add options classes and bind appsettings with validation
- [ ] Create docker-compose.dev.yml for Postgres 16 on 5433 with volume
- [ ] Create docker-compose.prod.yml for Postgres 16 on 5432 with volume
- [ ] Create docker-compose.pgadmin.yml for on-demand pgAdmin
- [ ] Configure Marten v8 with schema gen4finaltest_testlogs, AutoCreate dev
- [ ] Implement FinalTestLog document and nested models
- [ ] Implement FinalTestLogParser using sv-SE culture and robust section parsing
- [ ] Implement non-recursive *.log watcher with initial scan
- [ ] Add stable-size debounce and bounded concurrency queue
- [ ] Implement id/hash, dedupe, replacement history, persistence
- [ ] Move success to archive, failures to error with timestamp suffix
- [ ] Write README with setup, compose commands, config, run steps


