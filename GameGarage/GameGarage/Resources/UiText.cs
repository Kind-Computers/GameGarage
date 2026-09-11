using System.Globalization;
using System.Resources;
using System.Windows.Markup;

namespace GameGarage.Resources;

/// <summary>English fallback resources. Set CurrentUICulture before creating windows.</summary>
public static class UiText
{
    private static readonly ResourceManager Manager = new("GameGarage.Resources.UiText", typeof(UiText).Assembly);
    public static string Get(string key) => Manager.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";
    public static string Format(string key, params object?[] args) => string.Format(CultureInfo.CurrentCulture, Get(key), args);
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => UiText.Get(key);
}
