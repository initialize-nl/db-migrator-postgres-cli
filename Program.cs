#nullable enable
using System.Diagnostics.CodeAnalysis;
using CommandLine;
using InitializeNL.DbMigrator.Postgres;
using Microsoft.Extensions.Logging;
using Npgsql;
using Spectre.Console;

namespace InitializeNL.DbMigrator.Cli;

internal static partial class Program
{
  [LoggerMessage(Level = LogLevel.Information, Message = "Running migrations")]
  private static partial void LogRunningMigrations(ILogger logger);

  [LoggerMessage(Level = LogLevel.Error, Message = "Failed to complete migrations")]
  private static partial void LogMigrationsFailed(ILogger logger, Exception ex);

  [SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Top-level exception handler for logging")]
  public static async Task<int> Main(string[] args)
  {
    int errorLevel = -1;
    CommandLineOptions? parsedOptions = null;

    await Parser.Default.ParseArguments<CommandLineOptions>(args)
      .WithParsedAsync(options =>
      {
        parsedOptions = options;
        return Task.CompletedTask;
      }).ConfigureAwait(false);

    if (parsedOptions == null)
    {
      return errorLevel;
    }

    string connectionString = ResolveConnectionString(parsedOptions);
    if (string.IsNullOrWhiteSpace(connectionString))
    {
      AnsiConsole.MarkupLine(
        "[red]No connection string provided. Use --connection-string or set DBMIGRATOR_CONNECTION.[/]");
      return errorLevel;
    }

    connectionString = ApplyPasswordOverride(connectionString, parsedOptions);

    using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
    {
      builder.AddSimpleConsole(options =>
      {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
        options.IncludeScopes = true;
      });
      builder.SetMinimumLevel(LogLevel.Information);
    });

    ILogger logger = loggerFactory.CreateLogger("InitializeNL.DbMigrator.Cli");

    IConnectionProvider connectionProvider = string.IsNullOrWhiteSpace(parsedOptions.DiscoveryScript)
      ? new SingleConnectionProvider(connectionString)
      : await SqlDiscoveryConnectionProvider.FromFileAsync(
        connectionString,
        parsedOptions.DiscoveryScript).ConfigureAwait(false);

    Migrator migrator = new(
      parsedOptions,
      connectionProvider,
      new PostgresMigrationTracker(),
      new PostgresAdvisoryLock(),
      loggerFactory);

    if (!migrator.PreConfirm() || !await migrator.CheckDestructiveScriptsAsync().ConfigureAwait(false))
    {
      return errorLevel;
    }

    try
    {
      LogRunningMigrations(logger);
      await migrator.RunAsync().ConfigureAwait(false);
      errorLevel = 0;
    }
    catch (Exception ex)
    {
      LogMigrationsFailed(logger, ex);
    }

    return errorLevel;
  }

  private static string ResolveConnectionString(CommandLineOptions options)
  {
    if (!string.IsNullOrWhiteSpace(options.ConnectionString))
    {
      return options.ConnectionString;
    }

    return Environment.GetEnvironmentVariable("DBMIGRATOR_CONNECTION") ?? string.Empty;
  }

  private static string ApplyPasswordOverride(string connectionString, CommandLineOptions options)
  {
    string password = options.Password;
    if (string.IsNullOrWhiteSpace(password))
    {
      password = Environment.GetEnvironmentVariable("DBMIGRATOR_PASSWORD") ?? string.Empty;
    }

    if (string.IsNullOrWhiteSpace(password))
    {
      return connectionString;
    }

    NpgsqlConnectionStringBuilder builder = new(connectionString)
    {
      Password = password,
    };
    return builder.ToString();
  }
}