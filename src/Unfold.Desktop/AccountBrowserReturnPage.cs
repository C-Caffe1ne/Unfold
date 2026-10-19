using System.Net;
using System.Security.Cryptography;
using System.Text;
using Unfold.Core;

namespace Unfold.Desktop;

/// <summary>Offline browser guidance; the app still verifies the sign-in and purchase access.</summary>
internal static class AccountBrowserReturnPage
{
    private const string Styles = """
        :root { color-scheme: light; --canvas: #f1edf7; --surface: #fdfcff; --ink: #32283e; --line: #d5cade; }
        * { box-sizing: border-box; }
        body { display: grid; place-items: center; min-height: 100svh; margin: 0; padding: 32px 20px;
          background: var(--canvas); color: var(--ink); font-family: -apple-system, BlinkMacSystemFont,
          "Segoe UI", "Apple SD Gothic Neo", "Malgun Gothic", sans-serif; }
        main { width: 100%; max-width: 520px; padding: 40px 40px 44px; background: var(--surface);
          border: 1px solid var(--line); border-radius: 28px; text-align: center; }
        .brand { display: block; width: 112px; height: auto; margin: 0 auto; }
        .companion { display: grid; place-items: center; width: 256px; max-width: 100%; aspect-ratio: 1;
          margin: 32px auto 28px; border-radius: 50%; background: #ece5f4; }
        .companion img { display: block; width: 88%; height: 88%; object-fit: contain; }
        h1 { max-width: 360px; margin: 0 auto; font-size: 24px; font-weight: 600; line-height: 1.5;
          letter-spacing: -.035em; word-break: keep-all; overflow-wrap: anywhere; text-wrap: balance; }
        .return-note { margin: 12px 0 0; color: #70627f; font-size: 15px; line-height: 1.7;
          word-break: keep-all; overflow-wrap: anywhere; text-wrap: balance; }
        @media (max-width: 480px) {
          body { padding: 20px 16px; }
          main { padding: 32px 24px 36px; border-radius: 24px; }
          .companion { width: 216px; margin: 28px auto 24px; }
          h1 { font-size: 20px; }
        }
        """;

    private static readonly Lazy<string> Logo = new(() => Image("logo.png"));
    private static readonly Lazy<string> Companion = new(() => Image("companion.png"));
    public static string ContentSecurityPolicy { get; } = "default-src 'none'; img-src data:; style-src 'sha256-"
        + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(Styles)))
        + "'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";

    public static string Render(string message, AppLanguage language)
    {
        // A line break in the configured copy separates the heading from its closing-tab note.
        var lines = message.Split('\n', 2);
        var note = lines.Length == 2 ? "<p class=\"return-note\">" + WebUtility.HtmlEncode(lines[1]) + "</p>" : "";
        var lang = language switch
        {
            AppLanguage.English => "en",
            AppLanguage.Japanese => "ja",
            AppLanguage.ChineseSimplified => "zh-CN",
            _ => "ko"
        };
        return $$"""
            <!doctype html>
            <html lang="{{lang}}">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>Unfold</title>
              <link rel="icon" href="data:,">
              <style>{{Styles}}</style>
            </head>
            <body>
              <main aria-labelledby="return-message">
                <img class="brand" src="{{Logo.Value}}" alt="Unfold" width="112">
                <div class="companion" aria-hidden="true">
                  <img src="{{Companion.Value}}" alt="" width="224" height="224">
                </div>
                <h1 id="return-message">{{WebUtility.HtmlEncode(lines[0])}}</h1>
                {{note}}
              </main>
            </body>
            </html>
            """;
    }

    private static string Image(string name)
    {
        using var stream = typeof(AccountBrowserReturnPage).Assembly.GetManifestResourceStream("Unfold.Account.BrowserReturn." + name)!;
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return "data:image/png;base64," + Convert.ToBase64String(bytes.ToArray());
    }
}
