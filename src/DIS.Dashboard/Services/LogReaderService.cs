using DIS.Core.Interfaces;
using DIS.Core.Models;

namespace DIS.Dashboard.Services;

/// <summary>
/// Wraps ILogReader for use in Blazor components.
/// Components call this to hydrate their initial state from SQLite history.
/// </summary>
public sealed class LogReaderService
{
    private readonly ILogReader _reader;

    public LogReaderService(ILogReader reader) => _reader = reader;

    public Task<IReadOnlyList<DISLogEntry>> GetRecentAsync(int count = 500)
        => _reader.GetRecentAsync(count);

    public Task<IReadOnlyList<DISLogEntry>> GetByAssetAsync(string asset, int count = 200)
        => _reader.GetByAssetAsync(asset, count);

    public Task<IReadOnlyList<DISLogEntry>> GetByEventTypeAsync(string eventType, int count = 200)
        => _reader.GetByEventTypeAsync(eventType, count);
}
