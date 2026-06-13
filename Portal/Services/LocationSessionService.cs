namespace Portal.Services;

/// <summary>
/// Scoped per-circuit service that holds the location the current user selected
/// on the dashboard for this browser session.
/// </summary>
public sealed class LocationSessionService
{
    public int    SelectedLocationID   { get; private set; }
    public string SelectedLocationCode { get; private set; } = string.Empty;
    public string SelectedLocationName { get; private set; } = string.Empty;
    public bool   HasLocation          => SelectedLocationID > 0;

    public event Action? LocationChanged;

    public void Select(int id, string code, string name)
    {
        SelectedLocationID   = id;
        SelectedLocationCode = code;
        SelectedLocationName = name;
        LocationChanged?.Invoke();
    }

    public void Clear()
    {
        SelectedLocationID   = 0;
        SelectedLocationCode = string.Empty;
        SelectedLocationName = string.Empty;
        LocationChanged?.Invoke();
    }
}
