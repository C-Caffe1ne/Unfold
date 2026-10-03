using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Unfold.Core;

namespace Unfold.Desktop;

internal interface IAccountCredentialStore
{
    byte[]? Read();
    void Write(byte[] value);
    void Delete();
}

/// <summary>Only the refresh credential is persisted. Access tokens, email and app access stay in memory.</summary>
internal sealed class AccountSessionVault(IAccountCredentialStore store)
{
    private static readonly ConcurrentDictionary<string, AccountSessionVault> Vaults = new(StringComparer.Ordinal);
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal static string Scope(AccountConnection connection, string dataRoot)
    {
        var path = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dataRoot));
        if (OperatingSystem.IsWindows()) path = path.ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{connection.ProjectUrl.AbsoluteUri}\n{connection.Environment}\n{path}")));
    }
    internal static AccountSessionVault For(AccountConnection connection, string dataRoot) =>
        Vaults.GetOrAdd(Scope(connection, dataRoot), key => new(new NativeAccountCredentialStore(key)));

    internal async Task<AccountSession?> LoadAsync()
    {
        var bytes = await Storage(store.Read);
        if (bytes is null) return null;
        try
        {
            if (bytes.Length is 0 or > 2560) throw new JsonException();
            var value = JsonSerializer.Deserialize<SavedCredential>(bytes);
            if (value is null || value.SchemaVersion != 1 || value.UserId == Guid.Empty || !ValidToken(value.RefreshToken))
                throw new JsonException();
            // An expired placeholder must be exchanged with Supabase before it can be presented or used.
            return new(value.UserId, "", value.RefreshToken, DateTimeOffset.MinValue);
        }
        catch (JsonException) { await ClearAsync(); return null; }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal async Task SaveAsync(AccountSession session)
    {
        if (session.UserId == Guid.Empty || !ValidToken(session.RefreshToken))
            throw new AccountException(AccountFailure.InvalidResponse);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new SavedCredential
            { SchemaVersion = 1, UserId = session.UserId, RefreshToken = session.RefreshToken });
        try
        {
            if (bytes.Length > 2560) throw new AccountException(AccountFailure.InvalidResponse);
            await Storage(() => { store.Write(bytes); return true; });
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    internal Task ClearAsync() => Storage(() => { store.Delete(); return true; });
    private static bool ValidToken(string? token) => token is { Length: > 0 and <= 1024 } && !token.Any(char.IsWhiteSpace) && !token.Any(char.IsControl);
    private static async Task<T> Storage<T>(Func<T> action)
    {
        try { return await Task.Run(action); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception
            or DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        { throw new AccountException(AccountFailure.SessionStorageUnavailable); }
    }
    // No generated record ToString: a credential must never appear in logs.
    private sealed class SavedCredential
    {
        public int SchemaVersion { get; init; }
        public Guid UserId { get; init; }
        public string RefreshToken { get; init; } = "";
        public override string ToString() => nameof(SavedCredential);
    }
}
