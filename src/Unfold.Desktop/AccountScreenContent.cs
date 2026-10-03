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
    public required string GoogleButton { get; init; }
    public required string PurchaseButton { get; init; }
    public required string ChangeAccountButton { get; init; }
    public required string QuitButton { get; init; }
    public string CodeButton { get; init; } = "코드 입력";
    public string CodeDialogTitle { get; init; } = "코드 입력";
    public string CodeLabel { get; init; } = "코드";
    public string CodeConfirmButton { get; init; } = "확인";
    public string InvalidCodeMessage { get; init; } = "올바른 코드를 입력해 주세요.";
    public string CodeUnavailable { get; init; } = "이용 권한을 확인하지 못했어요. 다시 시도해 주세요.";
    public string CodeRateLimited { get; init; } = "잠시 후 다시 시도해 주세요. 10분 동안 최대 5회 입력할 수 있어요.";
    public required string CancelButton { get; init; }
    public required string RetryButton { get; init; }
    public required string CheckingButton { get; init; }
    public required string OpeningCheckoutButton { get; init; }
    public required string CheckPurchaseButton { get; init; }
    public required string SigningInButton { get; init; }
    public required string BrowserWaiting { get; init; }
    public required string BrowserReturn { get; init; }
    public required string SignInFailed { get; init; }
    public required string SignInCancelled { get; init; }
    public required string SignInUnavailable { get; init; }
    public required string PurchaseUnavailable { get; init; }
    public required string CheckoutUnavailable { get; init; }
    public required string CheckoutWaiting { get; init; }
    public required string PurchaseNotFound { get; init; }
    public required string SignedInLabel { get; init; }
    public required string ReadyStatus { get; init; }
    public required string AccountSection { get; init; }
    public required string AccountButton { get; init; }
    public string SignOutButton { get; init; } = "로그아웃";
    public string SignedOutLabel { get; init; } = "로그인되지 않음";
    public string SignOutUnavailable { get; init; } = "앱에서 로그아웃했어요. 서버 세션 종료는 확인하지 못했어요.";
    public string RestoreUnavailable { get; init; } = "로그인 정보를 확인하지 못했어요. 다시 시도해 주세요.";
    public string SessionStorageUnavailable { get; init; } = "로그인 정보를 안전하게 저장하거나 삭제하지 못했어요. OS 보안 저장소 접근을 확인해 주세요.";
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
