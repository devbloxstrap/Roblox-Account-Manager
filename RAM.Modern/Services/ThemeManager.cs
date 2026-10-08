using System.Windows;
using System.Windows.Media;

namespace RAM.Modern.Services;

/// <summary>Runtime accent customization without changing security-related app behaviour.</summary>
public static class ThemeManager
{
    public static readonly string[] Presets = { "Aurora", "Ocean", "Amethyst", "Ember", "Emerald", "Custom" };

    public static void Apply(Window window, AdvancedSettings settings)
    {
        Color accent = GetAccent(settings);
        Color high = Lighten(accent, 0.35);
        Color low = Darken(accent, 0.30);
        foreach (string key in new[] { "ButtonBrush", "ButtonHoverBrush", "ActionGradient", "Accent" })
        {
            if (!window.Resources.Contains(key) || window.Resources[key] is not LinearGradientBrush brush || brush.GradientStops.Count == 0) continue;
            if (brush.IsFrozen) continue;
            int count = brush.GradientStops.Count;
            for (int i = 0; i < count; i++)
            {
                double t = count == 1 ? 0 : i / (double)(count - 1);
                brush.GradientStops[i].Color = Lerp(high, low, t);
            }
        }
    }

    private static Color GetAccent(AdvancedSettings settings)
    {
        var preset = settings.ThemePreset switch
        {
            "Ocean" => "#139DFF", "Amethyst" => "#A15AFF", "Ember" => "#F2854C",
            "Emerald" => "#2FCB9B", "Custom" => settings.CustomAccent,
            _ => "#4D9CFF"
        };
        return ParseColor(preset);
    }

    public static Color ParseColor(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length != 7 || input[0] != '#' ||
            !int.TryParse(input.AsSpan(1), System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            throw new ArgumentException("Accent color must be exactly #RRGGBB, e.g. #479EFF.");
        return Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
    }

    private static Color Lighten(Color c, double t) => Lerp(c, Colors.White, t);
    private static Color Darken(Color c, double t) => Lerp(c, Colors.Black, t);
    private static Color Lerp(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));
}
