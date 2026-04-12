using Cairo;
using Gdk;
using Gtk;

namespace Industriality.UI.Gtk.Widgets;

public sealed class RoundedImage : DrawingArea
{
    private readonly Pixbuf _source;
    private readonly double _cornerRadius;
    private readonly double _zoomFactor;

    public RoundedImage(string imagePath, double cornerRadius = 16d, double zoomFactor = 1.15d)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path must be provided.", nameof(imagePath));
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Rounded image source was not found.", imagePath);
        }

        _source = new Pixbuf(imagePath);
        _cornerRadius = Math.Max(0d, cornerRadius);
        _zoomFactor = Math.Max(1d, zoomFactor);
    }

    protected override bool OnDrawn(Context context)
    {
        var width = Allocation.Width;
        var height = Allocation.Height;

        if (width <= 0 || height <= 0)
        {
            return true;
        }

        AddRoundedRectanglePath(context, 0, 0, width, height, _cornerRadius);
        context.Clip();

        var coverScale = Math.Max(
            (double)width / _source.Width,
            (double)height / _source.Height) * _zoomFactor;

        var drawWidth = Math.Max(1, (int)Math.Ceiling(_source.Width * coverScale));
        var drawHeight = Math.Max(1, (int)Math.Ceiling(_source.Height * coverScale));
        var drawX = (width - drawWidth) / 2;
        var drawY = (height - drawHeight) / 2;

        using var scaled = _source.ScaleSimple(drawWidth, drawHeight, InterpType.Bilinear);
        Gdk.CairoHelper.SetSourcePixbuf(context, scaled, drawX, drawY);
        context.Paint();

        return true;
    }

    protected override void OnDestroyed()
    {
        _source.Dispose();
        base.OnDestroyed();
    }

    private static void AddRoundedRectanglePath(Context context, double x, double y, double width, double height, double radius)
    {
        var clampedRadius = Math.Min(radius, Math.Min(width, height) / 2d);

        context.NewSubPath();
        context.Arc(x + width - clampedRadius, y + clampedRadius, clampedRadius, -Math.PI / 2d, 0d);
        context.Arc(x + width - clampedRadius, y + height - clampedRadius, clampedRadius, 0d, Math.PI / 2d);
        context.Arc(x + clampedRadius, y + height - clampedRadius, clampedRadius, Math.PI / 2d, Math.PI);
        context.Arc(x + clampedRadius, y + clampedRadius, clampedRadius, Math.PI, 3d * Math.PI / 2d);
        context.ClosePath();
    }
}
