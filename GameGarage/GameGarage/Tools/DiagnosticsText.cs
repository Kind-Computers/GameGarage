using System.Globalization;
using System.Resources;

namespace GameGarage.Tools;

internal static class DiagnosticsText
{
    private static readonly ResourceManager Manager = new("GameGarage.Tools.Diagnostics", typeof(DiagnosticsText).Assembly);
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new MissingManifestResourceException($"Missing diagnostic resource: {key}");
    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
