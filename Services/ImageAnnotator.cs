using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace WeatherImageGenerator.Services;

public sealed class ImageAnnotator
{
    private readonly Font _fontLarge;
    private readonly Font _fontMedium;

    public ImageAnnotator()
    {
        // Use a built-in font family as fallback if no custom font file is present.
        // If you want a specific TTF, add it to the repo and load via collection.Add("path.ttf").
        var family = SystemFonts.Families.FirstOrDefault(f => f.Name.Contains("Arial", StringComparison.OrdinalIgnoreCase));
        if (family == default)
        {
            family = SystemFonts.Collection.Families.First();
        }

        _fontLarge = family.CreateFont(56, FontStyle.Regular);
        _fontMedium = family.CreateFont(48, FontStyle.Regular);
    }

    public Image<Rgba32> Annotate(Image<Rgba32> background, string stationName, string temperatureC, string conditionText)
    {
        // Draw a subtle dark overlay behind the text to improve contrast
        background.Mutate(ctx =>
        {
            // Top-left overlay rectangle
            ctx.Fill(Color.Black.WithAlpha(0.25f), new RectangleF(0, 0, background.Width, 220));

            var padding = 30f;
            var x = padding;
            var y = padding;

            ctx.DrawText(stationName, _fontLarge, Color.White, new PointF(x, y));
            y += 70;
            ctx.DrawText(temperatureC, _fontMedium, Color.White, new PointF(x, y));
            y += 70;
            ctx.DrawText(conditionText, _fontMedium, Color.White, new PointF(x, y));
        });

        return background;
    }
}
