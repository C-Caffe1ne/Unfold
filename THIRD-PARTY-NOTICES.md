# Third-party components in the C# desktop build

- **Avalonia 12.1.2** and Fluent theme — MIT; copyright the AvaloniaUI contributors.
  Source and license: https://github.com/AvaloniaUI/Avalonia
- **SkiaSharp 3.119.4** — MIT; copyright Microsoft and contributors.
  Source and license: https://github.com/mono/SkiaSharp
- **Svg.Controls.Avalonia 12.0.0.17 / Svg.Skia** — MIT; copyright Wiesław Šoltés.
  Source and license: https://github.com/wieslawsoltes/Svg.Skia
- **Skia** native rendering library — BSD-style; copyright Google and contributors.
  License: https://skia.googlesource.com/skia/+/main/LICENSE
- **HarfBuzz / HarfBuzzSharp** text shaping — upstream MIT-style licenses.
  Sources: https://github.com/harfbuzz/harfbuzz and https://github.com/mono/SkiaSharp
- **.NET runtime** (self-contained distributions) — MIT; copyright .NET contributors.
  Source and notices: https://github.com/dotnet/runtime
- **Noonnu Basic Gothic Regular** — copyright 2024 Project Noonnu; designer Rabbit's Typo. Foundry.
  The publisher permits commercial embedding and redistribution; the font itself may not be sold.
  Published terms: https://noonnu.cc/font_page/1496
  A copyright notice and terms summary are included at `Licenses/NoonnuBasicGothic.txt`.
- **FFmpeg 8.1.2** command-line media importer — LGPL-2.1-or-later; copyright the FFmpeg contributors.
  Pinned binary build: https://github.com/serversideup/ffmpeg-lgpl-builds/releases/tag/v8.1.2-27
  FFmpeg runs as a separate process. Its license, exact source provenance and bundled-library
  notices are retained in `Tools/` next to the executable (`COPYING.LGPLv2.1`, `SOURCE.txt`,
  and platform-specific license files). Upstream source: https://ffmpeg.org/releases/ffmpeg-8.1.2.tar.xz
  Build scripts: https://github.com/serversideup/ffmpeg-lgpl-builds/tree/v8.1.2-27

Dependency packages retain their upstream notices. Published .NET runtime files
include the runtime's LICENSE and ThirdPartyNotices documents. Exact dependency
versions are recorded in NuGet lock files.

The Piskel web runtime is not distributed. The retained C# diagnostic editor
implements Piskel v2 file-format compatibility through `PiskelCodec`; it does not
embed the upstream web application. Built-in character files are the existing
project assets.
