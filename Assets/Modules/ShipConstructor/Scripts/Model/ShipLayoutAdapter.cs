using UnityEngine;
using GameDatabase.Enums;
using GameDatabase.Model;

namespace Constructor.Model
{
    public interface IShipLayout
    {
        int CellCount { get; }
        ref readonly LayoutRect Rect { get; }
        CellType this[int x, int y] { get; }

        [System.Obsolete] int Size { get; }
    }

    public readonly struct LayoutRect
    {
        public LayoutRect(int xmin, int ymin, int xmax, int ymax)
        {
            // SAFETY CHECK: Log exact coordinates if rect creation fails
            if (xmax < xmin || ymax < ymin)
            {
                Debug.LogError($"[LayoutRect] Creation error: Invalid coordinates! Min({xmin}, {ymin}) cannot be greater than Max({xmax}, {ymax}).");
                throw new System.InvalidOperationException($"Invalid LayoutRect: Min({xmin}, {ymin}), Max({xmax}, {ymax})");
            }

            xMin = xmin;
            yMin = ymin;
            xMax = xmax;
            yMax = ymax;
        }

        public readonly int xMin;
        public readonly int xMax;
        public readonly int yMin;
        public readonly int yMax;

        public int Width => xMax - xMin + 1;
        public int Height => yMax - yMin + 1;
        public int Square => Width * Height;

        public bool IsInsideRect(int x, int y) => x >= xMin && y >= yMin && x <= xMax && y <= yMax;
    }

    public static class ShipLayoutExtensison
    {
        public static int ToArrayIndex(this in LayoutRect rect, int x, int y)
        {
            return x - rect.xMin + (y - rect.yMin) * rect.Width;
        }

        public static void ArrayIndexToXY(this in LayoutRect rect, int index, out int x, out int y)
        {
            x = rect.xMin + index % rect.Width;
            y = rect.yMin + index / rect.Width;
        }
    }

    public class ShipLayoutAdapter : IShipLayout
    {
        private readonly Layout _layout;
        private readonly LayoutRect _rect;
        private readonly string _debugName; // Used for detailed logging

        public CellType this[int x, int y]
        {
            get
            {
                // SAFETY CHECK: Prevent OutOfRange exceptions and log the exact ship name and coordinates
                if (!_rect.IsInsideRect(x, y))
                {
                    Debug.LogWarning($"[ShipLayoutAdapter | Build: {_debugName}] Out of bounds access attempt at X:{x}, Y:{y}. Valid bounds: ({_rect.xMin}, {_rect.yMin}) to ({_rect.xMax}, {_rect.yMax}). Returned CellType.Empty.");
                    return CellType.Empty;
                }
                return (CellType)_layout[x, y];
            }
        }

        public int CellCount => _layout.CellCount;
        public int Size => _layout.Size;

        public ref readonly LayoutRect Rect => ref _rect;

        public ShipLayoutAdapter(Layout layout, string debugName = "Unknown")
        {
            _layout = layout;
            _debugName = debugName;
            _rect = new LayoutRect(0, 0, Size - 1, Size - 1);
        }
    }
}