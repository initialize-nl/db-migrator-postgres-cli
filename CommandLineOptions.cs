#nullable enable
using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace InitializeNL.DbMigrator.Cli;

[SuppressMessage(
  "Performance",
  "CA1812:Avoid uninstantiated internal classes",
  Justification = "Instantiated by CommandLineParser")]
internal sealed class CommandLineOptions
{
  [Option("dry-run", HelpText = "Preview mode, no changes applied (default).")]
  public bool DryRun { get; set; } = true;

  [Option("execute", HelpText = "Apply migrations for real.")]
  public bool Execute
  {
    get => !DryRun;
    set => DryRun = !value;
  }

  [Option(
    "connection-string",
    Required = false,
    HelpText = "Database connection string. Falls back to DBMIGRATOR_CONNECTION env var.")]
  public string ConnectionString { get; set; } = string.Empty;

  [Option(
    "password",
    Required = false,
    HelpText = "Override the password in the connection string. Falls back to DBMIGRATOR_PASSWORD env var.")]
  public string Password { get; set; } = string.Empty;

  [Option("source", Required = false, HelpText = "Path to the migration scripts directory.")]
  public string Source { get; set; } = string.Empty;

  [Option("assembly", Required = false, HelpText = "Path to a .dll containing CodeMigration classes with [Migration] attributes.")]
  public string Assembly { get; set; } = string.Empty;

  [Option("target", Required = false, HelpText = "Target migration name (latest when omitted).")]
  public string Target { get; set; } = string.Empty;

  [Option(
    "discovery-script",
    Required = false,
    HelpText = "SQL file that returns connection targets (columns: server, connection_string).")]
  public string DiscoveryScript { get; set; } = string.Empty;

  [Option("no-lock", Required = false, HelpText = "Skip distributed locking (use with caution).", Default = false)]
  public bool NoLock { get; set; }

  [Option("yes", Required = false, HelpText = "Skip interactive confirmation prompts.", Default = false)]
  public bool Yes { get; set; }

  [Option(
    "allow-destructive",
    Required = false,
    HelpText = "Skip confirmation for destructive migrations.",
    Default = false)]
  public bool AllowDestructive { get; set; }

  [Option(
    "fill-gaps",
    Required = false,
    HelpText = "Execute missed migrations before the last applied one.",
    Default = false)]
  public bool FillGaps { get; set; }

  [Option("server-parallelism", Required = false, Default = 1, HelpText = "Max servers to migrate in parallel.")]
  public int ServerParallelism { get; set; } = 1;

  [Option(
    "database-parallelism",
    Required = false,
    Default = 1,
    HelpText = "Max databases to migrate in parallel per server.")]
  public int DatabaseParallelism { get; set; } = 1;
}