namespace Unfold.Core;

public sealed partial class GlbModel
{
    // Internal stroke scale: 100% is 10px at the reference 192px frame.
    // The fixed 1% stroke is .1px; supersampling and DPI scale it together.
    private const float OutlineWidthPercent = 1;

    private void ApplyOutline(int size, Span<uint> pixels, ReadOnlySpan<int> elements)
    {
        var width = size / 192f * (OutlineWidthPercent / 10f);
        var seeds = renderWorkspace.OutlineSeeds(pixels.Length);
        for (var i = 0; i < pixels.Length; i++) seeds[i] = pixels[i] >> 24 == 0 ? -1 : i;
        // Scale the coverage of a one-sample stroke below one raster pixel,
        // keeping very thin contours visible without widening them.
        var exteriorRadius = Math.Max(width, 1);
        var exteriorCoverage = Math.Min(width, 1);
        PropagateOutlineSeeds(size, seeds, exteriorRadius);
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            var index = y * size + x; var seed = seeds[index];
            if (seed < 0 || seed == index) continue;
            var dx = seed % size - x; var dy = seed / size - y;
            var coverage = Math.Clamp(exteriorRadius + .5f - MathF.Sqrt(dx * dx + dy * dy), 0, 1) * exteriorCoverage;
            pixels[index] = (uint)MathF.Round((pixels[seed] >> 24) * coverage) << 24;
        }
        ApplyElementOutlines(size, pixels, elements, seeds, width / 2);
    }

    private static void ApplyElementOutlines(int size, Span<uint> pixels, ReadOnlySpan<int> elements,
        Span<int> seeds, float radius)
    {
        var doubledSize = size * 2;
        var hasBoundary = false;
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            var index = y * size + x; var element = elements[index];
            seeds[index] = -1;
            if (element == 0) continue;
            // Seed the boundary halfway between visible samples, not at each
            // triangle edge. Hidden and transparent elements leave no IDs.
            if (x + 1 < size && elements[index + 1] != 0 && elements[index + 1] != element)
                seeds[index] = y * 2 * doubledSize + x * 2 + 1;
            else if (y + 1 < size && elements[index + size] != 0 && elements[index + size] != element)
                seeds[index] = (y * 2 + 1) * doubledSize + x * 2;
            else if (x > 0 && elements[index - 1] != 0 && elements[index - 1] != element)
                seeds[index] = y * 2 * doubledSize + x * 2 - 1;
            else if (y > 0 && elements[index - size] != 0 && elements[index - size] != element)
                seeds[index] = (y * 2 - 1) * doubledSize + x * 2;
            hasBoundary |= seeds[index] >= 0;
        }
        if (!hasBoundary) return;
        PropagateOutlineSeeds(size, seeds, radius, coordinateScale: 2, elements: elements);
        for (var y = 0; y < size; y++) for (var x = 0; x < size; x++)
        {
            var index = y * size + x; var seed = seeds[index];
            if (elements[index] == 0 || seed < 0) continue;
            var dx = seed % doubledSize - x * 2; var dy = seed / doubledSize - y * 2;
            var coverage = Math.Clamp(radius + .5f - MathF.Sqrt(dx * dx + dy * dy) / 2, 0, 1);
            if (coverage == 0) continue;
            var color = pixels[index]; var keep = (uint)MathF.Round((1 - coverage) * 255);
            pixels[index] = color & 0xFF000000 | (((color >> 16) & 255) * keep / 255) << 16 |
                (((color >> 8) & 255) * keep / 255) << 8 | (color & 255) * keep / 255;
        }
    }

    private static void PropagateOutlineSeeds(int size, Span<int> seeds, float radius, int coordinateScale = 1,
        ReadOnlySpan<int> elements = default)
    {
        var limit = (int)MathF.Ceiling((radius + .5f) * coordinateScale);
        var limitSquared = limit * limit + 1;
        var coordinateSize = size * coordinateScale;
        // Two bounded distance scans reuse the same seed buffer for exterior
        // and element contours. Work remains linear in the frame size.
        for (var pass = 0; pass < 2; pass++)
        {
            var step = pass == 0 ? 1 : -1;
            for (var y = pass == 0 ? 0 : size - 1; y >= 0 && y < size; y += step)
            for (var x = pass == 0 ? 0 : size - 1; x >= 0 && x < size; x += step)
            {
                var index = y * size + x; var nearest = -1; var distance = limitSquared;
                // Internal lines only affect existing surfaces. Empty background
                // needs no distance work and cannot bridge two separate parts.
                if (!elements.IsEmpty && elements[index] == 0) continue;
                var px = x * coordinateScale; var py = y * coordinateScale;
                ConsiderOutlineSeed(seeds[index], px, py, coordinateSize, ref nearest, ref distance);
                if (distance != 0)
                {
                    if (x - step >= 0 && x - step < size)
                        ConsiderOutlineSeed(seeds[index - step], px, py, coordinateSize, ref nearest, ref distance);
                    if (y - step >= 0 && y - step < size)
                        for (var dx = -1; dx <= 1; dx++)
                            if (x + dx >= 0 && x + dx < size)
                                ConsiderOutlineSeed(seeds[index - step * size + dx], px, py, coordinateSize, ref nearest, ref distance);
                }
                seeds[index] = nearest;
            }
        }
    }

    private static void ConsiderOutlineSeed(int seed, int x, int y, int size, ref int nearest, ref int distance)
    {
        if (seed < 0) return;
        var dx = seed % size - x; var dy = seed / size - y;
        var candidate = dx * dx + dy * dy;
        if (candidate >= distance) return;
        nearest = seed; distance = candidate;
    }
}
