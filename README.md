# db-migrator-postgres

PostgreSQL database migration CLI tool for .NET, powered by [InitializeNL.DbMigrator](https://github.com/initialize-nl/db-migrator).

## Install

```bash
dotnet tool install -g InitializeNL.DbMigrator.Postgres.Cli
```

## Usage

```bash
db-migrator-postgres \
    --connection-string "Host=localhost;Database=mydb;Username=postgres;Password=secret" \
    --source ./migrations \
    --execute
```

Or with environment variables:

```bash
export DBMIGRATOR_CONNECTION="Host=localhost;Database=mydb;Username=postgres"
export DBMIGRATOR_PASSWORD="secret"

db-migrator-postgres --source ./migrations --execute
```

## Options

| Option | Description |
|--------|-------------|
| `--connection-string` | Database connection string (or `DBMIGRATOR_CONNECTION` env var) |
| `--password` | Override password (or `DBMIGRATOR_PASSWORD` env var) |
| `--source` | Path to migration scripts directory |
| `--target` | Target migration name (latest when omitted) |
| `--execute` | Apply migrations (default is dry-run) |
| `--dry-run` | Preview mode, no changes applied |
| `--discovery-script` | SQL file for multi-tenant target discovery |
| `--yes` | Skip confirmation prompts |
| `--allow-destructive` | Skip destructive migration confirmation |
| `--no-lock` | Skip distributed locking |
| `--fill-gaps` | Execute missed migrations before the last applied one |
| `--server-parallelism` | Max servers to migrate in parallel (default: 1) |
| `--database-parallelism` | Max databases per server in parallel (default: 1) |

## Migration Scripts

Scripts follow the naming convention: `yyyy-MM-dd_HH-mm-ssZ_description.up.sql` / `.down.sql`

### Script Arguments

```sql
-- arg: destructive
DROP TABLE old_data;
```

```sql
-- arg: irreversible
-- arg: repeat-until-affected-lte 0
DELETE FROM large_table WHERE expired < NOW() LIMIT 10000;
```

## License

[MIT](LICENSE)
