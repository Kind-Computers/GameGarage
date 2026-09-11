// Copyright Kind Computers. Licensed under the MIT License.
using System.Globalization;
using System.Resources;

[assembly: NeutralResourcesLanguage("en")]

namespace StabilityTest;

internal static class WorkerText
{
    private static readonly ResourceManager Manager = new("StabilityTest.WorkerText", typeof(WorkerText).Assembly);

    internal static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException(key);

    internal static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}

internal static class ScanAlgorithm
{
    // Bump this identifier when pattern generation, addressing, or scan semantics change.
    internal const string Version = "xor-address-v1";
}
