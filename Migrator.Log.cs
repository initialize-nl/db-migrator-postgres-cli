#nullable enable
using InitializeNL.DbMigrator.Scripts;
using Microsoft.Extensions.Logging;

namespace InitializeNL.DbMigrator.Cli;

internal sealed partial class Migrator
{
  // Class-level logger (_logger)
  [LoggerMessage(Level = LogLevel.Information, Message = "Running in Dry Run mode")]
  private partial void LogDryRunMode();

  [LoggerMessage(Level = LogLevel.Information, Message = "Running in Real mode")]
  private partial void LogRealMode();

  [LoggerMessage(Level = LogLevel.Information, Message = "Starting migrations for server {Server}")]
  private partial void LogStartingServer(string server);

  [LoggerMessage(Level = LogLevel.Information, Message = "Checking destructive content for {Server}/{Database}")]
  private partial void LogCheckingDestructive(string server, string database);

  [LoggerMessage(Level = LogLevel.Error, Message = "Init failed for {Server}/{Database}: {ErrorMessage}")]
  private partial void LogInitFailed(string server, string database, string errorMessage);

  [LoggerMessage(
    Level = LogLevel.Warning,
    Message = "Destructive script {ScriptName} denied for {Server}/{Database}, aborting")]
  private partial void LogDestructiveDenied(string scriptName, string server, string database);

  [LoggerMessage(Level = LogLevel.Information, Message = "Destructive check passed for {Server}/{Database}")]
  private partial void LogDestructiveCheckPassed(string server, string database);

  // Per-database logger (passed as parameter) — use static partial methods
  [LoggerMessage(Level = LogLevel.Information, Message = "Starting migration")]
  private static partial void LogStartingMigration(ILogger logger);

  [LoggerMessage(Level = LogLevel.Information, Message = "Migration completed")]
  private static partial void LogMigrationCompleted(ILogger logger);

  [LoggerMessage(Level = LogLevel.Error, Message = "Migration failed: {ErrorMessage}")]
  private static partial void LogMigrationError(ILogger logger, Exception ex, string errorMessage);

  [LoggerMessage(Level = LogLevel.Information, Message = "Initializing migration")]
  private static partial void LogInitializing(ILogger logger);

  [LoggerMessage(Level = LogLevel.Information, Message = "Connected to {ConnectionString}")]
  private static partial void LogConnected(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Information, Message = "Pending scripts: {Scripts}")]
  private static partial void LogPendingScripts(ILogger logger, string scripts);

  [LoggerMessage(Level = LogLevel.Error, Message = "Script {ScriptName} failed")]
  private static partial void LogScriptFailed(ILogger logger, Exception ex, string scriptName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Deferred scripts (will re-run next time): {Scripts}")]
  private static partial void LogDeferredScripts(ILogger logger, string scripts);

  [LoggerMessage(Level = LogLevel.Information, Message = "Already up to date")]
  private static partial void LogUpToDate(ILogger logger);

  [LoggerMessage(Level = LogLevel.Error, Message = "Migration failed")]
  private static partial void LogMigrationFailed(ILogger logger, Exception ex);

  [LoggerMessage(Level = LogLevel.Information, Message = "Acquiring lock on {ConnectionString}")]
  private static partial void LogAcquiringLock(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to acquire lock, skipping migration")]
  private static partial void LogLockFailed(ILogger logger);

  [LoggerMessage(Level = LogLevel.Information, Message = "Lock acquired on {ConnectionString}")]
  private static partial void LogLockAcquired(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Information, Message = "[DRY RUN] Lock acquired on {ConnectionString}")]
  private static partial void LogDryRunLockAcquired(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Information, Message = "Locking disabled, skipping lock acquisition")]
  private static partial void LogLockingDisabled(ILogger logger);

  [LoggerMessage(Level = LogLevel.Information, Message = "Lock released on {ConnectionString}")]
  private static partial void LogLockReleased(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Information, Message = "[DRY RUN] Lock released on {ConnectionString}")]
  private static partial void LogDryRunLockReleased(ILogger logger, string connectionString);

  [LoggerMessage(Level = LogLevel.Information, Message = "[DRY RUN] {ScriptName} ({ScriptType})")]
  private static partial void LogDryRunScript(ILogger logger, string scriptName, ScriptType scriptType);

  [LoggerMessage(Level = LogLevel.Information, Message = "Executing {ScriptName} ({ScriptType})")]
  private static partial void LogExecutingScript(ILogger logger, string scriptName, ScriptType scriptType);

  [LoggerMessage(Level = LogLevel.Information, Message = "Rows affected: {Affected} ({Total} total)")]
  private static partial void LogRowsAffected(ILogger logger, int affected, int total);

  [LoggerMessage(Level = LogLevel.Information, Message = "Repeating {ScriptName}, threshold is {MaxAffected} rows")]
  private static partial void LogRepeatingScript(ILogger logger, string scriptName, int maxAffected);

  [LoggerMessage(Level = LogLevel.Information, Message = "Completed {ScriptName}")]
  private static partial void LogScriptCompleted(ILogger logger, string scriptName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Skipped {ScriptName} (empty script)")]
  private static partial void LogSkippedEmptyScript(ILogger logger, string scriptName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Deferred history for {ScriptName}")]
  private static partial void LogDeferredHistory(ILogger logger, string scriptName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Recording {ScriptName} as applied")]
  private static partial void LogRecordingApplied(ILogger logger, string scriptName);

  [LoggerMessage(Level = LogLevel.Information, Message = "Recording {ScriptName} as reverted")]
  private static partial void LogRecordingReverted(ILogger logger, string scriptName);
}