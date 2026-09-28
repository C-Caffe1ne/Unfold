using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unfold.Desktop;

/// <summary>Presentation only. These prices never create orders or grant purchase access.</summary>
public sealed record AccountMarket(string Id, string Label, string Currency, long AmountMinor, int MinorUnitDigits, string Prefix, string Suffix)
{
    public string DisplayPrice => Prefix + (AmountMinor / (decimal)Math.Pow(10, MinorUnitDigits))
        .ToString("N" + MinorUnitDigits, CultureInfo.InvariantCulture) + Suffix;
    public override string ToString() => Label;
}

public sealed record AccountScreenCopy
{
    public required string WindowTitle { get; init; }
    public required string LoginStep { get; init; }
    public required string PurchaseStep { get; init; }
    public required string WelcomeTitle { get; init; }
    public required string PurchaseTitle { get; init; }
    public required string ReadyTitle { get; init; }
    public required string ProductLabel { get; init; }
    public required string PurchaseTerm { get; init; }
    public required string GoogleButton { get; init; }
    public required string PurchaseButton { get; init; }
    public required string ChangeAccountButton { get; init; }
    public required string OpenAppButton { get; init; }
    public required string CancelButton { get; init; }
    public required string RetryButton { get; init; }
    public required string CheckingButton { get; init; }
    public required string SigningInButton { get; init; }
    public required string BrowserWaiting { get; init; }
    public required string BrowserReturn { get; init; }
    public required string SignInFailed { get; init; }
    public required string SignInCancelled { get; init; }
    public required string SignInUnavailable { get; init; }
    public required string PurchaseUnavailable { get; init; }
    public required string CheckoutUnavailable { get; init; }
    public required string SignedInLabel { get; init; }
    public required string ReadyStatus { get; init; }
    public required string AccountSection { get; init; }
    public required string AccountButton { get; init; }
    public required string MarketLabel { get; init; }
    public required string Copyright { get; init; }
}

public sealed record AccountScreenContent(int SchemaVersion, string Brand, string CompanionText, string CompanionImage,
    string DefaultMarket, AccountMarket[] Markets, AccountScreenCopy Copy)
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    public static string AssetRoot => Path.Combine(AppContext.BaseDirectory, "Assets", "Account");
    public static AccountScreenContent Load(string? path = null)
    {
        path ??= Path.Combine(AssetRoot, "entry-screen.json");
        try { return Parse(File.ReadAllText(path)); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            // A presentation edit must not prevent the local app from starting.
            using var stream = typeof(AccountScreenContent).Assembly.GetManifestResourceStream("Unfold.Account.entry-screen.json")!;
            using var reader = new StreamReader(stream);
            return Parse(reader.ReadToEnd());
        }
    }
    internal static AccountScreenContent Parse(string json)
    {
        var content = JsonSerializer.Deserialize<AccountScreenContent>(json, JsonOptions);
        if (content is null || content.SchemaVersion != 1 || string.IsNullOrWhiteSpace(content.Brand) || content.CompanionText is null
            || string.IsNullOrWhiteSpace(content.CompanionImage) || Path.GetFileName(content.CompanionImage) != content.CompanionImage
            || content.CompanionImage.Contains('\\') || content.Copy is null
            || content.Markets is not { Length: > 0 and <= 8 }
            || content.Markets.Any(p => p is null || string.IsNullOrWhiteSpace(p.Id) || string.IsNullOrWhiteSpace(p.Label)
                || string.IsNullOrWhiteSpace(p.Currency) || p.AmountMinor < 0 || p.MinorUnitDigits is < 0 or > 3 || p.Prefix is null || p.Suffix is null)
            || content.Markets.Select(p => p.Id).Distinct().Count() != content.Markets.Length
            || !content.Markets.Any(p => p.Id == content.DefaultMarket)
            || typeof(AccountScreenCopy).GetProperties().Any(p => string.IsNullOrWhiteSpace((string?)p.GetValue(content.Copy))))
            throw new InvalidDataException("Invalid account presentation configuration.");
        return content;
    }
}
