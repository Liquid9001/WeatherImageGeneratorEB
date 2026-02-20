using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace WeatherImageGenerator.Services;

public sealed class ImageAnnotator
{
    private readonly FontFamily _fontFamily; // Added to create new font sizes on the fly
    private readonly Font _fontLarge;
    private readonly Font _fontMedium;

    public ImageAnnotator()
    {
        // Use a built-in font family as fallback if no custom font file is present.
        var family = SystemFonts.Families.FirstOrDefault(f => f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase));
        if (family == default)
        {
            family = SystemFonts.Collection.Families.First();
        }

        _fontFamily = family; // Save the family for our dynamic resizing

        _fontLarge = family.CreateFont(56, FontStyle.Regular);
        _fontMedium = family.CreateFont(48, FontStyle.Regular);
    }

    public Image<Rgba32> Annotate(Image<Rgba32> background, string stationName, string temperatureC, string conditionText)
    {
        if (stationName.StartsWith("Meetstation Meetstation", StringComparison.OrdinalIgnoreCase))
        {
            stationName = stationName.Replace("Meetstation Meetstation", "Meetstation", StringComparison.OrdinalIgnoreCase);
        }

        background.Mutate(ctx =>
        {
            // Top-left overlay rectangle
            ctx.Fill(Color.Black.WithAlpha(0.25f), new RectangleF(0, 0, background.Width, 220));

            var padding = 30f;
            var x = padding;
            var y = padding;

            // --- DYNAMIC FONT SIZING FOR STATION NAME ---
            float maxTextWidth = background.Width - (padding * 2);
            float currentFontSize = 56f; // Start at the default large size
            Font dynamicStationFont = _fontFamily.CreateFont(currentFontSize, FontStyle.Regular);

            // Measure the text. If it's too wide, shrink the font by 2 points and check again!
            while (TextMeasurer.MeasureBounds(stationName, new TextOptions(dynamicStationFont)).Width > maxTextWidth && currentFontSize > 20f)
            {
                currentFontSize -= 2f;
                dynamicStationFont = _fontFamily.CreateFont(currentFontSize, FontStyle.Regular);
            }

            ctx.DrawText(stationName, dynamicStationFont, Color.White, new PointF(x, y));
            y += 70;
            ctx.DrawText(temperatureC, _fontMedium, Color.White, new PointF(x, y));
            y += 70;
            ctx.DrawText(conditionText, _fontMedium, Color.White, new PointF(x, y));
        });

        return background;
    }
}