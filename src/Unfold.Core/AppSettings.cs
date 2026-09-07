using System.Text.Json;

namespace Unfold.Core;

public sealed record AppSettings
{
    public int IntervalMinutes { get; init; } = 60;
    public int IdleMinutes { get; init; } = 5;
    public string SelectedCharacterId { get; init; } = "default-cat";
    public bool ShowPet { get; init; } = true;
    public int? PetX { get; init; }
    public int? PetY { get; init; }
    public static AppSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var value = JsonSerializer.Deserialize<AppSettings>(ImageCodec.ReadBounded(path, 65536), CharacterLibrary.JsonOptions) ?? throw new InvalidDataException("Missing settings.");
        if (value.IntervalMinutes is < 5 or > 240 || value.IdleMinutes is < 1 or > 60 || !CharacterLibrary.SafeId(value.SelectedCharacterId))
            throw new InvalidDataException("Invalid settings values.");
        return value;
    }
    public void Save(string path) => AtomicFile.Write(path, JsonSerializer.SerializeToUtf8Bytes(this, CharacterLibrary.JsonOptions));
}
