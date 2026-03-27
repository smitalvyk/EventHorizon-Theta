using System.Collections.Generic;
using UnityEngine;
using GameDatabase.Enums;
using Constructor.Model;

namespace ShipEditor
{
    public class ShipMeshBuilder
    {
        public interface ILayout
        {
            ref readonly LayoutRect Rect { get; }
            CellType this[int x, int y] { get; }
            string GetWeaponClasses(int x, int y);
        }

        public struct CustomCellInfo
        {
            public Color Color;
            public Rect UVRect;
            public bool MergeCells;
        }

        private readonly Dictionary<int, int> _cache = new();
        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _triangles = new();
        private readonly float _cellSize;

        public Dictionary<int, CustomCellInfo> CustomCells { get; set; } = new Dictionary<int, CustomCellInfo>();

        public Color OuterCellColor { get; set; } = Color.white;
        public Color InnerCellColor { get; set; } = Color.white;
        public Color EngineCellColor { get; set; } = Color.white;
        public Color WeaponCellColor { get; set; } = Color.white;

        public Rect DefaultUVRect = new Rect(0f, 0f, 1f, 1f);

        public ShipMeshBuilder(float cellSize)
        {
            _cellSize = cellSize;
        }

        public void Build(ILayout layout)
        {
            int xMin = layout.Rect.xMin;
            int xMax = layout.Rect.xMax;
            int yMin = layout.Rect.yMin;
            int yMax = layout.Rect.yMax;

            int width = xMax - xMin + 1;
            int height = yMax - yMin + 1;

            if (width <= 0 || height <= 0) return;

            bool[,] visited = new bool[width, height];

            for (int i = yMin; i <= yMax; ++i)
            {
                for (int j = xMin; j <= xMax; ++j)
                {
                    int localX = j - xMin;
                    int localY = i - yMin;

                    if (visited[localX, localY]) continue;

                    var cellType = layout[j, i];
                    int cellId = (int)cellType;

                    if (CustomCells.TryGetValue(cellId, out var customInfo))
                    {
                        if (customInfo.MergeCells)
                        {
                            int maxSize = 1;
                            while (localX + maxSize < width && localY + maxSize < height)
                            {
                                bool canExpand = true;
                                for (int dy = 0; dy <= maxSize; dy++)
                                {
                                    if (visited[localX + maxSize, localY + dy] || (int)layout[j + maxSize, i + dy] != cellId)
                                    {
                                        canExpand = false; break;
                                    }
                                }
                                if (!canExpand) break;

                                for (int dx = 0; dx <= maxSize; dx++)
                                {
                                    if (visited[localX + dx, localY + maxSize] || (int)layout[j + dx, i + maxSize] != cellId)
                                    {
                                        canExpand = false; break;
                                    }
                                }
                                if (!canExpand) break;

                                maxSize++;
                            }

                            for (int dy = 0; dy < maxSize; dy++)
                                for (int dx = 0; dx < maxSize; dx++)
                                    visited[localX + dx, localY + dy] = true;

                            AddCustomColorCellMerged(localX, localY, maxSize, customInfo.Color, customInfo.UVRect);
                        }
                        else
                        {
                            visited[localX, localY] = true;
                            AddCustomColorCellMerged(localX, localY, 1, customInfo.Color, customInfo.UVRect);
                        }
                    }
                    else if (IsValidCell(cellType))
                    {
                        visited[localX, localY] = true;
                        if (cellType == CellType.InnerOuter)
                            AddMixedCell(localX, localY, CellType.Outer, CellType.Inner);
                        else
                            AddSolidCell(localX, localY, cellType);
                    }
                    else
                    {
                        visited[localX, localY] = true;
                    }
                }
            }
        }

        private void AddCustomColorCellMerged(int x, int y, int size, Color32 color, Rect uvRect)
        {
            var v1 = GetCustomVertexEx(x, y, color, new Vector2(uvRect.xMin, uvRect.yMax));
            var v2 = GetCustomVertexEx(x + size, y, color, new Vector2(uvRect.xMax, uvRect.yMax));
            var v3 = GetCustomVertexEx(x + size, y + size, color, new Vector2(uvRect.xMax, uvRect.yMin));
            var v4 = GetCustomVertexEx(x, y + size, color, new Vector2(uvRect.xMin, uvRect.yMin));

            _triangles.Add(v1); _triangles.Add(v2); _triangles.Add(v3);
            _triangles.Add(v3); _triangles.Add(v4); _triangles.Add(v1);
        }

        private int GetCustomVertexEx(int x, int y, Color32 color, Vector2 uv)
        {
            var id = _vertices.Count;
            _vertices.Add(new Vector3(x * _cellSize, -y * _cellSize, 0));
            _uv.Add(uv);
            _colors.Add(color);
            return id;
        }

        private void AddSolidCell(int x, int y, CellType cellType)
        {
            var v1 = GetVertex(x, y, cellType);
            var v2 = GetVertex(x + 1, y, cellType);
            var v3 = GetVertex(x + 1, y + 1, cellType);
            var v4 = GetVertex(x, y + 1, cellType);

            _triangles.Add(v1); _triangles.Add(v2); _triangles.Add(v3);
            _triangles.Add(v3); _triangles.Add(v4); _triangles.Add(v1);
        }

        private void AddMixedCell(int x, int y, CellType cellType1, CellType cellType2)
        {
            var v0 = GetVertex(x, y + 1, cellType1);
            var v1 = GetVertex(x, y, cellType1);
            var v2 = GetVertex(x + 1, y, cellType1);
            var v3 = GetVertex(x + 1, y, cellType2);
            var v4 = GetVertex(x + 1, y + 1, cellType2);
            var v5 = GetVertex(x, y + 1, cellType2);

            _triangles.Add(v0); _triangles.Add(v1); _triangles.Add(v2);
            _triangles.Add(v3); _triangles.Add(v4); _triangles.Add(v5);
        }

        public Mesh CreateMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = _vertices.ToArray();
            mesh.triangles = _triangles.ToArray();
            mesh.uv = _uv.ToArray();
            mesh.colors32 = _colors.ToArray();
            mesh.Optimize();
            return mesh;
        }

        private static int Key(int x, int y, CellType cell)
        {
            const int minValue = -15;
            const int coordinateBits = 12;
            const int coordinateMask = (1 << coordinateBits) - 1;
            x = (x - minValue) & coordinateMask;
            y = (y - minValue) & coordinateMask;
            return (((((int)cell) << coordinateBits) + y) << coordinateBits) + x;
        }

        private bool IsValidCell(CellType cell)
        {
            switch (cell)
            {
                case CellType.Outer:
                case CellType.Inner:
                case CellType.InnerOuter:
                case CellType.Engine:
                case CellType.Weapon: return true;
                default: return false;
            }
        }

        private Color32 CellToColor(CellType cell)
        {
            switch (cell)
            {
                case CellType.Outer: return OuterCellColor;
                case CellType.Inner: return InnerCellColor;
                case CellType.InnerOuter: return InnerCellColor;
                case CellType.Engine: return EngineCellColor;
                case CellType.Weapon: return WeaponCellColor;
                default: return new Color32();
            }
        }

        private int GetVertex(int x, int y, CellType cell)
        {
            var key = Key(x, y, cell);
            if (!_cache.TryGetValue(key, out var id))
            {
                id = _vertices.Count;
                _vertices.Add(new Vector3(x * _cellSize, -y * _cellSize, 0));

                float u = x % 2 == 0 ? DefaultUVRect.xMin : DefaultUVRect.xMax;
                float v = y % 2 == 0 ? DefaultUVRect.yMin : DefaultUVRect.yMax;

                _uv.Add(new Vector2(u, v));
                _colors.Add(CellToColor(cell));
                _cache.Add(key, id);
            }
            return id;
        }
    }
}