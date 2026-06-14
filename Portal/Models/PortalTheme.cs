namespace Portal.Models;

public sealed record PortalTheme
{
    public string PortalName      { get; set; } = "Culinaire";
    public string Tagline         { get; set; } = "Distinctive Dining & Hospitality Management";
    public string LogoPath        { get; set; } = "/images/logo.png";
    public string BackgroundColor { get; set; } = "#FFFFFF";
    public string TextColor       { get; set; } = "#000000";
    public string PrimaryColor    { get; set; } = "#2B6B35";
    public string AccentColor     { get; set; } = "#1A4A22";
    public string SidebarBg       { get; set; } = "#1A4A22";
    public string SidebarText     { get; set; } = "#FFFFFF";
    public string HeaderBg        { get; set; } = "#2B6B35";
    public string HeaderText      { get; set; } = "#FFFFFF";
    public string FooterBg        { get; set; } = "#1A4A22";
    public string FooterText      { get; set; } = "#FFFFFF";

    public string ToCssVariables() =>
        ":root {" +
        $"--color-bg:{BackgroundColor};" +
        $"--color-text:{TextColor};" +
        $"--color-primary:{PrimaryColor};" +
        $"--color-accent:{AccentColor};" +
        $"--color-sidebar-bg:{SidebarBg};" +
        $"--color-sidebar-txt:{SidebarText};" +
        $"--color-header-bg:{HeaderBg};" +
        $"--color-header-txt:{HeaderText};" +
        $"--color-footer-bg:{FooterBg};" +
        $"--color-footer-txt:{FooterText};" +
        "}";
}
