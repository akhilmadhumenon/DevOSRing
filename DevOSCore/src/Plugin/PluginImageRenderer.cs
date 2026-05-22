using System;
using Loupedeck;

namespace DevOSRing.Core.Hosting;

/// <summary>
/// Draws action button images for each <see cref="ActionState"/>. Uses Loupedeck's
/// <see cref="BitmapBuilder"/> so the output matches device DPI without us shipping PNGs.
/// </summary>
public static class PluginImageRenderer
{
    public static BitmapImage Render(string title, ActionState state, string status, PluginImageSize size)
    {
        var w = size.GetWidth();
        var h = size.GetHeight();
        using var bb = new BitmapBuilder(w, h);

        var (bg, accent) = ColoursFor(state);
        bb.Clear(bg);
        DrawAccentStripe(bb, accent);

        var fontSize = SelectFontSize(size);
        var statusFontSize = Math.Max(8, fontSize - 4);

        bb.DrawText(title, x: 0, y: 4, width: w, height: h / 2,
            color: BitmapColor.White, fontSize: fontSize);

        if (!string.IsNullOrEmpty(status))
        {
            bb.DrawText(status, x: 0, y: h / 2, width: w, height: h / 2 - 4,
                color: accent, fontSize: statusFontSize);
        }

        return bb.ToImage();
    }

    private static (BitmapColor bg, BitmapColor accent) ColoursFor(ActionState s) => s switch
    {
        ActionState.Busy    => (new BitmapColor(20, 20, 28),  new BitmapColor(255, 196, 0)),
        ActionState.Success => (new BitmapColor(10, 32, 16),  new BitmapColor( 90, 220, 90)),
        ActionState.Error   => (new BitmapColor(40, 12, 12),  new BitmapColor(240, 100, 80)),
        _                   => (new BitmapColor(18, 18, 24),  new BitmapColor(120, 160, 240)),
    };

    private static void DrawAccentStripe(BitmapBuilder bb, BitmapColor accent)
    {
        var w = bb.Width;
        var stripe = Math.Max(2, w / 32);
        bb.DrawRectangle(0, bb.Height - stripe, w, stripe, accent);
    }

    private static int SelectFontSize(PluginImageSize s) => s switch
    {
        PluginImageSize.Width60  => 11,
        PluginImageSize.Width90  => 14,
        PluginImageSize.Width116 => 18,
        _ => 14,
    };
}
