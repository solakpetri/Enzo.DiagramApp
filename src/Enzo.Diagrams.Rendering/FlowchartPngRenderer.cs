using System.Text;
using SkiaSharp;
using Svg.Skia;
using Svg.Skia.TypefaceProviders;

namespace Enzo.Diagrams.Rendering;

public static class FlowchartPngRenderer
{
    private const string BundledFontResourceName = "Enzo.Diagrams.Rendering.Assets.Fonts.DejaVuSans.ttf";

    private static readonly Lazy<ITypefaceProvider> BundledTypefaceProvider = new(CreateBundledTypefaceProvider);

    public static byte[] Render(string svg)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        using var skSvg = new SKSvg();

        try
        {
            var settings = skSvg.Settings ?? throw new FlowchartPngRenderException("SVG could not be rasterized.");
            var typefaceProviders = settings.TypefaceProviders ?? throw new FlowchartPngRenderException("SVG could not be rasterized.");
            typefaceProviders.Insert(0, BundledTypefaceProvider.Value);

            if (skSvg.Load(stream) is null || skSvg.Picture is null)
            {
                throw new FlowchartPngRenderException("SVG could not be rasterized.");
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
