using Unfold.Core;

namespace Unfold.Desktop;

internal static class PetPackImport
{
    internal static async Task<CharacterPackage?> Save(string path, CharacterLibrary library,
        Func<CharacterPackage, Task> installed, Func<CharacterPack, CharacterPackInstallInfo, Task<bool>> confirm)
    {
        using var pack = await Task.Run(() => CharacterPack.Open(path));
        var target = await Task.Run(() => library.InspectInstall(pack));
        if (!await confirm(pack, target)) return null;
        // Retain the inspected revision so another edit cannot be overwritten while confirmation is open.
        var result = await Task.Run(() => library.Install(pack, target.Revision));
        await installed(result);
        return result;
    }
}
