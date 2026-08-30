using System.Text;
using SkiaSharp;
using Svg.Skia;

namespace Enzo.Diagrams.Rendering;

public static class FlowchartPngRenderer
{
    public static byte[] Render(string svg)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var skSvg = new SKSvg();

        if (skSvg.Load(stream) is null || skSvg.Picture is null)
        {
            throw new InvalidOperationException("SVG could not be rasterized.");
        }

        using var output = new MemoryStream();
        using var colorSpace = SKColorSpace.CreateSrgb();
        skSvg.Picture.ToImage(output, SKColors.Empty, SKEncodedImageFormat.Png, 100, 1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);

        return output.ToArray();
    }
}
