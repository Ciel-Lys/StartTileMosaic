using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace TileMosaic
{
    internal static class EditorTheme
    {
        public static readonly Color Surface = Color.White;
        public static readonly Color Workspace = Color.FromArgb(233, 239, 249);
        public static readonly Color EmptyTile = Color.FromArgb(242, 246, 252);
        public static readonly Color Line = Color.FromArgb(205, 215, 231);
        public static readonly Color Ink = Color.FromArgb(23, 38, 64);
        public static readonly Color Muted = Color.FromArgb(78, 95, 120);
        public static readonly Color Accent = Color.FromArgb(40, 85, 201);
        public static readonly Color AccentHover = Color.FromArgb(29, 66, 166);
        public static readonly Color Selection = Color.FromArgb(222, 232, 255);
        public static readonly Color SelectionOutline = Color.FromArgb(91, 125, 221);

        public static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
            path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
            path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
            path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class EditorButton : Button
    {
        public bool Primary { get; set; }
        public bool MenuIcon { get; set; }
        bool hovered, pressed;

        public EditorButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); pressed = e.Button == MouseButtons.Left; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); pressed = false; Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Parent == null ? EditorTheme.Surface : Parent.BackColor);
            if (Width < 5 || Height < 5) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float unit = g.DpiX / 96f;
            Color fill = EditorTheme.Surface, ink = EditorTheme.Ink, outline = EditorTheme.Line;
            if (!Enabled)
            {
                fill = EditorTheme.EmptyTile;
                ink = EditorTheme.Muted;
            }
            else if (Primary)
            {
                fill = hovered || pressed ? EditorTheme.AccentHover : EditorTheme.Accent;
                ink = Color.White;
                outline = fill;
            }
            else if (pressed) fill = EditorTheme.Selection;
            else if (hovered) fill = EditorTheme.EmptyTile;

            var bounds = new RectangleF(1, 1, Width - 3, Height - 3);
            using (var path = EditorTheme.RoundedRectangle(bounds, 8 * unit))
            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(Focused ? EditorTheme.Accent : outline, Focused ? 2 : 1))
            {
                g.FillPath(brush, path);
                g.DrawPath(border, path);
            }

            if (MenuIcon)
            {
                using (var pen = new Pen(ink, 1.6f * unit) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    for (int line = -1; line <= 1; line++)
                        g.DrawLine(pen, Width / 2f - 7 * unit, Height / 2f + line * 5 * unit,
                            Width / 2f + 7 * unit, Height / 2f + line * 5 * unit);
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, ink, TextFormatFlags.HorizontalCenter |
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            }
        }
    }
}
