using System.Text;
using SkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;

namespace Enzo.Diagrams.Rendering;

public static class FlowchartPngRenderer
{
    private const string BundledFontResourceName = "Enzo.Diagrams.Rendering.Assets.Fonts.DejaVuSans.ttf";
    private const int DefaultMaxWidth = 8_000;
    private const int DefaultMaxHeight = 8_000;
    private const int DefaultMaxPixels = 16_000_000;

    private static readonly Lazy<ITypefaceProvider> BundledTypefaceProvider = new(CreateBundledTypefaceProvider);

    public static byte[] Render(string svg)
    {
        return Render(svg, DefaultMaxWidth, DefaultMaxHeight, DefaultMaxPixels);
    }

    public static byte[] Render(string svg, int maxWidth, int maxHeight, int maxPixels)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var skSvg = new SKSvg();

        try
        {
            ConfigureTypefaceProvider(skSvg);

            if (skSvg.Load(stream) is null || skSvg.Picture is null)
            {
                throw new FlowchartPngRenderException("SVG could not be rasterized.");
            }

            var bounds = skSvg.Picture.CullRect;
            var width = Math.Ceiling(bounds.Width);
            var height = Math.Ceiling(bounds.Height);
            if (width <= 0 || height <= 0 || width > maxWidth || height > maxHeight || width * height > maxPixels)
            {
                throw new FlowchartPngRenderException("SVG dimensions exceed configured PNG rendering limits.");
            }

            using var output = new MemoryStream();
            using var colorSpace = SKColorSpace.CreateSrgb();
            skSvg.Picture.ToImage(output, SKColors.Empty, SKEncodedImageFormat.Png, 100, 1f, 1f, SKColorType.Rgba8888, SKAlphaType.Premul, colorSpace);

            return output.ToArray();
        }
        catch (FlowchartPngRenderException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new FlowchartPngRenderException("SVG could not be rasterized.", exception);
        }
    }

    private static void ConfigureTypefaceProvider(SKSvg skSvg)
    {
        var settings = skSvg.Settings
            ?? throw new FlowchartPngRenderException("SVG rasterizer settings are unavailable.");

        settings.TypefaceProviders ??= [];
        settings.TypefaceProviders.Insert(0, BundledTypefaceProvider.Value);
    }

    private static ITypefaceProvider CreateBundledTypefaceProvider()
    {
        using var fontStream = typeof(FlowchartPngRenderer).Assembly.GetManifestResourceStream(BundledFontResourceName)
            ?? throw new FlowchartPngRenderException("Bundled PNG font could not be loaded.");

        var provider = new CustomTypefaceProvider(fontStream, 0)
        {
            FamilyName = "DejaVu Sans"
        };
        provider.FamilyAliases.Add("Arial");
        provider.FamilyAliases.Add("sans-serif");

        return provider;
    }
}
