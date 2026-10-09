namespace Unfold.Core;

public sealed partial class CharacterLibrary
{
    // One-way migration after validating the playable package. No source-format reader or backup is retained.
    private void RemoveRetiredArtworkSource(CharacterPackage package)
    {
        const string retiredSource = "source.piskel";
        var manifest = package.Manifest;
        if (string.Equals(manifest.SpriteSheet.File, retiredSource, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(manifest.Model?.File, retiredSource, StringComparison.OrdinalIgnoreCase) ||
            manifest.Animations.Values.Any(animation => string.Equals(animation.Gif, retiredSource, StringComparison.OrdinalIgnoreCase))) return;
        try
        {
            var source = AssetPath(package.DirectoryPath, retiredSource);
            if (!File.Exists(source)) return;
            foreach (var action in manifest.Animations.Keys) _ = package.LoadAnimation(action);
            File.Delete(source);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException)
        { Warning?.Invoke($"Retired source cleanup deferred for {manifest.Id}: {error.Message}"); }
    }
}
