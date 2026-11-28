using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace TileMosaic
{
    public enum TileShape { Medium, Wide, Large }

    public sealed class FreeTileLayout
    {
        internal const int MaxTiles = 48, MaxColumns = MaxTiles * 2 + 1, MaxRows = MaxTiles * 3 + 1;
        readonly List<Tile> tiles = new List<Tile>();
        bool fitToTiles = true;

        public struct Tile
        {
            public Point Position;
            public TileShape Shape;
            public TileBinding Binding;
            public Rectangle Bounds { get { return new Rectangle(Position, Cells(Shape)); } }
        }

        public FreeTileLayout() : this(MaxColumns) { }
        public FreeTileLayout(int columns) { Columns = columns; }
        public int Columns { get; private set; }
        public int Count { get { return tiles.Count; } }
        public int ColumnsUsed { get { return tiles.Count == 0 ? 0 : tiles.Max(tile => tile.Bounds.Right); } }
        public int RowsUsed { get { return tiles.Count == 0 ? 0 : tiles.Max(tile => tile.Bounds.Bottom); } }
        internal int CropColumns { get { return fitToTiles ? ColumnsUsed : Columns; } }
        public Tile this[int index] { get { return tiles[index]; } }

        internal static Size Cells(TileShape shape)
        {
            return new Size(shape == TileShape.Medium ? 1 : 2, shape == TileShape.Large ? 2 : 1);
        }

        public FreeTileLayout Copy()
        {
            var result = new FreeTileLayout(Columns) { fitToTiles = fitToTiles };
            result.tiles.AddRange(tiles);
            return result;
        }

        Point FindSpace(Size size, List<Rectangle> occupied)
        {
            // New tiles fill the existing canvas before starting another row. Dragging
            // them beyond that area can still extend the canvas in either dimension.
            int rowWidth = Math.Min(Columns, Math.Max(4, ColumnsUsed));
            for (int row = 0; row <= MaxRows - size.Height; row++)
                for (int column = 0; column <= rowWidth - size.Width; column++)
                {
                    var bounds = new Rectangle(column, row, size.Width, size.Height);
                    if (!occupied.Any(bounds.IntersectsWith)) return bounds.Location;
                }
            throw new InvalidOperationException(UiText.Get("画布没有足够的空间。"));
        }

        public void Add(TileShape shape)
        {
            var position = FindSpace(Cells(shape), tiles.Select(tile => tile.Bounds).ToList());
            tiles.Add(new Tile { Position = position, Shape = shape });
        }

        public void Remove(int index) { tiles.RemoveAt(index); }

        public void SetBinding(int index, TileBinding binding)
        {
            var tile = tiles[index];
            tile.Binding = binding;
            tiles[index] = tile;
        }

        internal void ClearBindings()
        {
            for (int i = 0; i < Count; i++) SetBinding(i, null);
        }

        public bool TryMove(int index, Point position)
        {
            if (!CanMove(index, position)) return false;
            var tile = tiles[index];
            tile.Position = position;
            tiles[index] = tile;
            return true;
        }

        internal bool CanMove(int index, Point position)
        {
            var bounds = new Rectangle(position, Cells(tiles[index].Shape));
            if (bounds.Left < 0 || bounds.Left > Columns - bounds.Width ||
                bounds.Top < 0 || bounds.Top > MaxRows - bounds.Height) return false;
            for (int i = 0; i < Count; i++)
                if (i != index && bounds.IntersectsWith(tiles[i].Bounds)) return false;
            return true;
        }

        internal void Validate()
        {
            if (Columns < 2 || Columns > MaxColumns || Count < 1 || Count > MaxTiles)
                throw new InvalidDataException(UiText.Get("自由组合布局无效。"));
            var occupied = new List<Rectangle>();
            foreach (var tile in tiles)
            {
                var bounds = tile.Bounds;
                if (!Enum.IsDefined(typeof(TileShape), tile.Shape) || bounds.Left < 0 || bounds.Left > Columns - bounds.Width ||
                    bounds.Top < 0 || bounds.Top > MaxRows - bounds.Height || occupied.Any(bounds.IntersectsWith))
                    throw new InvalidDataException(UiText.Get("自由组合布局无效。"));
                occupied.Add(bounds);
            }
        }

        internal XElement Xml()
        {
            return new XElement("FreeLayout", new XAttribute("Columns", Columns), new XAttribute("FitToTiles", fitToTiles),
                tiles.Select(tile =>
                new XElement("Tile", new XAttribute("X", tile.Position.X), new XAttribute("Y", tile.Position.Y),
                    new XAttribute("Shape", tile.Shape), tile.Binding == null ? null : tile.Binding.Xml("Binding"))));
        }

        internal static FreeTileLayout Load(XElement xml)
        {
            // Older projects used the full group width to crop the source.
            var layout = new FreeTileLayout((int)xml.Attribute("Columns")) {
                fitToTiles = (bool?)xml.Attribute("FitToTiles") ?? false
            };
            foreach (var tile in xml.Elements("Tile"))
                layout.tiles.Add(new Tile { Position = new Point((int)tile.Attribute("X"), (int)tile.Attribute("Y")),
                    Shape = (TileShape)Enum.Parse(typeof(TileShape), (string)tile.Attribute("Shape")),
                    Binding = TileBinding.Load(tile.Element("Binding") ?? tile) });
            return layout;
        }
    }
}
