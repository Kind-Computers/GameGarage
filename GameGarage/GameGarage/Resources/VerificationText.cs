using System.Globalization;
using System.Resources;

namespace GameGarage.Resources;

/// <summary>English fallback text for the frontend's result summaries.</summary>
public static class VerificationText
{
    private static readonly ResourceManager Manager =
        new("GameGarage.Resources.VerificationText", typeof(VerificationText).Assembly);

    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";
    public static string Format(string key, params object?[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
