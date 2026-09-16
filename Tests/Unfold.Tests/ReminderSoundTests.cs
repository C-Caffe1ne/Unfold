using Unfold.Core;

namespace Unfold.Tests;

public class ReminderSoundTests
{
    [Fact]
    public void BuiltInSoundsAreDistinctValidWavesAndImportSurvivesSourceRemoval()
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(Path.Combine(temp.Path, "sounds"));
        var due = ReminderSounds.Default(ReminderSound.Due); var completed = ReminderSounds.Default(ReminderSound.Completed);
        ReminderSounds.Validate(due); ReminderSounds.Validate(completed); Assert.NotEqual(due, completed);
        var source = Path.Combine(temp.Path, "소리.wav"); File.WriteAllBytes(source, completed);
        var id = library.Import(source); File.Delete(source);
        Assert.Equal(completed, File.ReadAllBytes(library.Resolve(ReminderSound.Due, id)));
        File.WriteAllText(library.Resolve(ReminderSound.Due, id), "broken");
        Assert.Equal(due, File.ReadAllBytes(library.Resolve(ReminderSound.Due, id)));
    }
    [Fact]
    public void MalformedAndUnsupportedWaveFilesAreRejectedBeforeStorage()
    {
        using var temp = new TempDirectory(); var library = new ReminderSounds(Path.Combine(temp.Path, "sounds"));
        var source = Path.Combine(temp.Path, "bad.wav"); File.WriteAllText(source, "not wave");
        Assert.Throws<InvalidDataException>(() => library.Import(source));
        var bytes = ReminderSounds.Default(ReminderSound.Due); bytes[20] = 3; File.WriteAllBytes(source, bytes);
        Assert.Throws<InvalidDataException>(() => library.Import(source));
        using (var stream = File.Create(source)) stream.SetLength(5 * 1024 * 1024 + 1);
        Assert.Contains("5 MiB", Assert.Throws<InvalidDataException>(() => library.Import(source)).Message);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "sounds")));
    }
}
