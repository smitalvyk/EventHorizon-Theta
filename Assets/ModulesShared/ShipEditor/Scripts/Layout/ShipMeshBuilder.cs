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
            public Color Color1;
            public Color Color2;
            public Color Color3;
            public Color Color4;
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

                            AddCustomShapeCell(localX, localY, maxSize, customInfo);
                        }
                        else
                        {
                            visited[localX, localY] = true;
                            AddCustomShapeCell(localX, localY, 1, customInfo);
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

        // Draw shape based on the amount of valid colors provided
        private void AddCustomShapeCell(int x, int y, int size, CustomCellInfo info)
        {
            float xMin = x * _cellSize;
            float xMax = (x + size) * _cellSize;
            float yMin = -y * _cellSize;
            float yMax = -(y + size) * _cellSize;
            float xMid = (xMin + xMax) / 2f;
            float yMid = (yMin + yMax) / 2f;

            float uMin = info.UVRect.xMin;
            float uMax = info.UVRect.xMax;
            float vMin = info.UVRect.yMin;
            float vMax = info.UVRect.yMax;
            float uMid = (uMin + uMax) / 2f;
            float vMid = (vMin + vMax) / 2f;

            Color c1 = info.Color1;
            Color c2 = info.Color2;
            Color c3 = info.Color3;
            Color c4 = info.Color4;

            // Ignore default transparent/black database colors
            bool hasC2 = (c2.r > 0f || c2.g > 0f || c2.b > 0f) && c2.a > 0f;
            bool hasC3 = (c3.r > 0f || c3.g > 0f || c3.b > 0f) && c3.a > 0f;
            bool hasC4 = (c4.r > 0f || c4.g > 0f || c4.b > 0f) && c4.a > 0f;

            int colorCount = 1;
            if (hasC2) colorCount = 2;
            if (hasC2 && hasC3) colorCount = 3;
            if (hasC2 && hasC3 && hasC4) colorCount = 4;

            if (colorCount == 1)
            {
                // 1 Color: Solid square
                var vTL = GetCustomVertexRaw(xMin, yMin, c1, new Vector2(uMin, vMax));
                var vTR = GetCustomVertexRaw(xMax, yMin, c1, new Vector2(uMax, vMax));
                var vBR = GetCustomVertexRaw(xMax, yMax, c1, new Vector2(uMax, vMin));
                var vBL = GetCustomVertexRaw(xMin, yMax, c1, new Vector2(uMin, vMin));

                _triangles.Add(vTL); _triangles.Add(vTR); _triangles.Add(vBR);
                _triangles.Add(vBR); _triangles.Add(vBL); _triangles.Add(vTL);
            }
            else if (colorCount == 2)
            {
                // 2 Colors: Diagonal split (Bottom-Left to Top-Right)

                // Top-Left triangle
                var t1_vTL = GetCustomVertexRaw(xMin, yMin, c1, new Vector2(uMin, vMax));
                var t1_vTR = GetCustomVertexRaw(xMax, yMin, c1, new Vector2(uMax, vMax));
                var t1_vBL = GetCustomVertexRaw(xMin, yMax, c1, new Vector2(uMin, vMin));
                _triangles.Add(t1_vTL); _triangles.Add(t1_vTR); _triangles.Add(t1_vBL);

                // Bottom-Right triangle
                var t2_vTR = GetCustomVertexRaw(xMax, yMin, c2, new Vector2(uMax, vMax));
                var t2_vBR = GetCustomVertexRaw(xMax, yMax, c2, new Vector2(uMax, vMin));
                var t2_vBL = GetCustomVertexRaw(xMin, yMax, c2, new Vector2(uMin, vMin));
                _triangles.Add(t2_vTR); _triangles.Add(t2_vBR); _triangles.Add(t2_vBL);
            }
            else
            {
                // 3 or 4 Colors: Envelope (4 triangles from center)
                Color topC = c1;
                Color rightC = c2;
                Color bottomC = c3;
                Color leftC = colorCount == 4 ? c4 : c1; // Duplicate top color if only 3 are provided

                // Top sector
                var t1_v1 = GetCustomVertexRaw(xMin, yMin, topC, new Vector2(uMin, vMax));
                var t1_v2 = GetCustomVertexRaw(xMax, yMin, topC, new Vector2(uMax, vMax));
                var t1_vc = GetCustomVertexRaw(xMid, yMid, topC, new Vector2(uMid, vMid));
                _triangles.Add(t1_v1); _triangles.Add(t1_v2); _triangles.Add(t1_vc);

                // Right sector
                var t2_v1 = GetCustomVertexRaw(xMax, yMin, rightC, new Vector2(uMax, vMax));
                var t2_v2 = GetCustomVertexRaw(xMax, yMax, rightC, new Vector2(uMax, vMin));
                var t2_vc = GetCustomVertexRaw(xMid, yMid, rightC, new Vector2(uMid, vMid));
                _triangles.Add(t2_v1); _triangles.Add(t2_v2); _triangles.Add(t2_vc);

                // Bottom sector
                var t3_v1 = GetCustomVertexRaw(xMax, yMax, bottomC, new Vector2(uMax, vMin));
                var t3_v2 = GetCustomVertexRaw(xMin, yMax, bottomC, new Vector2(uMin, vMin));
                var t3_vc = GetCustomVertexRaw(xMid, yMid, bottomC, new Vector2(uMid, vMid));
                _triangles.Add(t3_v1); _triangles.Add(t3_v2); _triangles.Add(t3_vc);

                // Left sector
                var t4_v1 = GetCustomVertexRaw(xMin, yMax, leftC, new Vector2(uMin, vMin));
                var t4_v2 = GetCustomVertexRaw(xMin, yMin, leftC, new Vector2(uMin, vMax));
                var t4_vc = GetCustomVertexRaw(xMid, yMid, leftC, new Vector2(uMid, vMid));
                _triangles.Add(t4_v1); _triangles.Add(t4_v2); _triangles.Add(t4_vc);
            }
        }

        private int GetCustomVertexRaw(float posX, float posY, Color color, Vector2 uv)
        {
            var id = _vertices.Count;
            _vertices.Add(new Vector3(posX, posY, 0));
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