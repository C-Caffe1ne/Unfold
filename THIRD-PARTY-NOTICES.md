# Third-party components in the C# desktop build

- **Avalonia 12.1.2** and Fluent theme — MIT; copyright the AvaloniaUI contributors.
  Source and license: https://github.com/AvaloniaUI/Avalonia
- **SkiaSharp 3.119.4** — MIT; copyright Microsoft and contributors.
  Source and license: https://github.com/mono/SkiaSharp
- **Skia** native rendering library — BSD-style; copyright Google and contributors.
  License: https://skia.googlesource.com/skia/+/main/LICENSE
- **HarfBuzz / HarfBuzzSharp** text shaping — upstream MIT-style licenses.
  Sources: https://github.com/harfbuzz/harfbuzz and https://github.com/mono/SkiaSharp
- **.NET runtime** (self-contained distributions) — MIT; copyright .NET contributors.
  Source and notices: https://github.com/dotnet/runtime

Dependency packages retain their upstream notices. Published .NET runtime files
include the runtime's LICENSE and ThirdPartyNotices documents. Exact dependency
versions are recorded in NuGet lock files.

The Piskel web runtime is not included in the C# build, and it has been removed
from the Swift app as well — neither build vendors Piskel code. The Swift app
still reads and writes the `.piskel` file format (model version 2 JSON), but a
file format is not itself a licensed work, so no Apache-2.0 notice is required
now that the vendored runtime is gone. Character assets are the existing project
assets, not third-party assets introduced by this migration.
