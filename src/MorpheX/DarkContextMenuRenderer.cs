using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MorpheX;

/// <summary>
/// Custom dark-theme renderer for the system-tray context menu.
/// GDI resources (brushes, pens) are created once on construction and reused
/// across all paint calls. GraphicsPath geometry is cached per-dimensions so
/// the arc math in RoundedRect only runs when the menu size actually changes,
/// which is essentially never during a single session.
/// </summary>
internal sealed class DarkContextMenuRenderer : ToolStripProfessionalRenderer, IDisposable
{
    // ── Cached GDI objects ───────────────────────────────────────────────
    // All rendering occurs on the UI thread, so there is no concurrency concern.
    private readonly SolidBrush _backgroundBrush = new(Color.FromArgb(255, 32, 32, 32));
    private readonly SolidBrush _hoverBrush       = new(Color.FromArgb(255, 55, 55, 55));
    private readonly Pen        _borderPen         = new(Color.FromArgb(255, 58, 58, 58));
    private readonly Pen        _separatorPen      = new(Color.FromArgb(255, 58, 58, 58));

    // ── Cached path geometry ─────────────────────────────────────────────
    // Recomputed only when the dimensions change (practically never).
    private GraphicsPath? _borderPath;
    private Size          _borderSize;
    private GraphicsPath? _hoverPath;
    private Rectangle     _hoverBounds;

    public DarkContextMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        e.Graphics.FillRectangle(_backgroundBrush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);

        if (_borderPath == null || _borderSize != rect.Size)
        {
            _borderPath?.Dispose();
            _borderPath = RoundedRect(rect, 8);
            _borderSize = rect.Size;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(_borderPen, _borderPath);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;

        var bounds = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);

        if (_hoverPath == null || _hoverBounds != bounds)
        {
            _hoverPath?.Dispose();
            _hoverPath = RoundedRect(bounds, 4);
            _hoverBounds = bounds;
        }

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(_hoverBrush, _hoverPath);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled
            ? Color.FromArgb(255, 240, 240, 240)
            : Color.FromArgb(255, 140, 140, 140);
        base.OnRenderItemText(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        e.Graphics.DrawLine(_separatorPen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(255, 180, 180, 180);
        base.OnRenderArrow(e);
    }

    public void Dispose()
    {
        _backgroundBrush.Dispose();
        _hoverBrush.Dispose();
        _borderPen.Dispose();
        _separatorPen.Dispose();
        _borderPath?.Dispose();
        _hoverPath?.Dispose();
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var arc  = new Rectangle(bounds.Location, new Size(diameter, diameter));
        var path = new GraphicsPath();

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);

        path.CloseFigure();
        return path;
    }

    private sealed class DarkColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder                    => Color.FromArgb(255, 58, 58, 58);
        public override Color MenuItemBorder                => Color.Transparent;
        public override Color MenuItemSelected              => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuStripGradientBegin        => Color.FromArgb(255, 32, 32, 32);
        public override Color MenuStripGradientEnd          => Color.FromArgb(255, 32, 32, 32);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuItemSelectedGradientEnd   => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuItemPressedGradientBegin  => Color.FromArgb(255, 65, 65, 65);
        public override Color MenuItemPressedGradientEnd    => Color.FromArgb(255, 65, 65, 65);
        public override Color ImageMarginGradientBegin      => Color.FromArgb(255, 32, 32, 32);
        public override Color ImageMarginGradientMiddle     => Color.FromArgb(255, 32, 32, 32);
        public override Color ImageMarginGradientEnd        => Color.FromArgb(255, 32, 32, 32);
        public override Color SeparatorDark                 => Color.FromArgb(255, 58, 58, 58);
        public override Color SeparatorLight                => Color.FromArgb(255, 58, 58, 58);
        public override Color ToolStripDropDownBackground   => Color.FromArgb(255, 32, 32, 32);
    }
}
