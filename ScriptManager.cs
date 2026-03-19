#nullable enable
using System.Data;
using InitializeNL.DbMigrator.Scripts;
using InitializeNL.DbMigrator.Sources;
using Microsoft.Extensions.Logging;
using CoreScript = InitializeNL.DbMigrator.Scripts.Script;
using CoreScriptManager = InitializeNL.DbMigrator.ScriptManager;

namespace InitializeNL.DbMigrator.Cli;

internal sealed partial class ScriptManager
{
  private readonly CoreScriptManager _coreScriptManager = new();
  private readonly ILogger _logger;

  public ScriptManager(ILoggerFactory loggerFactory)
  {
    _logger = loggerFactory.CreateLogger<ScriptManager>();
  }

  public IReadOnlyList<UpDownScript> Migrations => _coreScriptManager.Migrations;

  public void Load(IMigrationSource migrationSource, string? target)
  {
    LogRetrievingScripts();
    _coreScriptManager.Load(migrationSource, target);
  }

  public void Load(string scriptsDir, string? target)
  {
    Load(new FileSystemMigrationSource(scriptsDir), target);
  }

  public async Task<List<CoreScript>> GenerateMigrationQueueAsync(
    IDbConnection dbConnection,
    IMigrationTracker migrationTracker,
    string? targetName,
    bool allowFillGaps)
  {
    IReadOnlyList<string> applied = await migrationTracker.GetAppliedAsync(dbConnection).ConfigureAwait(false);
    List<CoreScript> queue = await _coreScriptManager.GenerateMigrationQueueAsync(applied, targetName, allowFillGaps)
      .ConfigureAwait(false);
    if (targetName == null && Migrations.Count > 0)
    {
      LogTargetMigration(Migrations[^1].ShortName);
    }
    else if (targetName != null)
    {
      LogTargetMigration(targetName);
    }

    return queue;
  }
}