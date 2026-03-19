namespace InitializeNL.DbMigrator.Cli;

internal static class SemaphoreSlimExtensions
{
  public static async Task<AcquiredSemaphoreSlim> WaitDisposableAsync(
    this SemaphoreSlim semaphore,
    CancellationToken cancellationToken = default)
  {
    await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
    return new AcquiredSemaphoreSlim(semaphore);
  }
}

internal sealed class AcquiredSemaphoreSlim(SemaphoreSlim semaphore) : IDisposable
{
  private SemaphoreSlim Semaphore { get; } = semaphore;

  public void Dispose()
  {
    Semaphore.Release();
  }
}