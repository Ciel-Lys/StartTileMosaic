using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TileMosaic
{
    public sealed class GenieCanvas:Control
    {
        public Project Settings;public Bitmap Picture;public bool Numbers;
        public event Action Changed;
        public event Action SelectionChanged;
        public event Action LayoutChanged;
        public Point? SelectedTile { get; private set; }
        readonly ToolTip tips = new ToolTip();
        readonly ContextMenuStrip addTileMenu = new ContextMenuStrip();
        readonly Timer tileHold = new Timer { Interval = 350 };
        readonly Timer dragScroll = new Timer { Interval = 35 };
        Point pressLocation, previous;
        bool pressed, canDrag, dragging;
        FreeTileLayout rememberedFreeLayout;
        int pendingTile = -1, movingTile = -1;
        Point? dropCell;
        float scale = 1;
        const int LeftMargin = 60, TopMargin = 108, ToolStep = 46;
        const int ModeButtonY = 10, LayoutActionX = 276, ToolbarWidth = 376;

        public GenieCanvas()
        {
            DoubleBuffered = true;
            BackColor = EditorTheme.Workspace;
            ForeColor = EditorTheme.Ink;
            TabStop = true;
            AccessibleName = UiText.Get("磁贴画布");
            tileHold.Tick += ArmTileMove;
            dragScroll.Tick += ScrollWhileDragging;
        }

        sealed class EdgeButton : Button
        {
            public bool WideIcon, SquareIcon;

            public EdgeButton()
            {
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                UseVisualStyleBackColor = false;
                SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque |
                    ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                // A button can be painted repeatedly into a buffer last used by another control.
                g.Clear(BackColor);
                var state = g.Save();
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float unit = Math.Min(ClientSize.Width, ClientSize.Height) / 38f;
                g.ScaleTransform(unit, unit);
                bool highlighted = Enabled && (Focused || ClientRectangle.Contains(PointToClient(MousePosition)));
                var ink = Enabled ? EditorTheme.Accent : EditorTheme.Muted;
                var fill = highlighted ? EditorTheme.Selection : EditorTheme.Surface;
                using (var brush = new SolidBrush(fill))
                    g.FillEllipse(brush, 2, 2, 34, 34);
                using (var outline = new Pen(highlighted ? EditorTheme.Accent : EditorTheme.Line, 1.3f))
                    g.DrawEllipse(outline, 2, 2, 34, 34);
                using (var pen = new Pen(ink, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    if (WideIcon || SquareIcon)
                    {
                        int w = WideIcon ? 20 : 12;
                        g.DrawRectangle(pen, (38 - w) / 2f, 13, w, 12);
                    }
                    else
                    {
                        g.DrawLine(pen, 10, 19, 27, 19);
                        if (Text == "+") g.DrawLine(pen, 19, 10, 19, 27);
                    }
                }
                g.Restore(state);
            }
        }

        void Edge(string symbol, string description, int x, int y, bool enabled, Action action)
        {
            var button = new EdgeButton {
                Text = symbol, AccessibleName = description, Cursor = Cursors.Hand,
                WideIcon = symbol == "wide", SquareIcon = symbol == "square",
                Enabled = enabled, BackColor = BackColor,
                Bounds = ScaledBounds(x, y, 38, 38)
            };
            tips.SetToolTip(button, description);
            button.Click += delegate { action(); RefreshLayout(); };
            Controls.Add(button);
        }

        Rectangle ScaledBounds(int x, int y, int width, int height)
        {
            return Rectangle.Round(new RectangleF(x * scale, y * scale, width * scale, height * scale));
        }

        EditorButton ToolbarButton(string name, string label, int x, int width, bool active)
        {
            var button = new EditorButton { Name = name, Text = UiText.Get(label), Primary = active,
                Cursor = Cursors.Hand, Bounds = ScaledBounds(x, ModeButtonY, width, 34) };
            Controls.Add(button);
            return button;
        }

        void SetWidths(List<int> widths)
        {
            Settings.ColumnWidths = widths.ToArray();
            Settings.Columns = widths.Count;
        }

        public void SelectTile(Point? tile)
        {
            if (SelectedTile == tile) return;
            SelectedTile = tile;
            Invalidate();
            if (SelectionChanged != null) SelectionChanged();
        }

        internal void SetBinding(TileBinding binding, bool overwriteAll)
        {
            Settings.SetBinding(SelectedTile, binding, overwriteAll);
            if (overwriteAll && !SelectedTile.HasValue && Settings.FreeLayout == null && rememberedFreeLayout != null)
                rememberedFreeLayout.ClearBindings();
        }

        void ShiftBindings(bool row, int index)
        {
            Settings.ShiftBindingsAfterRemoval(row, index);
            if (SelectedTile.HasValue)
                SelectTile(Project.TileAfterRemoval(SelectedTile.Value, row, index));
        }

        Rectangle TileBounds(Point tile)
        {
            var bounds = Settings.BoundsAt(tile);
            bounds.Offset(LeftMargin, TopMargin);
            return bounds;
        }

        Point? HitTile(Point location)
        {
            var point = new Point((int)Math.Floor(location.X / scale), (int)Math.Floor(location.Y / scale));
            for (int i = 0; i < Settings.TileCount; i++)
            {
                var tile = Settings.TileKeyAt(i);
                if (TileBounds(tile).Contains(point)) return tile;
            }
            return null;
        }

        void SetFreeMode(bool enabled)
        {
            if (enabled == (Settings.FreeLayout != null)) return;
            EndPointer();
            Capture = false;
            if (!enabled)
            {
                rememberedFreeLayout = Settings.FreeLayout;
                Settings.FreeLayout = null;
            }
            else
            {
                if (rememberedFreeLayout == null) rememberedFreeLayout = new FreeTileLayout();
                Settings.FreeLayout = rememberedFreeLayout;
            }
            SelectTile(null);
            RefreshLayout();
            var workspace = Parent as ScrollableControl;
            if (workspace != null) workspace.AutoScrollPosition = Point.Empty;
        }

        void ShowAddTileMenu(Control anchor)
        {
            foreach (ToolStripItem old in addTileMenu.Items.Cast<ToolStripItem>().ToArray()) old.Dispose();
            addTileMenu.Items.Add(UiText.Get("中（150 × 150）"), null, delegate { AddFreeTile(TileShape.Medium); });
            addTileMenu.Items.Add(UiText.Get("宽（310 × 150）"), null, delegate { AddFreeTile(TileShape.Wide); });
            addTileMenu.Items.Add(UiText.Get("大（310 × 310）"), null, delegate { AddFreeTile(TileShape.Large); });
            addTileMenu.Show(anchor, new Point(0, anchor.Height));
        }

        void AddFreeTile(TileShape shape)
        {
            var layout = Settings.FreeLayout;
            if (layout == null || layout.Count >= FreeTileLayout.MaxTiles) return;
            layout.Add(shape);
            SelectTile(new Point(layout.Count - 1, 0));
            RefreshLayout();
        }

        Size FrameSize()
        {
            if (Settings.TileCount == 0) return new Size(310, 150);
            var size = new Size(Settings.Width, Settings.Height);
            if (dropCell.HasValue)
            {
                var bounds = DropBounds();
                size.Width = Math.Max(size.Width, bounds.Right - LeftMargin);
                size.Height = Math.Max(size.Height, bounds.Bottom - TopMargin);
            }
            return size;
        }

        void UpdateCanvasSize()
        {
            var frame = FrameSize();
            int width = Math.Max(LeftMargin + frame.Width + 12, ToolbarWidth);
            int height = TopMargin + frame.Height + 12;
            if (dragging && movingTile >= 0)
            {
                int pitch = 150 + Settings.Gap;
                int maxWidth = LeftMargin + Settings.FreeLayout.Columns * pitch - Settings.Gap + 12;
                int maxHeight = TopMargin + FreeTileLayout.MaxRows * pitch - Settings.Gap + 12;
                width = Math.Max(width, Math.Min(width + pitch, maxWidth));
                height = Math.Min(height + pitch, maxHeight);
            }
            var size = new Size((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale));
            if (dragging && movingTile >= 0)
                size = new Size(Math.Max(Width, size.Width), Math.Max(Height, size.Height));
            if (Size != size) Size = size;
        }

        public void RefreshLayout()
        {
            AccessibleName = UiText.Get("磁贴画布");
            using (var g = CreateGraphics()) scale = g.DpiX / 96f;
            SuspendLayout();
            var focused = Controls.Cast<Control>().FirstOrDefault(button => button.Focused);
            string focus = focused == null ? null : focused.Name;
            foreach (Control old in Controls.Cast<Control>().ToArray()) old.Dispose();
            var layout = Settings.FreeLayout;
            UpdateCanvasSize();
            ToolbarButton("grid", "行列布局", LeftMargin, 94, layout == null).Click += delegate { SetFreeMode(false); };
            ToolbarButton("free", "自由组合", LeftMargin + 100, 100, layout != null).Click += delegate { SetFreeMode(true); };
            if (layout == null)
            {
                var size = ToolbarButton("large", "大磁贴", LayoutActionX, 92, Settings.LargeTiles);
                tips.SetToolTip(size, UiText.Get(Settings.LargeTiles ? "退出大磁贴模式" : "将画布切换为大磁贴（310 × 310）"));
                size.Click += delegate { Settings.LargeTiles = !Settings.LargeTiles; RefreshLayout(); };
                RegularToolbar();
            }
            else
            {
                var add = ToolbarButton("add", "添加磁贴 ▾", LayoutActionX, 92, false);
                add.Enabled = layout.Count < FreeTileLayout.MaxTiles;
                add.Click += delegate { ShowAddTileMenu(add); };
                Controls.Add(new Label { Text = UiText.Get("长按左键后拖动 · 右键删除"),
                    AutoEllipsis = true, ForeColor = EditorTheme.Muted, TextAlign = ContentAlignment.MiddleLeft,
                    Bounds = ScaledBounds(LeftMargin, 57, Math.Max(310, Settings.Width), 32) });
            }
            ResumeLayout();
            if (!string.IsNullOrEmpty(focus))
            {
                var replacement = Controls[focus];
                if (replacement != null) replacement.Focus();
            }
            Invalidate();
            if (LayoutChanged != null) LayoutChanged();
        }

        void RegularToolbar()
        {
            var widths = Enumerable.Range(0, Settings.Columns).Select(column => Settings.IsWide(column) ? 310 : 150).ToList();
            Edge("+", UiText.Get("添加一列"), 12, 57, widths.Count < 6,
                delegate { widths.Add(310); SetWidths(widths); });
            for (int col = 0; col < Settings.Columns; col++)
            {
                int groupWidth = Settings.LargeTiles ? 38 : ToolStep + 38;
                int index = col, x = LeftMargin + Settings.LeftAt(col) + (Settings.WidthAt(col) - groupWidth) / 2;
                Edge("-", UiText.Format("删除第 {0} 列", col + 1), x, 57, widths.Count > 1,
                    delegate { widths.RemoveAt(index); SetWidths(widths); ShiftBindings(false, index); });
                if (!Settings.LargeTiles)
                {
                    bool wide = Settings.IsWide(col);
                    Edge(wide ? "square" : "wide", UiText.Format(wide ? "将第 {0} 列切换为方形" : "将第 {0} 列切换为宽磁贴", col + 1),
                        x + ToolStep, 57, true, delegate { widths[index] = wide ? 150 : 310; SetWidths(widths); });
                }
            }
            for (int row = 0; row < Settings.Rows; row++)
            {
                int index = row;
                int y = TopMargin + row * (Settings.TileHeight + Settings.Gap)
                    + (Settings.TileHeight - 150) / 2 + (row == 0 ? 32 : 56);
                if (row == 0)
                {
                    Edge("+", UiText.Get("添加一行"), 12, y, Settings.Rows < 8, delegate { Settings.Rows++; });
                    y += 48;
                }
                Edge("-", UiText.Format("删除第 {0} 行", row + 1), 12, y, Settings.Rows > 1,
                    delegate { Settings.Rows--; ShiftBindings(true, index); });
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Settings == null) return;
            var g = e.Graphics;
            Images.Quality(g);
            g.ScaleTransform(scale, scale);
            var frameSize = FrameSize();
            var frame = new Rectangle(LeftMargin - 7, TopMargin - 7, frameSize.Width + 14, frameSize.Height + 14);
            using (var shadow = new SolidBrush(Color.FromArgb(18, EditorTheme.Ink)))
                g.FillRectangle(shadow, frame.X, frame.Y + 3, frame.Width, frame.Height);
            g.FillRectangle(Brushes.White, frame);
            using (var border = new Pen(EditorTheme.Line)) g.DrawRectangle(border, frame);
            if (Settings.TileCount == 0)
            {
                using (var text = new SolidBrush(EditorTheme.Muted))
                using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(UiText.Get("选择「添加磁贴」开始"), Font, text, frame, format);
                return;
            }
            using (var empty = new SolidBrush(EditorTheme.EmptyTile))
            using (var line = new Pen(EditorTheme.Line))
            for (int i = 0; i < Settings.TileCount; i++)
            {
                var tile = Settings.TileKeyAt(i);
                var rect = TileBounds(tile);
                if (Picture != null)
                    g.DrawImage(Picture, rect, Images.TileCrop(Picture.Size, Settings, tile), GraphicsUnit.Pixel);
                else
                {
                    g.FillRectangle(empty, rect);
                    if (Settings.Gap > 0 || Settings.FreeLayout != null)
                        g.DrawRectangle(line, rect.X, rect.Y, rect.Width - 1, rect.Height - 1);
                }
                if (Numbers)
                {
                    using (var brush = new SolidBrush(Color.FromArgb(150, 15, 27, 40)))
                        g.FillRectangle(brush, rect.X + 5, rect.Y + 5, 58, 24);
                    string number = Settings.FreeLayout == null
                        ? (tile.Y + 1).ToString("D2") + "–" + (tile.X + 1).ToString("D2") : (i + 1).ToString("D2");
                    g.DrawString(number, Font, Brushes.White, rect.X + 9, rect.Y + 8);
                }
                if (Settings.ShowNames && Picture != null)
                    using (var brush = new SolidBrush(Color.FromArgb(135, 15, 27, 40)))
                    {
                        g.FillRectangle(brush, rect.X, rect.Bottom - 24, rect.Width, 24);
                        g.DrawString(Project.TileDisplayName, Font, Brushes.White, rect.X + 5, rect.Bottom - 22);
                    }
            }
            if (SelectedTile.HasValue)
            {
                RectangleF bounds = TileBounds(SelectedTile.Value);
                bounds.Inflate(-3, -3);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var outline = EditorTheme.RoundedRectangle(bounds, 7))
                using (var halo = new Pen(Color.FromArgb(205, Color.White), 3.5f))
                using (var stroke = new Pen(EditorTheme.SelectionOutline, 1.75f))
                {
                    g.DrawPath(halo, outline);
                    g.DrawPath(stroke, outline);
                }
            }
            if (dropCell.HasValue)
            {
                var bounds = DropBounds();
                using (var pen = new Pen(EditorTheme.Accent, 2) { DashStyle = DashStyle.Dash })
                using (var fill = new SolidBrush(Color.FromArgb(60, EditorTheme.Accent)))
                {
                    g.FillRectangle(fill, bounds);
                    g.DrawRectangle(pen, bounds);
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EndPointer();
                tileHold.Dispose();
                dragScroll.Dispose();
                addTileMenu.Dispose();
                tips.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || Settings == null || pressed) return;
            Focus();
            pressLocation = previous = e.Location;
            pressed = true;
            canDrag = Picture != null && Settings.TileCount > 0
                && new Rectangle(LeftMargin, TopMargin, Settings.Width, Settings.Height)
                .Contains((int)(e.X / scale), (int)(e.Y / scale));
            Capture = true;
            if (Settings.FreeLayout != null)
            {
                var tile = HitTile(e.Location);
                if (tile.HasValue)
                {
                    pendingTile = tile.Value.X;
                    tileHold.Start();
                }
            }
        }

        void ArmTileMove(object sender, EventArgs e)
        {
            tileHold.Stop();
            if (!pressed || !Capture || pendingTile < 0) return;
            movingTile = pendingTile;
            pendingTile = -1;
            Cursor = Cursors.SizeAll;
            SelectTile(new Point(movingTile, 0));
        }

        bool OutsideDragThreshold(Point location)
        {
            var size = SystemInformation.DragSize;
            return Math.Abs(location.X - pressLocation.X) * 2 >= size.Width ||
                Math.Abs(location.Y - pressLocation.Y) * 2 >= size.Height;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right && !pressed && Settings != null && Settings.FreeLayout != null)
            {
                var tile = HitTile(e.Location);
                if (tile.HasValue)
                {
                    int index = tile.Value.X;
                    Settings.FreeLayout.Remove(index);
                    if (SelectedTile.HasValue && SelectedTile.Value.X >= index)
                        SelectTile(SelectedTile.Value.X == index ? (Point?)null : new Point(SelectedTile.Value.X - 1, 0));
                    RefreshLayout();
                }
                return;
            }
            if (e.Button != MouseButtons.Left || !pressed) return;
            if (movingTile >= 0 && dragging && UpdateDropCell(e.Location))
                Settings.FreeLayout.TryMove(movingTile, dropCell.Value);
            if (movingTile < 0 && !dragging && !OutsideDragThreshold(e.Location))
            {
                var tile = HitTile(e.Location);
                if (tile == HitTile(pressLocation))
                    SelectTile(tile == SelectedTile ? null : tile);
            }
            EndPointer();
            Capture = false;
        }

        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) EndPointer();
        }

        void EndPointer()
        {
            pressed = canDrag = dragging = false;
            tileHold.Stop();
            pendingTile = -1;
            if (movingTile >= 0)
            {
                dragScroll.Stop();
                movingTile = -1;
                dropCell = null;
                UpdateCanvasSize();
                Invalidate();
            }
            Cursor = Cursors.Default;
        }

        bool UpdateDropCell(Point location)
        {
            var layout = Settings.FreeLayout;
            int pitch = 150 + Settings.Gap;
            var origin = layout[movingTile].Position;
            var target = new Point(origin.X + (int)Math.Round((location.X - pressLocation.X) / scale / pitch),
                origin.Y + (int)Math.Round((location.Y - pressLocation.Y) / scale / pitch));
            dropCell = layout.CanMove(movingTile, target) ? (Point?)target : null;
            return dropCell.HasValue;
        }

        Rectangle DropBounds()
        {
            int pitch = 150 + Settings.Gap;
            var tile = Settings.FreeLayout[movingTile];
            var bounds = TileBounds(new Point(movingTile, 0));
            bounds.Offset((dropCell.Value.X - tile.Position.X) * pitch, (dropCell.Value.Y - tile.Position.Y) * pitch);
            return bounds;
        }

        void PreviewDrop(Point location)
        {
            Cursor = UpdateDropCell(location) ? Cursors.SizeAll : Cursors.No;
            UpdateCanvasSize();
            Invalidate();
        }

        void ScrollWhileDragging(object sender, EventArgs e)
        {
            var workspace = Parent as ScrollableControl;
            if (workspace == null || !Capture) return;
            var cursor = workspace.PointToClient(MousePosition);
            if (!workspace.ClientRectangle.Contains(cursor)) return;
            int edge = (int)Math.Ceiling(26 * scale);
            int step = (int)Math.Ceiling(15 * scale);
            var current = workspace.AutoScrollPosition;
            int dx = cursor.X < edge ? -step : cursor.X >= workspace.ClientSize.Width - edge ? step : 0;
            int dy = cursor.Y < edge ? -step : cursor.Y >= workspace.ClientSize.Height - edge ? step : 0;
            if (dx == 0 && dy == 0) return;
            workspace.AutoScrollPosition = new Point(Math.Max(0, -current.X + dx), Math.Max(0, -current.Y + dy));
            if (workspace.AutoScrollPosition != current) PreviewDrop(PointToClient(MousePosition));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!pressed || (e.Button & MouseButtons.Left) == 0 || (!dragging && !OutsideDragThreshold(e.Location))) return;
            if (movingTile >= 0)
            {
                dragging = true;
                PreviewDrop(e.Location);
                if (!dragScroll.Enabled) dragScroll.Start();
                return;
            }
            if (pendingTile >= 0)
            {
                tileHold.Stop();
                pendingTile = -1;
            }
            if (!canDrag || Picture == null) return;
            if (!dragging)
            {
                dragging = true;
                Cursor = Cursors.SizeAll;
            }
            var crop = Images.Crop(Picture.Size, Settings);
            float dx = Picture.Width - crop.Width, dy = Picture.Height - crop.Height;
            if (dx > .01) Settings.PanX = Math.Max(0, Math.Min(1, Settings.PanX - (e.X - previous.X) * crop.Width / scale / Settings.Width / dx));
            if (dy > .01) Settings.PanY = Math.Max(0, Math.Min(1, Settings.PanY - (e.Y - previous.Y) * crop.Height / scale / Settings.Height / dy));
            previous = e.Location;
            Invalidate();
            if (Changed != null) Changed();
        }
        public void ZoomBy(float value){Settings.Zoom=Math.Max(1,Math.Min(4,Settings.Zoom+value));Invalidate();if(Changed!=null)Changed();}
        protected override bool IsInputKey(Keys keyData){return keyData==Keys.Add||keyData==Keys.Subtract||keyData==Keys.Oemplus||keyData==Keys.OemMinus||base.IsInputKey(keyData);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Add||e.KeyCode==Keys.Oemplus)ZoomBy(.1f);if(e.KeyCode==Keys.Subtract||e.KeyCode==Keys.OemMinus)ZoomBy(-.1f);}
    }

    public sealed class GenieForm:Form
    {
        sealed class CanvasViewport : Panel
        {
            const int MouseWheelMessage = 0x020A;
            const int ControlKey = 0x0008;
            public GenieCanvas Canvas;

            protected override void WndProc(ref Message message)
            {
                long wheel = message.WParam.ToInt64();
                if (message.Msg == MouseWheelMessage && (wheel & ControlKey) != 0)
                {
                    // Consume the original wheel message before AutoScroll handles it.
                    int delta = unchecked((short)(wheel >> 16));
                    if (delta != 0 && Canvas != null && Canvas.Picture != null && Canvas.Settings.TileCount > 0)
                        Canvas.ZoomBy(delta > 0 ? .1f : -.1f);
                    message.Result = IntPtr.Zero;
                    return;
                }
                base.WndProc(ref message);
            }
        }

        sealed class Photo:IDisposable
        {public Bitmap Image;public FrameCrop Crop;public override string ToString(){return Crop.Name;}public void Dispose(){Image.Dispose();}}
        readonly string root;readonly Project settings=new Project{Rows=2,Columns=2,ColumnWidths=new[]{310,150}};
        readonly List<Photo> photos=new List<Photo>();
        GenieCanvas canvas;Panel workspace,sidebar;ListBox list;TextBox bindingValue;ComboBox interval;Label status;Panel photoActions;ContextMenuStrip menu;
        readonly List<Action> languageBindings = new List<Action>();
        readonly ToolTip bindingTips = new ToolTip();
        readonly Icon applicationIcon;
        Label bindingLabel;
        EditorButton websiteButton, fileButton, clearBindingButton, globalBindingButton;
        bool updatingBinding;
        Panel intervalRow;
        NumericUpDown customMinutes;
        static readonly KeyValuePair<int, string>[] rotationIntervals = {
            new KeyValuePair<int, string>(5, "5 分钟"),
            new KeyValuePair<int, string>(10, "10 分钟"),
            new KeyValuePair<int, string>(15, "15 分钟"),
            new KeyValuePair<int, string>(30, "30 分钟"),
            new KeyValuePair<int, string>(60, "1 小时"),
            new KeyValuePair<int, string>(180, "3 小时"),
            new KeyValuePair<int, string>(360, "6 小时"),
            new KeyValuePair<int, string>(720, "12 小时"),
            new KeyValuePair<int, string>(1440, "24 小时")
        };
        string statusKey = "";
        object[] statusArgs = new object[0];
        Button pin;bool busy,binding;int selected=-1;
        public GenieForm(string library,string initial)
        {
            root = library;
            Text = "TileGenie";
            using (var resource = typeof(GenieForm).Assembly.GetManifestResourceStream("TileMosaic.TileGenie.ico"))
            {
                if (resource != null)
                {
                    applicationIcon = new Icon(resource);
                    Icon = applicationIcon;
                }
            }
            Font = new Font("Segoe UI", 10);
            BackColor = EditorTheme.Surface;
            ForeColor = EditorTheme.Ink;
            ClientSize = new Size(980, 660);
            MinimumSize = new Size(640, 490);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            StartPosition = FormStartPosition.CenterScreen;
            Build();BindEditorControls(this);ApplyLanguage();AllowDrop=true;DragEnter+=delegate(object s,DragEventArgs e){if(e.Data.GetDataPresent(DataFormats.FileDrop))e.Effect=DragDropEffects.Copy;};DragDrop+=delegate(object s,DragEventArgs e){if(!busy)AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));};
            if(!string.IsNullOrEmpty(initial))Shown+=delegate{AddFiles(new[]{initial});};
            FormClosing+=delegate(object s,FormClosingEventArgs e){if(busy){e.Cancel=true;SetStatus("正在固定磁贴，请等待完成后再关闭。");}};
        }
        Label Label(Control parent, string text, int x, int y, int width, int height)
        {
            var label = new Label { Text = text, Bounds = new Rectangle(x, y, width, height),
                ForeColor = EditorTheme.Muted };
            parent.Controls.Add(label);
            return label;
        }

        EditorButton Button(Control parent, string text, int x, int y, int width, EventHandler handler)
        {
            var button = new EditorButton { Text = text, Bounds = new Rectangle(x, y, width, 36),
                Cursor = Cursors.Hand };
            button.Click += handler;
            parent.Controls.Add(button);
            return button;
        }
        void Build()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 76 };
            header.Paint += delegate(object sender, PaintEventArgs e) {
                using (var pen = new Pen(EditorTheme.Line))
                    e.Graphics.DrawLine(pen, 0, header.Height - 1, header.Width, header.Height - 1);
            };
            Controls.Add(header);
            var menuButton = Button(header, "", 24, 20, 38, delegate(object sender, EventArgs e) {
                var button = (Button)sender;
                menu.Show(button, new Point(0, button.Height));
            });
            menuButton.AccessibleName = "菜单";
            menuButton.MenuIcon = true;
            var title = Label(header, "TileGenie", 76, 10, 320, 54);
            title.Font = new Font("Segoe UI Semibold", 27);
            title.ForeColor = EditorTheme.Ink;
            title.TextAlign = ContentAlignment.MiddleLeft;
            status = new Label { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(24, 0, 24, 0),
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = EditorTheme.Ink, BackColor = EditorTheme.Selection,
                Visible = false };
            status.TextChanged += delegate { status.Visible = status.Text.Length > 0; };
            Controls.Add(status);
            var body = new Panel { Dock = DockStyle.Fill };
            Controls.Add(body);
            body.BringToFront();
            sidebar = new Panel { Dock = DockStyle.Left, Width = 260, AutoScroll = true, BackColor = EditorTheme.Surface };
            body.Controls.Add(sidebar);
            Button(sidebar, "添加图片…", 24, 22, 204, delegate { Choose(); });
            var pinButton = Button(sidebar, "固定磁贴", 24, 66, 204, async delegate { await PinNew(); });
            pinButton.Primary = true;
            pin = pinButton;
            pin.Enabled = false;
            Button(sidebar, "帮助", 24, 110, 204, delegate {
                MessageBox.Show(this, UiText.Get("添加图片后，拖动画布中的图片裁剪，按住 Ctrl 并滚动滚轮缩放。")
                    + Environment.NewLine + UiText.Get("行列布局中，列按钮位于画布上方，行按钮位于左侧。点击「大磁贴」可切换整个画布。")
                    + Environment.NewLine + UiText.Get("自由组合中，点击「添加磁贴」选择尺寸。长按磁贴直到光标变为移动图标，再拖到空位。右键单击可删除磁贴。")
                    + Environment.NewLine + UiText.Get("选中图片可单独裁剪、移除或调整顺序。多张图片可设置轮换。")
                    + Environment.NewLine + UiText.Get("磁贴可绑定网址或文件。选中单块可单独更改，全局绑定会覆盖单块设置。"), UiText.Get("操作帮助"));
            });
            bindingLabel = Label(sidebar, "全局绑定（可选）", 24, 168, 204, 22);
            bindingLabel.AutoEllipsis = true;
            bindingValue = new TextBox { Location = new Point(24, 192), Width = 204, BorderStyle = BorderStyle.FixedSingle,
                ForeColor = EditorTheme.Ink, BackColor = EditorTheme.EmptyTile };
            bindingValue.TextChanged += delegate { SaveWebsiteBinding(); };
            sidebar.Controls.Add(bindingValue);
            websiteButton = Button(sidebar, "网址", 24, 222, 52, delegate { EditWebsiteBinding(); });
            fileButton = Button(sidebar, "选择文件…", 82, 222, 94, delegate { ChooseFileBinding(); });
            clearBindingButton = Button(sidebar, "清除", 182, 222, 46, delegate { ApplyBinding(null); });
            globalBindingButton = Button(sidebar, "返回全局绑定", 24, 264, 204, delegate {
                canvas.SelectTile(null);
                bindingValue.Focus();
            });
            globalBindingButton.Visible = false;
            Label(sidebar, "图片轮换", 24, 310, 204, 22);
            BuildIntervalRow();
            var photoPanel = new Panel { Left = 24, Top = 376, Width = 204, Height = 150,
                BackColor = EditorTheme.Line, Padding = new Padding(1) };
            sidebar.Controls.Add(photoPanel);
            Action sizePhotos = delegate {
                int y = photoPanel.Top - sidebar.AutoScrollPosition.Y;
                int margin = photoPanel.Left - sidebar.AutoScrollPosition.X;
                photoPanel.Height = Math.Max(margin * 5, sidebar.Height - y - margin);
            };
            sidebar.SizeChanged += delegate { sizePhotos(); };
            list = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false,
                DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 52, BackColor = EditorTheme.EmptyTile, ForeColor = EditorTheme.Ink,
                AccessibleName = "轮换图片列表" };
            photoPanel.Controls.Add(list);
            list.DrawItem += DrawPhoto;
            list.SelectedIndexChanged+=delegate{SelectPhoto(list.SelectedIndex);};
            photoActions = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = EditorTheme.Surface, Visible = false };
            photoPanel.Controls.Add(photoActions);
            list.BringToFront();
            Button(photoActions, "移除", 5, 5, 88, delegate { RemovePhoto(); });
            Button(photoActions, "↑", 98, 5, 46, delegate { MovePhoto(-1); }).AccessibleName = "上移图片";
            Button(photoActions, "↓", 149, 5, 46, delegate { MovePhoto(1); }).AccessibleName = "下移图片";
            canvas = new GenieCanvas { Settings = settings, Font = Font };
            workspace = new CanvasViewport { Canvas = canvas, Dock = DockStyle.Fill, AutoScroll = true, BackColor = EditorTheme.Workspace };
            body.Controls.Add(workspace);
            workspace.BringToFront();
            workspace.Controls.Add(canvas);
            workspace.MouseClick += delegate(object sender, MouseEventArgs e) {
                if (e.Button == MouseButtons.Left) canvas.SelectTile(null);
            };
            canvas.Changed += SaveCrop;
            canvas.SelectionChanged += RefreshBindingEditor;
            canvas.LayoutChanged += UpdatePinAvailability;
            canvas.RefreshLayout();
            Shown += delegate {
                list.ItemHeight = (int)Math.Round(52 * DeviceDpi / 96f);
                sizePhotos();
                canvas.RefreshLayout();
            };
            BuildMenu();
        }

        void SaveWebsiteBinding()
        {
            if (updatingBinding || canvas == null || bindingValue.ReadOnly) return;
            string value = bindingValue.Text.Trim();
            Uri url;
            canvas.SetBinding(TileBinding.Website(value), Project.TryWebsite(value, out url));
        }

        void EditWebsiteBinding()
        {
            if (bindingValue.ReadOnly)
            {
                updatingBinding = true;
                bindingValue.ReadOnly = false;
                bindingValue.Text = "";
                updatingBinding = false;
                bindingTips.SetToolTip(bindingValue, UiText.Get("输入以 http:// 或 https:// 开头的网址。"));
            }
            bindingValue.Focus();
            bindingValue.SelectAll();
        }

        void ChooseFileBinding()
        {
            using (var dialog = new OpenFileDialog { Title = UiText.Get("选择要打开的文件"),
                Filter = UiText.Get("所有文件") + " (*.*)|*.*",
                CheckFileExists = true, RestoreDirectory = true, DereferenceLinks = false })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path;
                if (!TileBinding.TryFilePath(dialog.FileName, out path) || !File.Exists(path))
                {
                    MessageBox.Show(this, UiText.Get("请选择有效的文件。"), "TileGenie");
                    return;
                }
                ApplyBinding(new TileBinding(TileBindingKind.File, path));
            }
        }

        void ApplyBinding(TileBinding binding)
        {
            canvas.SetBinding(binding, true);
            RefreshBindingEditor();
        }

        void RefreshBindingEditor()
        {
            var tile = canvas.SelectedTile;
            bindingLabel.Text = tile.HasValue
                ? settings.FreeLayout == null
                    ? UiText.Format("绑定 · 第 {0} 行第 {1} 列", tile.Value.Y + 1, tile.Value.X + 1)
                    : UiText.Format("绑定 · 第 {0} 块磁贴", tile.Value.X + 1)
                : UiText.Get("全局绑定（可选）");
            bindingValue.AccessibleName = bindingLabel.Text;
            var binding = settings.BindingAt(tile);
            bool isFile = binding != null && binding.Kind == TileBindingKind.File;
            globalBindingButton.Visible = tile.HasValue;
            bindingTips.SetToolTip(websiteButton, UiText.Get("编辑网址绑定"));
            bindingTips.SetToolTip(fileButton, UiText.Get(tile.HasValue
                ? "为这块磁贴选择文件。" : "为全部磁贴选择文件，并覆盖单块绑定。"));
            bindingTips.SetToolTip(clearBindingButton, UiText.Get(tile.HasValue
                ? "清除单块绑定并沿用全局设置。" : "清除全部磁贴的绑定。"));
            bindingTips.SetToolTip(bindingValue, isFile ? binding.Target : UiText.Get("输入以 http:// 或 https:// 开头的网址。"));
            updatingBinding = true;
            try
            {
                bindingValue.ReadOnly = isFile;
                bindingValue.Text = binding == null ? "" : binding.Target;
            }
            finally { updatingBinding = false; }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!busy && keyData == Keys.Escape && canvas != null && canvas.SelectedTile.HasValue)
            {
                canvas.SelectTile(null);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        void BuildIntervalRow()
        {
            intervalRow = new Panel { Bounds = new Rectangle(24, 334, 204, 29), Enabled = false };
            interval = new ComboBox { Dock = DockStyle.Top, FlatStyle = FlatStyle.Flat,
                ForeColor = EditorTheme.Ink, BackColor = EditorTheme.EmptyTile,
                DropDownStyle = ComboBoxStyle.DropDownList, AccessibleName = "图片轮换间隔" };
            // The right panel reserves space only while the custom option is selected.
            var custom = new Panel { Dock = DockStyle.Right, Width = 112, Visible = false };
            customMinutes = new NumericUpDown {
                Bounds = new Rectangle(8, 0, 65, 26),
                Minimum = Project.MinimumCycleMinutes, Maximum = Project.MaximumCycleMinutes,
                Value = settings.CycleMinutes, AccessibleName = "自定义轮换间隔（分钟）",
                ForeColor = EditorTheme.Ink, BackColor = EditorTheme.EmptyTile
            };
            custom.Controls.Add(customMinutes);
            Label(custom, "分钟", 75, 3, 36, 24);
            intervalRow.Controls.Add(interval);
            intervalRow.Controls.Add(custom);
            interval.BringToFront();
            intervalRow.SizeChanged += delegate { interval.DropDownWidth = intervalRow.ClientSize.Width; };
            interval.DropDownWidth = intervalRow.ClientSize.Width;

            interval.Items.AddRange(rotationIntervals.Select(option => UiText.Get(option.Value)).ToArray());
            interval.Items.Add(UiText.Get("自定义"));
            interval.SelectedIndexChanged += delegate { custom.Visible = interval.SelectedIndex == rotationIntervals.Length; };
            interval.SelectedIndex = Array.FindIndex(rotationIntervals, option => option.Key == settings.CycleMinutes);
            sidebar.Controls.Add(intervalRow);
        }

        int SelectedCycleMinutes
        {
            get { return interval.SelectedIndex == rotationIntervals.Length
                ? (int)Math.Round(customMinutes.Value, MidpointRounding.AwayFromZero)
                : rotationIntervals[interval.SelectedIndex].Key; }
        }

        void DrawPhoto(object sender, DrawItemEventArgs e)
        {
            bool selectedRow = (e.State & DrawItemState.Selected) != 0;
            var background = selectedRow ? EditorTheme.Selection : EditorTheme.EmptyTile;
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (e.Index < 0 || e.Index >= photos.Count) return;
            var photo = photos[e.Index];
            int inset = Math.Max(4, e.Bounds.Height / 7);
            int h = e.Bounds.Height - inset * 2;
            int w = h * 3 / 2;
            var imageBounds = new Rectangle(e.Bounds.X + inset, e.Bounds.Y + inset, w, h);
            using (var thumb = Images.Render(photo.Image, new RectangleF(0, 0, photo.Image.Width, photo.Image.Height), w, h, true))
                e.Graphics.DrawImage(thumb, imageBounds);
            var textBounds = Rectangle.FromLTRB(imageBounds.Right + inset, e.Bounds.Top, e.Bounds.Right - inset, e.Bounds.Bottom);
            TextRenderer.DrawText(e.Graphics, photo.Crop.Name, Font, textBounds, EditorTheme.Ink,
                TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            if ((e.State & DrawItemState.Focus) != 0)
                ControlPaint.DrawFocusRectangle(e.Graphics, e.Bounds, EditorTheme.Accent, background);
        }

        void BuildMenu()
        {
            menu = new ContextMenuStrip { Font = Font };
            menu.Items.Add("画布选项…", null, delegate { ShowOptions(); });
            menu.Items.Add(new ToolStripSeparator());
            var zoomIn = menu.Items.Add("放大图片", null, delegate { canvas.ZoomBy(.1f); });
            var zoomOut = menu.Items.Add("缩小图片", null, delegate { canvas.ZoomBy(-.1f); });
            var reset = menu.Items.Add("复位裁剪", null, delegate { ResetCrop(); });
            var rotate = menu.Items.Add("顺时针旋转", null, delegate {
                photos[selected].Image.RotateFlip(RotateFlipType.Rotate90FlipNone);
                ResetCrop();
                list.Invalidate();
            });
            foreach (ToolStripItem item in menu.Items)
            {
                var label = item.Text;
                if (!(item is ToolStripSeparator))
                    languageBindings.Add(delegate { item.Text = UiText.Get(label); });
            }
            menu.Items.Add(new ToolStripSeparator());
            var language = new ToolStripMenuItem("语言 / Language");
            var chinese = language.DropDownItems.Add("简体中文", null, delegate { SelectLanguage(false); }) as ToolStripMenuItem;
            var english = language.DropDownItems.Add("English", null, delegate { SelectLanguage(true); }) as ToolStripMenuItem;
            menu.Items.Add(language);
            languageBindings.Add(delegate { chinese.Checked = !UiText.English; english.Checked = UiText.English; });
            menu.Opening += delegate(object sender, System.ComponentModel.CancelEventArgs e) {
                e.Cancel = busy;
                rotate.Enabled = reset.Enabled = zoomIn.Enabled = zoomOut.Enabled = selected >= 0;
            };
        }

        void BindEditorControls(Control parent)
        {
            foreach (Control control in parent.Controls)
            {
                if (control == canvas) continue;
                string label = control.Text;
                if (control != status && (control is Button || control is Label))
                    languageBindings.Add(delegate { control.Text = UiText.Get(label); });
                string accessible = control.AccessibleName;
                if (!string.IsNullOrEmpty(accessible))
                    languageBindings.Add(delegate { control.AccessibleName = UiText.Get(accessible); });
                BindEditorControls(control);
            }
        }

        void ApplyLanguage()
        {
            foreach (var update in languageBindings) update();
            int index = interval.SelectedIndex;
            interval.BeginUpdate();
            try
            {
                for (int i = 0; i < rotationIntervals.Length; i++)
                    interval.Items[i] = UiText.Get(rotationIntervals[i].Value);
                interval.Items[rotationIntervals.Length] = UiText.Get("自定义");
                interval.SelectedIndex = index;
            }
            finally { interval.EndUpdate(); }
            canvas.RefreshLayout();
            RefreshBindingEditor();
            status.Text = UiText.Format(statusKey, statusArgs);
        }

        void SelectLanguage(bool english)
        {
            if (UiText.English == english) return;
            try { UiText.Select(english); }
            catch (IOException ex) { MessageBox.Show(this, ex.Message, UiText.Get("无法保存语言偏好。")); }
            catch (UnauthorizedAccessException ex) { MessageBox.Show(this, ex.Message, UiText.Get("无法保存语言偏好。")); }
            ApplyLanguage();
        }

        void SetStatus(string key, params object[] args)
        {
            statusKey = key;
            statusArgs = args;
            status.Text = UiText.Format(key, args);
        }

        void ResetCrop()
        {
            settings.Zoom = 1;
            settings.PanX = settings.PanY = .5f;
            SaveCrop();
            canvas.Invalidate();
        }

        void ShowOptions()
        {
            using (var dialog = new Form { Text = UiText.Get("画布选项"), Font = Font, ClientSize = new Size(280, 182),
                StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false, MinimizeBox = false })
            {
                var numbers = new CheckBox { Text = UiText.Get("预览行列编号"), Checked = canvas.Numbers, Bounds = new Rectangle(18, 14, 210, 26) };
                var names = new CheckBox { Text = UiText.Get("在磁贴上显示标题"), Checked = settings.ShowNames, Bounds = new Rectangle(18, 45, 235, 26) };
                var label = new Label { Text = UiText.Get("间距补偿"), Bounds = new Rectangle(18, 87, 100, 28) };
                var gap = new NumericUpDown { Minimum = 0, Maximum = 24, Value = settings.Gap, Bounds = new Rectangle(130, 82, 76, 28) };
                var ok = new Button { Text = UiText.Get("确定"), DialogResult = DialogResult.OK, Bounds = new Rectangle(174, 133, 86, 32) };
                dialog.Controls.AddRange(new Control[] { numbers, names, label, gap, ok });
                dialog.AcceptButton = ok;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                canvas.Numbers = numbers.Checked;
                settings.ShowNames = names.Checked;
                settings.Gap = (int)gap.Value;
                canvas.RefreshLayout();
            }
        }

        void SaveCrop()
        {
            if (selected < 0 || selected >= photos.Count) return;
            var frame = photos[selected].Crop;
            frame.Zoom = settings.Zoom;
            frame.PanX = settings.PanX;
            frame.PanY = settings.PanY;
        }
        void SelectPhoto(int index)
        {
            if(binding)return;SaveCrop();selected=index;if(index>=0&&index<photos.Count){var f=photos[index].Crop;settings.Zoom=f.Zoom;settings.PanX=f.PanX;settings.PanY=f.PanY;canvas.Picture=photos[index].Image;}else canvas.Picture=null;
            canvas.Invalidate();UpdatePinAvailability();intervalRow.Enabled=photos.Count>1;photoActions.Visible=index>=0;
        }
        void UpdatePinAvailability() { pin.Enabled = !busy && photos.Count > 0 && settings.TileCount > 0; }
        void Choose(){if(busy)return;using(var d=new OpenFileDialog{Multiselect=true,Filter=UiText.Get("图片")+"|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"})if(d.ShowDialog(this)==DialogResult.OK)AddFiles(d.FileNames);}
        void AddFiles(string[] files)
        {
            if(files==null)return;int before=photos.Count;var failures=new List<string>();
            foreach(string path in files){if(photos.Count>=20){failures.Add(UiText.Get("最多支持 20 张图片。"));break;}try{var photo=new Photo{Image=Images.Load(path),Crop=new FrameCrop{Name=Path.GetFileNameWithoutExtension(path)}};photos.Add(photo);list.Items.Add(photo);}catch(Exception ex){failures.Add(Path.GetFileName(path)+"："+ex.Message);}}
            if(photos.Count>before)list.SelectedIndex=before;
            SetStatus("");if(failures.Count>0)MessageBox.Show(this,string.Join("\r\n",failures),UiText.Get("部分图片未能打开"));
        }
        void RemovePhoto(){if(busy||selected<0)return;int index=selected;canvas.Picture=null;binding=true;var photo=photos[index];photos.RemoveAt(index);list.Items.RemoveAt(index);selected=-1;binding=false;photo.Dispose();if(photos.Count>0)list.SelectedIndex=Math.Min(index,photos.Count-1);else SelectPhoto(-1);}
        void MovePhoto(int delta){if(busy||selected<0||selected+delta<0||selected+delta>=photos.Count)return;SaveCrop();int target=selected+delta;var photo=photos[selected];binding=true;photos.RemoveAt(selected);photos.Insert(target,photo);list.Items.Clear();list.Items.AddRange(photos.Cast<object>().ToArray());selected=-1;binding=false;list.SelectedIndex=target;}
        async Task PinNew()
        {
            if (busy || photos.Count == 0 || settings.TileCount == 0) return;
            SaveCrop();
            settings.Name = UiText.Get("拼图") + " " + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
            settings.CycleMinutes = SelectedCycleMinutes;
            settings.Frames = photos.Select(p => p.Crop).ToList();
            var snapshot=settings.Copy();snapshot.Id=Guid.NewGuid().ToString("N");try{snapshot.Validate();}catch(Exception ex){MessageBox.Show(this,ex.Message,UiText.Get("检查输入"));return;}
            busy=true;pin.Enabled=false;sidebar.Enabled=false;workspace.Enabled=false;
            X509Certificate2 signer = null;
            try
            {
                SetStatus("正在准备本机签名证书…");
                signer = await LocalSigning.GetOrCreate();
                if (!LocalSigning.IsTrusted(signer))
                {
                    var choice = MessageBox.Show(this, UiText.Format(
                        "首次固定需要信任本机生成的磁贴签名证书。Windows 会请求管理员授权，将证书加入本机的“受信任的人”。\n\n指纹：{0}\n\n是否继续？",
                        signer.Thumbprint), "TileGenie", MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information, MessageBoxDefaultButton.Button2);
                    if (choice != DialogResult.OK || !await LocalSigning.Trust(signer))
                    {
                        SetStatus("");
                        return;
                    }
                }

                SetStatus("正在裁剪图片并准备磁贴…");
                Project project;
                var copies = new List<Bitmap>();
                try
                {
                    foreach (var photo in photos) copies.Add((Bitmap)photo.Image.Clone());
                    project = await Task.Run(() => PackageBuilder.Generate(snapshot, copies, root));
                }
                finally { foreach (var copy in copies) copy.Dispose(); }
                SetStatus("正在安装图片资源…");
                string family = await ShellIntegration.Install(project, signer);
                SetStatus("正在固定磁贴…");
                var result = StartPinning.Run(project, family, true);
                int count = result.Added + result.Present;
                if (project.Frames.Count == 1)
                {
                    if (result.FreeLayoutGroups > 1)
                        SetStatus("已固定 {0} 块磁贴，分为 {1} 组。可在开始菜单调整各组位置。", count, result.FreeLayoutGroups);
                    else
                        SetStatus(project.FreeLayout == null ? "已固定 {0} 块磁贴。到开始菜单按行列排列即可。" : "已按自由组合布局固定 {0} 块磁贴。", count);
                    return;
                }

                SetStatus("磁贴已固定，正在安排图片轮换…");
                try
                {
                    bool renews = await ShellIntegration.InitializeRotation(family);
                    if (renews && result.FreeLayoutGroups > 1)
                        SetStatus("已固定 {0} 块磁贴（{1} 组），每 {2} 分钟轮换图片。", count, result.FreeLayoutGroups, project.CycleMinutes);
                    else
                        SetStatus(renews ? "已固定 {0} 块磁贴，每 {1} 分钟轮换图片。"
                            : "磁贴已固定，已安排未来 96 次轮换。点击磁贴可续排。", count, project.CycleMinutes);
                }
                catch (Exception ex)
                {
                    const string message = "磁贴已固定。点击任一磁贴可重试初始化图片轮换。";
                    SetStatus(message);
                    LogError(message, ex);
                }
            }
            catch(Exception ex){ReportPinFailure(ex);}
            finally
            {
                if (signer != null) signer.Dispose();
                busy=false;sidebar.Enabled=true;workspace.Enabled=true;UpdatePinAvailability();
            }
        }
        void ReportPinFailure(Exception ex)
        {
            const string text = "固定未完成。请根据提示处理后再试。";
            SetStatus(text);
            string message = UiText.Get(text) + "\r\n\r\n" + ex.Message;
            MessageBox.Show(this, message, "TileGenie", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            LogError(text, ex);
        }
        void LogError(string text, Exception ex)
        {
            try{Directory.CreateDirectory(root);File.AppendAllText(Path.Combine(root,"errors.log"),DateTime.Now.ToString("s")+" "+text+Environment.NewLine+ex+Environment.NewLine);}catch{}
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                bindingTips.Dispose();
                if (menu != null) menu.Dispose();
                foreach (var photo in photos) photo.Dispose();
            }
            base.Dispose(disposing);
            if (disposing && applicationIcon != null) applicationIcon.Dispose();
        }
    }
}
