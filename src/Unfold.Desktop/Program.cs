using Avalonia;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--review-original-pets")
        {
            if (args.Length != 1) { Console.Error.WriteLine("Usage: --review-original-pets"); return 2; }
            var profile = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
            if (string.IsNullOrEmpty(profile))
                Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(Path.GetTempPath(), "Unfold-originals-" + Guid.NewGuid().ToString("N")));
            else if (Directory.Exists(profile) && Directory.EnumerateFileSystemEntries(profile).Any())
            { Console.Error.WriteLine("Original pet review requires a new empty UNFOLD_DATA_DIR."); return 2; }
        }
        if (args.Length > 0 && args[0] == "--review-pet-pack")
        {
            if (args.Length != 2) { Console.Error.WriteLine("Usage: --review-pet-pack <file.unfoldpet>"); return 2; }
            var profile = Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR");
            if (string.IsNullOrEmpty(profile))
                Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(Path.GetTempPath(), "Unfold-review-" + Guid.NewGuid().ToString("N")));
            else if (Directory.Exists(profile) && Directory.EnumerateFileSystemEntries(profile).Any())
            { Console.Error.WriteLine("Pet review requires a new empty UNFOLD_DATA_DIR."); return 2; }
        }
        if (args.Length > 0 && args[0] == "--pack-character")
        {
            if (args.Length != 4) { Console.Error.WriteLine("Usage: --pack-character <character-directory> <content-version> <new-output.unfoldpet>"); return 2; }
            try { CharacterPack.Create(args[1], args[2], args[3]); Console.WriteLine("Pet pack created: " + Path.GetFullPath(args[3])); return 0; }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or JsonException or ArgumentException)
            { Console.Error.WriteLine(error.Message); return 1; }
        }
        if (args.Length > 0 && args[0] == "--validate-characters")
        {
            if (args.Length > 2) { Console.Error.WriteLine("Usage: --validate-characters [directory]"); return 2; }
            var report = CharacterAssetAudit.Inspect(args.Length == 2 ? args[1] : AppPaths.BuiltInRoot);
            Console.WriteLine(JsonSerializer.Serialize(report, CharacterLibrary.JsonOptions));
            return report.Success ? 0 : 1;
        }
        if (args.Contains("--smoke-test") && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR")))
            Environment.SetEnvironmentVariable("UNFOLD_DATA_DIR", Path.Combine(Path.GetTempPath(), "Unfold-smoke-" + Guid.NewGuid().ToString("N")));
        var root = AppPaths.DataRoot;
        Directory.CreateDirectory(root);
        // File locks are released by the OS on crash. Do not allow two tray apps
        // to count time, display pets, or write settings concurrently.
        FileStream instance;
        try { instance = new FileStream(Path.Combine(root, ".instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException) { SingleInstance.RequestActivation(); return 0; }
        using (instance)
        {
            try { return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args); }
            catch (Exception error) { AppPaths.Log(error); return 1; }
        }
    }
    public static AppBuilder BuildAvaloniaApp() => ConfigureRendering(AppBuilder.Configure<App>().UsePlatformDetect()).LogToTrace();

    internal static AppBuilder ConfigureRendering(AppBuilder builder)
    {
        if (OperatingSystem.IsMacOS())
        {
            // Metal leaves transient previous-frame silhouettes in the transparent pet
            // window during rapid pose changes. Keep GPU rendering through OpenGL,
            // with a software fallback for Macs where OpenGL cannot initialize.
            builder.With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software]
            });
        }
        return builder;
    }
}

public static class AppPaths
{
    public static string DataRoot => Environment.GetEnvironmentVariable("UNFOLD_DATA_DIR") is { Length: > 0 } custom
        ? Path.GetFullPath(custom)
        : OperatingSystem.IsMacOS()
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Application Support", "Unfold")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Unfold");
    public static string BuiltInRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
    public static void Log(Exception error)
    {
        try { Directory.CreateDirectory(DataRoot); File.AppendAllText(Path.Combine(DataRoot, "unfold.log"), $"{DateTimeOffset.Now:O} {error}\n"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
