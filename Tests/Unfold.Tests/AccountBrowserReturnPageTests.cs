using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Unfold.Core;
using Unfold.Desktop;

namespace Unfold.Tests;

public class AccountBrowserReturnPageTests
{
    [Theory]
    [InlineData(AppLanguage.Korean, "ko")]
    [InlineData(AppLanguage.English, "en")]
    [InlineData(AppLanguage.Japanese, "ja")]
    [InlineData(AppLanguage.ChineseSimplified, "zh-CN")]
    public void ReturnGuidanceUsesTheSelectedLanguage(AppLanguage language, string tag)
    {
        var message = L.T(AccountScreenContent.Load().Copy.BrowserReturn, language);
        var html = AccountBrowserReturnPage.Render(message, language);
        Assert.Contains($"<html lang=\"{tag}\">", html);
        var lines = message.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains($"<h1 id=\"return-message\">{WebUtility.HtmlEncode(lines[0])}</h1>", html);
        Assert.Contains($"<p class=\"return-note\">{WebUtility.HtmlEncode(lines[1])}</p>", html);
        Assert.Contains("name=\"viewport\"", html);
    }

    [Fact]
    public void ConfiguredCopyIsEscapedAndArtworkNeedsNoExternalRequests()
    {
        const string message = "Return <script>alert('test')</script> & \"Unfold\"";
        var html = AccountBrowserReturnPage.Render(message, AppLanguage.English);
        Assert.Contains(WebUtility.HtmlEncode(message), html);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<a ", html, StringComparison.OrdinalIgnoreCase);
        var images = Regex.Matches(html, "src=\"([^\"]+)\"");
        Assert.Equal(2, images.Count);
        foreach (Match match in images)
        {
            Assert.StartsWith("data:image/png;base64,", match.Groups[1].Value);
            var bytes = Convert.FromBase64String(match.Groups[1].Value[22..]);
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, bytes[..8]);
        }
    }

    [Fact]
    public void PolicyAllowsOnlyTheExactBundledStylesAndImages()
    {
        var html = AccountBrowserReturnPage.Render("Return to Unfold", AppLanguage.English);
        var css = Regex.Match(html, "<style>([\\s\\S]*?)</style>").Groups[1].Value;
        Assert.NotEmpty(css);
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(css)));
        Assert.Equal($"default-src 'none'; img-src data:; style-src 'sha256-{hash}'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'",
            AccountBrowserReturnPage.ContentSecurityPolicy);
    }
}
