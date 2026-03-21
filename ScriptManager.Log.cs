#nullable enable
using Microsoft.Extensions.Logging;

namespace InitializeNL.DbMigrator.Cli;

internal sealed partial class ScriptManager
{
  [LoggerMessage(Level = LogLevel.Information, Message = "Retrieving scripts from source")]
  private partial void LogRetrievingScripts();

  [LoggerMessage(Level = LogLevel.Information, Message = "Target migration: {TargetMigration}")]
  private partial void LogTargetMigration(string targetMigration);

  [LoggerMessage(Level = LogLevel.Information, Message = "Loading SQL migrations from {Directory}")]
  private partial void LogLoadingFromDirectory(string directory);

  [LoggerMessage(Level = LogLevel.Information, Message = "Loading code migrations from {AssemblyPath}")]
  private partial void LogLoadingFromAssembly(string assemblyPath);
}