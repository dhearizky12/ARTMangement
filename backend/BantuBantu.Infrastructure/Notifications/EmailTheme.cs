namespace BantuBantu.Infrastructure.Notifications;

/// <summary>Email design tokens, mirrored from frontend/src/styles/tokens.css.
/// <see cref="NotificationThemeDriftTests"/> in the test project fails when
/// these drift from the frontend token file.</summary>
public static class EmailTheme
{
    public const string Primary = "#0f6e56";
    public const string PageBackground = "#f4efe4";
    public const string Surface = "#ffffff";
    public const string Ink = "#1e2b24";
    public const string BorderWidth = "3px";
    public const string Radius = "8px";
    public const string ShadowOffset = "5px";
    public const string Muted = "#6b6a63";
    public const string Accent = "#fac775";
    public const string AccentInk = "#412402";
    public const string Danger = "#9b2626";
    // Web-safe bold fallback for the app font ("Space Grotesk"), which mail
    // clients cannot load.
    public const string FontFamily = "'Space Grotesk', Arial, Helvetica, sans-serif";
    public const string Wordmark = "* bantu-bantu.";
}
