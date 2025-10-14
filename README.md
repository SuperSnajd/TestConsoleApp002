# FinalTest Log Ingest

A .NET 9 console application that monitors a directory for FinalTest log files, parses them, stores them in PostgreSQL using Marten event sourcing, and archives the processed files.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Prerequisites](#prerequisites)
- [Quick Start](#quick-start)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Running the Application](#running-the-application)
- [Docker Compose Services](#docker-compose-services)
- [Troubleshooting](#troubleshooting)
- [Production Deployment](#production-deployment)

## Overview

This application provides automated ingestion and processing of FinalTest log files from Gen4 FinalTest systems. It:

- Watches a configured directory for `*.log` files
- Parses log files using Swedish locale (sv-SE) for decimal parsing
- Stores parsed data in PostgreSQL using Marten document database
- Handles duplicate detection and file versioning
- Archives successfully processed files
- Routes failed files to an error directory

## Features

- **Real-time File Watching**: Monitors directories for new or changed log files
- **Debouncing**: Waits for files to stabilize before processing
- **Duplicate Detection**: Uses SHA-256 hashing to detect duplicate content
- **Version Management**: Tracks file replacements and maintains version history
- **Robust Parsing**: Handles the FinalTest format with decimal comma notation (sv-SE culture)
- **Event Sourcing**: Leverages Marten v8 for PostgreSQL document storage
- **Structured Logging**: Comprehensive logging with event IDs for monitoring
- **Graceful Shutdown**: Handles cancellation and flushes pending operations

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for PostgreSQL)
- PostgreSQL 16 (provided via Docker Compose)

## Quick Start

### 1. Clone and Build

```bash
cd FinalTestLogIngest
dotnet build
```

### 2. Start the Development Database

```bash
# Start PostgreSQL on port 5434
docker-compose -f docker-compose.dev.yml up -d

# Verify database is running
docker ps
```

### 3. Configure Watch Directories

Edit `appsettings.Development.json` or use the default paths:

- **Watch Path**: `C:/data/incoming` (create this directory)
- **Archive Path**: `C:/data/archive` (will be created automatically)
- **Error Path**: `C:/data/error` (will be created automatically)

```bash
# Windows
mkdir C:\data\incoming
mkdir C:\data\archive
mkdir C:\data\error

# Linux/Mac
mkdir -p /data/incoming /data/archive /data/error
```

### 4. Run the Application

```bash
# Development mode (uses appsettings.Development.json)
dotnet run --project FinalTestLogIngest --environment Development

# Production mode (uses appsettings.Production.json)
dotnet run --project FinalTestLogIngest --environment Production
```

### 5. Test the Application

Drop a test log file into the watch directory:

```bash
# Copy the example file
copy "Example file\Example testlog file.txt" C:\data\incoming\test.log

# Or on Linux/Mac
cp "Example file/Example testlog file.txt" /data/incoming/test.log
```

Watch the console output for processing logs.

## Configuration

Configuration is managed through `appsettings.json` files with environment-specific overrides.

### Key Configuration Sections

#### Watcher

Configures the file system watcher:

```json
{
  "Watcher": {
    "Path": "C:/data/incoming",
    "Filter": "*.log",
    "IncludeSubdirectories": false,
    "NotifyFilters": ["FileName", "LastWrite"],
    "InitialScan": true
  }
}
```

- **Path**: Directory to monitor for log files
- **Filter**: File pattern to watch (default: `*.log`)
- **IncludeSubdirectories**: Whether to watch subdirectories (default: false)
- **NotifyFilters**: File system changes to monitor
- **InitialScan**: Scan directory for existing files on startup (default: true)

#### Processing

Controls file processing behavior:

```json
{
  "Processing": {
    "StableWaitMs": 1500,
    "MaxConcurrency": 2
  }
}
```

- **StableWaitMs**: Milliseconds to wait for file size to stabilize before processing
- **MaxConcurrency**: Maximum number of files to process concurrently

#### Archive

Configures file archiving after processing:

```json
{
  "Archive": {
    "SuccessPath": "C:/data/archive",
    "ErrorPath": "C:/data/error",
    "OnSuccess": "Move",
    "OnError": "Move",
    "ArchiveNamePatternOnConflict": "{name}-{yyyyMMdd_HHmmss}{ext}",
    "PreserveSubfolders": false
  }
}
```

- **SuccessPath**: Destination for successfully processed files
- **ErrorPath**: Destination for files that failed processing
- **OnSuccess/OnError**: Action to take (`Move` or `Copy`)
- **ArchiveNamePatternOnConflict**: Pattern for renaming conflicting files
- **PreserveSubfolders**: Maintain subdirectory structure in archive

#### Database

PostgreSQL connection and Marten configuration:

```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5434;Database=finaltest_logs;Username=finaltest_user;Password=finaltest_dev_password;",
    "Schema": "gen4finaltest_testlogs",
    "AutoCreate": "All"
  }
}
```

- **ConnectionString**: PostgreSQL connection string
- **Schema**: Database schema for Marten documents
- **AutoCreate**: Schema creation mode (`All`, `CreateOrUpdate`, `None`)

#### Parsing

Controls log file parsing behavior:

```json
{
  "Parsing": {
    "Culture": "sv-SE",
    "Timezone": "Local",
    "IdentityFormat": "{DeviceSerial}-{Date:yyyyMMdd}-{Time:HHmmss}"
  }
}
```

- **Culture**: Culture for number/date parsing (use `sv-SE` for decimal comma)
- **Timezone**: Timezone interpretation for timestamps
- **IdentityFormat**: Pattern for generating document IDs

### Environment-Specific Configuration

**Development** (`appsettings.Development.json`):
- Database on port **5434**
- Debug logging enabled
- AutoCreate set to `All`

**Production** (`appsettings.Production.json`):
- Database on port **5432**
- Information logging only
- AutoCreate set to `CreateOrUpdate`
- Update paths for production environment

## Project Structure

```
FinalTestLogIngest/
├── Program.cs                          # Application entry point and host configuration
├── appsettings.json                    # Base configuration
├── appsettings.Development.json        # Development overrides
├── appsettings.Production.json         # Production overrides
├── FinalTestLogIngest.csproj          # Project file
├── Options/                            # Configuration option classes
│   ├── WatcherOptions.cs
│   ├── ProcessingOptions.cs
│   ├── ArchiveOptions.cs
│   ├── DatabaseOptions.cs
│   └── ParsingOptions.cs
├── Ingestion/                          # File watching and ingestion pipeline
│   ├── FileWatcherService.cs          # BackgroundService for file system watching
│   ├── FileQueue.cs                   # Queue for managing file processing
│   ├── DebounceTracker.cs            # Debouncing logic for file stability
│   └── FileIngestor.cs               # Orchestrates parsing, persistence, archiving
├── Parsing/                            # Log file parsing
│   ├── FinalTestLogParser.cs          # Parser implementation
│   └── Models/
│       └── FinalTestLog.cs           # Document model and nested types
├── Persistence/                        # Database and repository
│   ├── MartenConfig.cs                # Marten configuration
│   └── Repositories/
│       └── FinalTestLogRepository.cs  # Repository for FinalTestLog documents
└── Logging/                            # Logging infrastructure
    ├── LogEvents.cs                   # Structured log event IDs
    └── IngestionMetrics.cs           # Metrics tracking
```

## Running the Application

### Development Mode

```bash
# Start the development database
docker-compose -f docker-compose.dev.yml up -d

# Run the application
cd FinalTestLogIngest
dotnet run --environment Development
```

### Production Mode

```bash
# Start the production database
docker-compose -f docker-compose.prod.yml up -d

# Run the application
cd FinalTestLogIngest
dotnet run --environment Production
```

### Stopping the Application

Press `Ctrl+C` in the console. The application will:
1. Stop watching for new files
2. Finish processing any files in the queue
3. Log shutdown statistics
4. Exit gracefully

## Docker Compose Services

### Development Database (`docker-compose.dev.yml`)

PostgreSQL 16 on port **5434**:

```bash
# Start
docker-compose -f docker-compose.dev.yml up -d

# Stop
docker-compose -f docker-compose.dev.yml down

# View logs
docker-compose -f docker-compose.dev.yml logs -f

# Stop and remove volumes (destroys data)
docker-compose -f docker-compose.dev.yml down -v
```

**Connection Details**:
- Host: `localhost`
- Port: `5434`
- Database: `finaltest_logs`
- Username: `finaltest_user`
- Password: `finaltest_dev_password`

### Production Database (`docker-compose.prod.yml`)

PostgreSQL 16 on port **5432**:

```bash
# Set password before starting
export POSTGRES_PASSWORD=your_secure_password

# Start
docker-compose -f docker-compose.prod.yml up -d

# Stop
docker-compose -f docker-compose.prod.yml down
```

**Connection Details**:
- Host: `localhost`
- Port: `5432`
- Database: `finaltest_logs`
- Username: `finaltest_user`
- Password: Set via `POSTGRES_PASSWORD` environment variable

### pgAdmin (Optional) (`docker-compose.pgadmin.yml`)

Web-based PostgreSQL administration tool:

```bash
# Start pgAdmin
docker-compose -f docker-compose.pgadmin.yml up -d

# Access at http://localhost:5050
# Email: admin@example.com
# Password: admin
```

**Adding Database Server in pgAdmin**:
1. Right-click "Servers" → "Register" → "Server"
2. General tab: Name = `FinalTest Dev`
3. Connection tab:
   - Host: `host.docker.internal` (Windows/Mac) or `172.17.0.1` (Linux)
   - Port: `5434` (dev) or `5432` (prod)
   - Database: `finaltest_logs`
   - Username: `finaltest_user`
   - Password: `finaltest_dev_password`

### Using Multiple Services

```bash
# Start dev database and pgAdmin together
docker-compose -f docker-compose.dev.yml -f docker-compose.pgadmin.yml up -d

# Stop all services
docker-compose -f docker-compose.dev.yml -f docker-compose.pgadmin.yml down
```

## Troubleshooting

### Database Connection Issues

**Problem**: Application can't connect to PostgreSQL

**Solutions**:
```bash
# Check if PostgreSQL container is running
docker ps

# Check container logs
docker logs finaltest-postgres-dev

# Verify port is not in use
netstat -an | findstr 5434    # Windows
netstat -an | grep 5434       # Linux/Mac

# Test connection manually
psql -h localhost -p 5434 -U finaltest_user -d finaltest_logs
```

### File Not Being Processed

**Problem**: Files in watch directory are not being ingested

**Checklist**:
1. Verify watch path exists and is correct in configuration
2. Check file extension matches filter (default: `*.log`)
3. Ensure file is not locked by another process
4. Check application logs for errors
5. Verify debounce time has elapsed (default: 1500ms)

**Common Issues**:
- File still being written (wait for stability)
- Incorrect file permissions
- File already processed (check for duplicate in database)

### Parsing Errors

**Problem**: Files moved to error directory

**Solutions**:
```bash
# Check application logs for parsing errors
# Look for log events with EventId 4050-4059

# Verify file format matches expected FinalTest format
# Check for:
# - Correct decimal separator (comma for sv-SE)
# - Required fields present (Device Serial, Date, Time, etc.)
# - Valid date/time formats
```

### Schema/Migration Issues

**Problem**: Marten schema errors on startup

**Solutions**:
```bash
# Development: Drop and recreate schema
# Connect to database and run:
DROP SCHEMA IF EXISTS gen4finaltest_testlogs CASCADE;

# Restart application (AutoCreate will recreate schema)

# Production: Use CreateOrUpdate mode or manual migrations
```

### Disk Space Issues

**Problem**: Archive directory filling up

**Solutions**:
- Implement log rotation/cleanup policy
- Move old archives to long-term storage
- Compress archived files
- Consider using `Copy` instead of `Move` with external cleanup

### Performance Issues

**Problem**: Slow processing of files

**Tuning**:
1. Increase `MaxConcurrency` in processing options
2. Decrease `StableWaitMs` if files are from reliable sources
3. Optimize database indexes (already configured)
4. Use SSD for watch/archive directories
5. Increase PostgreSQL resources (Docker memory/CPU limits)

### Logs Not Appearing

**Problem**: Missing or insufficient logging

**Solutions**:
```json
// Increase log level in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "FinalTestLogIngest": "Debug"
    }
  }
}
```

## Production Deployment

### Pre-Deployment Checklist

- [ ] Update `appsettings.Production.json` with production values
- [ ] Set secure PostgreSQL password via environment variable
- [ ] Create production watch, archive, and error directories
- [ ] Set appropriate file system permissions
- [ ] Configure log rotation/archiving policy
- [ ] Set up monitoring and alerting
- [ ] Test disaster recovery procedures
- [ ] Document backup schedule

### Production Configuration Changes

**Database**:
```json
{
  "Database": {
    "ConnectionString": "Host=localhost;Port=5432;Database=finaltest_logs;Username=finaltest_user;Password=changeme;",
    "Schema": "gen4finaltest_testlogs",
    "AutoCreate": "CreateOrUpdate"  // Or "None" for manual migrations
  }
}
```

**Paths** (Linux/Docker):
```json
{
  "Watcher": {
    "Path": "/data/incoming"
  },
  "Archive": {
    "SuccessPath": "/data/archive",
    "ErrorPath": "/data/error"
  }
}
```

**Logging**:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### Running as a Service

**Windows Service**:
Use [NSSM](https://nssm.cc/) or [Windows Service Wrapper](https://github.com/winsw/winsw)

**Linux systemd**:
```ini
[Unit]
Description=FinalTest Log Ingest Service
After=network.target postgresql.service

[Service]
Type=notify
User=finaltestuser
WorkingDirectory=/opt/finaltest-ingest
ExecStart=/usr/bin/dotnet /opt/finaltest-ingest/FinalTestLogIngest.dll
Restart=on-failure
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

**Docker Container**:
```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY ./bin/Release/net9.0/publish/ .
ENTRYPOINT ["dotnet", "FinalTestLogIngest.dll"]
```

### Security Considerations

1. **Database Credentials**: Never commit passwords to source control
2. **File Permissions**: Restrict access to watch/archive directories
3. **Network Security**: Use firewall rules to restrict database access
4. **Monitoring**: Implement log aggregation and monitoring
5. **Backup**: Regular backups of PostgreSQL data volume
6. **Updates**: Keep .NET runtime and dependencies updated

### Monitoring

**Key Metrics to Monitor**:
- Files processed per hour
- Processing errors and error rate
- Database connection pool status
- Disk space on watch/archive paths
- Application memory/CPU usage
- Database query performance

**Log Events to Alert On**:
- `EventId 4050-4059`: Parsing/processing errors
- `EventId 5000-5009`: Database errors
- Application crashes or restarts
- Disk space warnings

## Data Model

The `FinalTestLog` document includes:

- **Identity**: Device Serial, Date, Time, computed document ID
- **Source Metadata**: File path, SHA-256 hash, ingestion timestamp
- **Header Fields**: Operator ID, MCU Serial, RF Unit Serial, Reader Type, versions
- **Test Summary**: Duration, Overall Pass/Fail
- **Current Result**: Measured current with limits
- **Signal Strength Blocks**: Multiple blocks with output power, frequency arrays, matrices, attenuation
- **Version History**: Tracks file replacements and changes
- **Raw Text**: Complete original file contents

## Support

For issues or questions:
1. Check the [Troubleshooting](#troubleshooting) section
2. Review application logs for error details
3. Verify configuration matches your environment
4. Check Docker container logs for database issues

## License

[Specify your license here]

## Version History

- **1.0.0** (Initial Release)
  - File watching and ingestion pipeline
  - FinalTest log format parsing (sv-SE culture)
  - PostgreSQL storage with Marten v8
  - Duplicate detection and versioning
  - Archiving and error handling
  - Structured logging with event IDs

