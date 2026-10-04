using System.Reflection;

namespace Unfold.Desktop;

internal static class AppRelease
{
    public static string Version { get; } = typeof(AppRelease).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion.Split('+')[0];
    public static string DisplayVersion { get; } = FormatDisplayVersion(Version);

    internal static string FormatDisplayVersion(string version)
    {
        var parts = version.Split('-', 2);
        var beta = parts.Length == 2 && (parts[1] == "beta" || parts[1].StartsWith("beta.", StringComparison.Ordinal));
        return beta ? $"Beta v{parts[0]}" : $"v{version}";
    }
}
