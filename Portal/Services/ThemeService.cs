using Portal.Models;

namespace Portal.Services;

/// <summary>
/// Singleton that holds the current portal theme. Reloaded via Admin → Theme Editor.
/// </summary>
public sealed class ThemeService
{
    private PortalTheme _theme = new();

    public PortalTheme Current => _theme;

    public void Load(IReadOnlyDictionary<string, string> settings)
    {
        _theme = new PortalTheme
        {
            PortalName      = Get(settings, "Portal.Name",         "Culinaire"),
            Tagline         = Get(settings, "Portal.Tagline",      "Distinctive Dining & Hospitality Management"),
            LogoPath        = Get(settings, "Portal.LogoPath",     "/images/logo.png"),
            BackgroundColor = Get(settings, "Theme.BackgroundColor","#FFFFFF"),
            TextColor       = Get(settings, "Theme.TextColor",     "#000000"),
            PrimaryColor    = Get(settings, "Theme.PrimaryColor",  "#2B6B35"),
            AccentColor     = Get(settings, "Theme.AccentColor",   "#1A4A22"),
            SidebarBg       = Get(settings, "Theme.SidebarBg",     "#1A4A22"),
            SidebarText     = Get(settings, "Theme.SidebarText",   "#FFFFFF"),
            HeaderBg        = Get(settings, "Theme.HeaderBg",      "#2B6B35"),
            HeaderText      = Get(settings, "Theme.HeaderText",    "#FFFFFF"),
            FooterBg        = Get(settings, "Theme.FooterBg",      "#1A4A22"),
            FooterText      = Get(settings, "Theme.FooterText",    "#FFFFFF"),
        };
        NotifyChanged();
    }

    public void Update(PortalTheme theme)
    {
        _theme = theme;
        NotifyChanged();
    }

    public event Action? OnChange;
    private void NotifyChanged() => OnChange?.Invoke();

    private static string Get(IReadOnlyDictionary<string, string> d, string k, string def) =>
        d.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : def;
}
