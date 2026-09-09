using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MorpheX;

internal sealed class DarkContextMenuRenderer : ToolStripProfessionalRenderer
{
    public DarkContextMenuRenderer() : base(new DarkColorTable()) { }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new SolidBrush(Color.FromArgb(255, 32, 32, 32));
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new Pen(Color.FromArgb(255, 58, 58, 58));
        var rect = new Rectangle(0, 0, e.AffectedBounds.Width - 1, e.AffectedBounds.Height - 1);
        using var path = RoundedRect(rect, 8);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        var item = e.Item;
        var g = e.Graphics;
        var bounds = new Rectangle(4, 1, item.Width - 8, item.Height - 2);

        if (item.Selected && item.Enabled)
        {
            using var brush = new SolidBrush(Color.FromArgb(255, 55, 55, 55));
            using var path = RoundedRect(bounds, 4);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.FillPath(brush, path);
        }
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
        var g = e.Graphics;
        int y = e.Item.Height / 2;
        using var pen = new Pen(Color.FromArgb(255, 58, 58, 58));
        g.DrawLine(pen, 12, y, e.Item.Width - 12, y);
    }

    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
    {
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = Color.FromArgb(255, 180, 180, 180);
        base.OnRenderArrow(e);
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
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
        public override Color MenuBorder => Color.FromArgb(255, 58, 58, 58);
        public override Color MenuItemBorder => Color.Transparent;
        public override Color MenuItemSelected => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuStripGradientBegin => Color.FromArgb(255, 32, 32, 32);
        public override Color MenuStripGradientEnd => Color.FromArgb(255, 32, 32, 32);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(255, 55, 55, 55);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(255, 65, 65, 65);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(255, 65, 65, 65);
        public override Color ImageMarginGradientBegin => Color.FromArgb(255, 32, 32, 32);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(255, 32, 32, 32);
        public override Color ImageMarginGradientEnd => Color.FromArgb(255, 32, 32, 32);
        public override Color SeparatorDark => Color.FromArgb(255, 58, 58, 58);
        public override Color SeparatorLight => Color.FromArgb(255, 58, 58, 58);
        public override Color ToolStripDropDownBackground => Color.FromArgb(255, 32, 32, 32);
    }
}
