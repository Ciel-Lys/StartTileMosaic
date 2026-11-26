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
            var fill = Enabled && Primary ? (hovered || pressed ? EditorTheme.AccentHover : EditorTheme.Accent)
                : Enabled && pressed ? EditorTheme.Selection : !Enabled || hovered ? EditorTheme.EmptyTile : EditorTheme.Surface;
            var ink = Enabled && Primary ? Color.White : Enabled ? EditorTheme.Ink : EditorTheme.Muted;
            float radius = Math.Min(8 * g.DpiX / 96f, (Height - 4) / 2f);
            using (var path = new GraphicsPath())
            using (var brush = new SolidBrush(fill))
            using (var border = new Pen(Focused ? EditorTheme.Accent : Primary ? fill : EditorTheme.Line, Focused ? 2 : 1))
            {
                float d = radius * 2, right = Width - 2, bottom = Height - 2;
                path.AddArc(1, 1, d, d, 180, 90);
                path.AddArc(right - d, 1, d, d, 270, 90);
                path.AddArc(right - d, bottom - d, d, d, 0, 90);
                path.AddArc(1, bottom - d, d, d, 90, 90);
                path.CloseFigure();
                g.FillPath(brush, path);
                g.DrawPath(border, path);
            }

            if (MenuIcon)
            {
                float unit = g.DpiX / 96f;
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
