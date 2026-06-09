using DIS.Dashboard.Models;

namespace DIS.Dashboard.Services;

/// <summary>
/// Provides the current dashboard state to all Razor components.
/// The production implementation subscribes to DIS log events and
/// updates the snapshot on each state/output/orchestration change.
/// </summary>
public interface IDashboardStateService
{
    DashboardState Current { get; }
    event Action OnStateChanged;
    void Update(Action<DashboardState> mutate);
}

/// <summary>
/// Default implementation — starts with realistic seed data and
/// exposes an Update() method for the log-reader background service
/// to push new values into.
/// </summary>
public sealed class DashboardStateService : IDashboardStateService
{
    private readonly DashboardState _state = new();
    private readonly object _lock = new();

    public DashboardState Current
    {
        get { lock (_lock) { return _state; } }
    }

    public event Action? OnStateChanged;

    public void Update(Action<DashboardState> mutate)
    {
        lock (_lock)
        {
            mutate(_state);
        }
        OnStateChanged?.Invoke();
    }
}
