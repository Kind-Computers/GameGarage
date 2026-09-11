using System.Reflection;
using GameGarage.Resources;

namespace GameGarage.Utilities;

/// <summary>Version metadata for the unrestricted MIT build.</summary>
internal static class ProductInfo
{
    public static string Version => typeof(ProductInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.1";
    public static string License => UiText.Get("MitLicense");
}
