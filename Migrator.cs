#nullable enable
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using Dapper;
using InitializeNL.DbMigrator.Scripts;
using Microsoft.Extensions.Logging;
using Npgsql;
using static Spectre.Console.AnsiConsole;
using CoreScript = InitializeNL.DbMigrator.Scripts.Script;

namespace InitializeNL.DbMigrator.Cli;

internal sealed partial class Migrator
{
  private const int MaxVisibleConnections = 10;
  private Dictionary<CoreScript, bool> DestructiveScriptConclusions { get; set; } = [];

  private readonly Lock _destructiveConfirmationLock = new();
  private readonly IConnectionProvider _connectionProvider;
  private readonly string _scriptsDir;
  private readonly string? _assemblyPath;
  private readonly string? _target;
  private readonly bool _useLocks;
  private readonly bool _dryRun;
  private readonly bool _quiet;
  private readonly bool _quietDestructive;
  private readonly bool _fillGaps;
  private readonly int _serverParallelism;
  private readonly int _databaseParallelism;
  private readonly IMigrationTracker _migrationTracker;
  private readonly IMigrationLock _migrationLock;
  private readonly ILogger _logger;
  private readonly ILoggerFactory _loggerFactory;
  private readonly ScriptManager _scriptManager;

  public Migrator(
    CommandLineOptions options,
    IConnectionProvider connectionProvider,
    IMigrationTracker migrationTracker,
    IMigrationLock migrationLock,
    ILoggerFactory loggerFactory)
  {
    ArgumentNullException.ThrowIfNull(options);
    _connectionProvider = connectionProvider;
    _scriptManager = new ScriptManager(loggerFactory);
    _scriptsDir = options.Source;
    _assemblyPath = string.IsNullOrWhiteSpace(options.Assembly) ? null : options.Assembly;
    _target = string.IsNullOrWhiteSpace(options.Target) ? null : options.Target;
    _useLocks = !options.NoLock;
    _dryRun = options.DryRun;
    _quiet = options.Yes;
    _quietDestructive = options.AllowDestructive;
    _fillGaps = options.FillGaps;
    _serverParallelism = options.ServerParallelism;
    _databaseParallelism = options.DatabaseParallelism;
    _migrationTracker = migrationTracker;
    _migrationLock = migrationLock;
    _loggerFactory = loggerFactory;
    _logger = loggerFactory.CreateLogger<Migrator>();
  }

  public async Task RunAsync()
  {
    if (_dryRun)
    {
      LogDryRunMode();
    }
    else
    {
      LogRealMode();
    }

    _scriptManager.Load(_scriptsDir, _assemblyPath, _target);
    IReadOnlyList<MigrationTargetGroup> connections = await _connectionProvider.GetTargetsAsync().ConfigureAwait(false);
    await MigrateAllAsync(connections, _target).ConfigureAwait(false);
  }

  private static bool PromptConfirmation(string confirmationPassword)
  {
    MarkupLine(
      CultureInfo.InvariantCulture,
      $"[bold red]Type [yellow]{confirmationPassword}[/] to confirm or press Enter to cancel:[/]");
    string userInput = Ask<string>("> ");
    if (string.IsNullOrWhiteSpace(userInput))
    {
      MarkupLine("[grey]Cancelled.[/]");
      return false;
    }

    if (confirmationPassword.Equals(userInput, StringComparison.OrdinalIgnoreCase))
    {
      return true;
    }

    return false;
  }

  private async Task MigrateAllAsync(IEnumerable<MigrationTargetGroup> servers, string? targetName)
  {
    using CancellationTokenSource cancellationTokenSource = new();
    using SemaphoreSlim serverSemaphore = new(_serverParallelism, _serverParallelism);
    await Task.WhenAll(
      servers.Select(async s =>
      {
        using (await serverSemaphore.WaitDisposableAsync(cancellationTokenSource.Token).ConfigureAwait(false))
        {
          if (cancellationTokenSource.Token.IsCancellationRequested)
          {
            return;
          }

          await MigrateOneServerAsync(s, targetName, cancellationTokenSource).ConfigureAwait(false);
        }
      })).ConfigureAwait(false);
  }

  private async Task MigrateOneServerAsync(
    MigrationTargetGroup server,
    string? targetName,
    CancellationTokenSource cancellationTokenSource)
  {
    using SemaphoreSlim databaseSemaphore = new(_databaseParallelism, _databaseParallelism);
    using SemaphoreSlim visualSemaphore = new(MaxVisibleConnections, MaxVisibleConnections);
    LogStartingServer(server.Server);
    IEnumerable<Task> dbTasks = server.Targets.Select(async target =>
    {
      using (await databaseSemaphore.WaitDisposableAsync(cancellationTokenSource.Token).ConfigureAwait(false))
      using (await visualSemaphore.WaitDisposableAsync(cancellationTokenSource.Token).ConfigureAwait(false))
      {
        ILogger dbLogger = _loggerFactory.CreateLogger("Migration");
        using (dbLogger.BeginScope(
                 new Dictionary<string, object>
                 {
                   ["Server"] = target.Server,
                   ["Database"] = target.Database,
                 }))
        {
          LogStartingMigration(dbLogger);
          try
          {
            await MigrateOneDatabaseAsync(target, targetName, dbLogger, cancellationTokenSource.Token)
              .ConfigureAwait(false);
            LogMigrationCompleted(dbLogger);
          }
          catch (Exception ex)
          {
            LogMigrationError(dbLogger, ex, ex.Message);
            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            throw;
          }
        }
      }
    });
    await Task.WhenAll(dbTasks).ConfigureAwait(false);
  }

  private async Task MigrateOneDatabaseAsync(
    MigrationTarget connection,
    string? targetName,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    LogInitializing(logger);
#pragma warning disable CA2000, CA2007
    await using NpgsqlConnection dbConnection = new(connection.ConnectionString);
#pragma warning restore CA2000, CA2007
    await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
    LogConnected(logger, connection.SafeConnectionString);
    if (!await AcquireLocksAsync(connection, dbConnection, logger).ConfigureAwait(false))
    {
      return;
    }

    try
    {
      List<CoreScript> migrationQueue = await _scriptManager.GenerateMigrationQueueAsync(
        dbConnection,
        _migrationTracker,
        targetName,
        _fillGaps).ConfigureAwait(false);
      if (migrationQueue.Count > 0)
      {
        string pendingScriptNames = string.Join(", ", migrationQueue.Select(s => $"{s.ShortName} ({s.ScriptType})"));
        LogPendingScripts(logger, pendingScriptNames);
        Queue<CoreScript> historyBuffer = new();
        if (cancellationToken.IsCancellationRequested)
        {
          return;
        }

        foreach (CoreScript script in migrationQueue)
        {
          try
          {
            await MigrateSingleScriptAsync(connection, dbConnection, script, historyBuffer, logger, cancellationToken)
              .ConfigureAwait(false);
          }
          catch (Exception ex)
          {
            LogScriptFailed(logger, ex, script.ShortName);
            throw;
          }
        }

        if (historyBuffer.Count > 0)
        {
          string deferredScriptNames = string.Join(", ", historyBuffer.Select(s => $"{s.ShortName} ({s.ScriptType})"));
          LogDeferredScripts(logger, deferredScriptNames);
        }
      }
      else
      {
        LogUpToDate(logger);
      }
    }
    catch (Exception ex)
    {
      LogMigrationFailed(logger, ex);
      throw;
    }
    finally
    {
      await ReleaseLocksAsync(connection, dbConnection, logger).ConfigureAwait(false);
    }
  }

  private async Task<bool> AcquireLocksAsync(MigrationTarget connection, IDbConnection dbConnection, ILogger logger)
  {
    if (_useLocks)
    {
      LogAcquiringLock(logger, connection.SafeConnectionString);
      if (!_dryRun)
      {
        bool lockAcquired;
        try
        {
          lockAcquired = await _migrationLock.AcquireAsync(dbConnection).ConfigureAwait(false);
        }
        catch (DbException)
        {
          lockAcquired = false;
        }

        if (!lockAcquired)
        {
          LogLockFailed(logger);
          return false;
        }

        LogLockAcquired(logger, connection.SafeConnectionString);
      }
      else
      {
        LogDryRunLockAcquired(logger, connection.SafeConnectionString);
      }
    }
    else
    {
      LogLockingDisabled(logger);
    }

    return true;
  }

  private async Task ReleaseLocksAsync(MigrationTarget connection, IDbConnection dbConnection, ILogger logger)
  {
    if (_useLocks)
    {
      if (!_dryRun)
      {
        await _migrationLock.ReleaseAsync(dbConnection).ConfigureAwait(false);
        LogLockReleased(logger, connection.SafeConnectionString);
      }
      else
      {
        LogDryRunLockReleased(logger, connection.SafeConnectionString);
      }
    }
  }

  private async Task MigrateSingleScriptAsync(
    MigrationTarget connection,
    IDbConnection dbConnection,
    CoreScript script,
    Queue<CoreScript> historyBuffer,
    ILogger logger,
    CancellationToken cancellationToken)
  {
    if (_dryRun)
    {
      if (cancellationToken.IsCancellationRequested)
      {
        return;
      }

      LogDryRunScript(logger, script.ShortName, script.ScriptType);
      await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
      return;
    }

    Stopwatch sw = Stopwatch.StartNew();
    if (!string.IsNullOrWhiteSpace(script.Sql))
    {
      int affectedTotal = 0;
      int affected;
      do
      {
        LogExecutingScript(logger, script.ShortName, script.ScriptType);
        affected = Math.Max(await dbConnection.ExecuteAsync(script.Sql, commandTimeout: 0).ConfigureAwait(false), 0);
        affectedTotal += affected;
        LogRowsAffected(logger, affected, affectedTotal);
        if (affected > script.RepeatUntilAffectedLessThanOrEqual)
        {
          LogRepeatingScript(logger, script.ShortName, script.RepeatUntilAffectedLessThanOrEqual);
        }
      } while (!cancellationToken.IsCancellationRequested && affected > script.RepeatUntilAffectedLessThanOrEqual);

      if (cancellationToken.IsCancellationRequested)
      {
        return;
      }

      LogScriptCompleted(logger, script.ShortName);
    }
    else
    {
      LogSkippedEmptyScript(logger, script.ShortName);
    }

    sw.Stop();

    historyBuffer.Enqueue(script);
    if (script.DeferAddToHistory)
    {
      LogDeferredHistory(logger, script.ShortName);
    }
    else
    {
      while (historyBuffer.TryDequeue(out CoreScript? historicScript))
      {
        if (historicScript.ScriptType == ScriptType.Up)
        {
          LogRecordingApplied(logger, historicScript.ShortName);
          await _migrationTracker.AddAsync(
              dbConnection,
              historicScript.ShortName,
              sw.ElapsedMilliseconds,
              cancellationToken)
            .ConfigureAwait(false);
        }
        else
        {
          LogRecordingReverted(logger, historicScript.ShortName);
          await _migrationTracker.RemoveAsync(
              dbConnection,
              historicScript.ShortName,
              sw.ElapsedMilliseconds,
              cancellationToken)
            .ConfigureAwait(false);
        }
      }
    }
  }

  private bool CheckDestructiveActions(CoreScript script, string server)
  {
    bool result = _quietDestructive;
    if (_quietDestructive || DestructiveScriptConclusions.TryGetValue(script, out result))
    {
      return result;
    }

    lock (_destructiveConfirmationLock)
    {
      if (DestructiveScriptConclusions.TryGetValue(script, out result))
      {
        return result;
      }

      if (!script.Destructive)
      {
        DestructiveScriptConclusions.Add(script, true);
        return true;
      }

      MarkupLine($"Script {script.ShortName} ({script.ScriptType}) is marked as destructive.");
      result = PromptConfirmation(server);
      DestructiveScriptConclusions.Add(script, result);
      return result;
    }
  }

  public bool PreConfirm()
  {
    _scriptManager.Load(_scriptsDir, _assemblyPath, _target);
    IReadOnlyList<MigrationTargetGroup> connections = _connectionProvider.GetTargetsAsync().Result;
    if (_quiet || connections.Count <= 0)
    {
      return true;
    }

    string confirmationServer = connections[0].Targets[0].Server.Trim();
    if (string.IsNullOrWhiteSpace(confirmationServer))
    {
      MarkupLine("[red]Hostname not found.[/]");
      return false;
    }

    List<MigrationTarget> allTargets = connections.SelectMany(s => s.Targets).ToList();
    MarkupLine(
      CultureInfo.InvariantCulture,
      $"[bold red]You are about to migrate {allTargets.Count} database(s).[/]");
    foreach (MigrationTarget target in allTargets)
    {
      MarkupLine(CultureInfo.InvariantCulture, $"[grey]  {target.SafeConnectionString}[/]");
    }

    string input = Ask<string>($"Type [green]{confirmationServer}[/] to confirm or press Enter to cancel:");
    if (string.IsNullOrWhiteSpace(input))
    {
      return false;
    }

    if (input == confirmationServer)
    {
      return true;
    }

    return false;
  }

  public async Task<bool> CheckDestructiveScriptsAsync()
  {
    Dictionary<string, bool> confirmedScripts = new(StringComparer.OrdinalIgnoreCase);
    IReadOnlyList<MigrationTargetGroup> connections = await _connectionProvider.GetTargetsAsync().ConfigureAwait(false);
    foreach (MigrationTargetGroup server in connections)
    {
      foreach (MigrationTarget target in server.Targets)
      {
        LogCheckingDestructive(target.Server, target.Database);
#pragma warning disable CA2007
        await using NpgsqlConnection dbConnection = new(target.ConnectionString);
#pragma warning restore CA2007
        await dbConnection.OpenAsync().ConfigureAwait(false);
        if (!_dryRun)
        {
          try
          {
            await _migrationTracker.InitAsync(dbConnection).ConfigureAwait(false);
          }
          catch (DbException ex)
          {
            LogInitFailed(target.Server, target.Database, ex.Message);
            return false;
          }
        }

        List<CoreScript> migrationQueue = await _scriptManager.GenerateMigrationQueueAsync(
          dbConnection,
          _migrationTracker,
          _target,
          _fillGaps).ConfigureAwait(false);
        if (migrationQueue.Count > 0)
        {
          foreach (CoreScript script in migrationQueue)
          {
            if (confirmedScripts.TryGetValue(script.ShortName, out bool alreadyConfirmed) && alreadyConfirmed)
            {
              continue;
            }

            if (!CheckDestructiveActions(script, target.Server))
            {
              LogDestructiveDenied(script.ShortName, target.Server, target.Database);
              return false;
            }

            confirmedScripts[script.ShortName] = true;
          }
        }

        LogDestructiveCheckPassed(target.Server, target.Database);
      }
    }

    return true;
  }
}