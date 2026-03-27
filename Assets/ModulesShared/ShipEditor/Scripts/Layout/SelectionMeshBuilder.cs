using System.Collections.Generic;
using UnityEngine;
using GameDatabase.Model;
using GameDatabase.Enums;

namespace ShipEditor
{
    public class SelectionMeshBuilder
    {
        public interface ICellValidator
        {
            bool IsVisible(int x, int y);
            bool IsValid(int x, int y);
            int GetCellId(int x, int y);
        }

        private readonly List<Vector3> _vertices = new();
        private readonly List<Vector2> _uv = new();
        private readonly List<Color32> _colors = new();
        private readonly List<int> _triangles = new();
        private readonly float _cellSize;
        private readonly int _x0;
        private readonly int _y0;
        private readonly ICellValidator _cellValidator;

        public Color ValidCellColor { get; set; } = Color.white;
        public Color InvalidCellColor { get; set; } = Color.white;

        public Rect DefaultUVRect = new Rect(0f, 0f, 1f, 1f);
        public Dictionary<int, Rect> CustomUVs { get; set; } = new Dictionary<int, Rect>();
        public HashSet<int> HighlightEnabledCells { get; set; } = new HashSet<int>();

        public SelectionMeshBuilder(ICellValidator cellValidator, float cellSize, int x0, int y0)
        {
            _cellValidator = cellValidator;
            _cellSize = cellSize;
            _x0 = x0;
            _y0 = y0;
        }

        public void Build(Layout layout, int x0, int y0)
        {
            var size = layout.Size;

            for (int i = 0; i < size; ++i)
            {
                for (int j = 0; j < size; ++j)
                {
                    var x = x0 + j;
                    var y = y0 + i;

                    if ((CellType)layout[j, i] == CellType.Empty) continue;
                    if (!_cellValidator.IsVisible(x, y)) continue;

                    int shipCellId = _cellValidator.GetCellId(x, y);
                    Color baseColor = _cellValidator.IsValid(x, y) ? ValidCellColor : InvalidCellColor;

                    baseColor = new Color(
                        Mathf.Clamp01(baseColor.r * 2.0f),
                        Mathf.Clamp01(baseColor.g * 2.0f),
                        Mathf.Clamp01(baseColor.b * 2.0f),
                        1f
                    );

                    Rect uvRect = DefaultUVRect;
                    if (HighlightEnabledCells != null && HighlightEnabledCells.Contains(shipCellId))
                    {
                        if (CustomUVs != null && CustomUVs.TryGetValue(shipCellId, out var customRect))
                        {
                            uvRect = customRect;
                        }
                    }

                    var v1 = GetVertex(x, y, baseColor, new Vector2(uvRect.xMin, uvRect.yMax));
                    var v2 = GetVertex(x + 1, y, baseColor, new Vector2(uvRect.xMax, uvRect.yMax));
                    var v3 = GetVertex(x + 1, y + 1, baseColor, new Vector2(uvRect.xMax, uvRect.yMin));
                    var v4 = GetVertex(x, y + 1, baseColor, new Vector2(uvRect.xMin, uvRect.yMin));

                    AddFace(v1, v2, v3, v4);
                }
            }
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

        private int GetVertex(int x, int y, Color color, Vector2 exactUV)
        {
            var id = _vertices.Count;
            _vertices.Add(new Vector3((x - _x0) * _cellSize, (_y0 - y) * _cellSize, 0));
            _uv.Add(exactUV);
            _colors.Add(color);
            return id;
        }

        private void AddFace(int v1, int v2, int v3, int v4)
        {
            _triangles.Add(v1);
            _triangles.Add(v2);
            _triangles.Add(v3);
            _triangles.Add(v3);
            _triangles.Add(v4);
            _triangles.Add(v1);
        }
    }
}