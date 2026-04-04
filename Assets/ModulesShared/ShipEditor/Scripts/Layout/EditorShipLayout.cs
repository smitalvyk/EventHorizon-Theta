using System;
using System.Linq;
using System.Collections.Generic;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using UnityEngine;
using Zenject;
using Constructor.Model;

namespace ShipEditor
{
    public class EditorShipLayout : MonoBehaviour
    {
        [SerializeField] private Color _innerCellColor;
        [SerializeField] private Color _outerCellColor;
        [SerializeField] private Color _engineCellColor;
        [SerializeField] private Color _weaponCellColor;
        [SerializeField] private Color _validCellColor;
        [SerializeField] private Color _invalidCellColor;
        [SerializeField] private Color _lockedCellColor;

        [SerializeField] private Transform _content;
        [SerializeField] private ShipLayoutElement _body;
        [SerializeField] private ShipModulesLayout _modules;
        [SerializeField] private ShipLayoutElement _selection;
        [SerializeField] private ShipLayoutElement _lockedCells;
        [SerializeField] private WeaponClassLayout _weaponClasses;
        [SerializeField] private SpriteRenderer _shipImage;

        [SerializeField] private float _lockSize = 0.5f;

        [Inject] private readonly GameDatabase.IDatabase _database;

        private float _cellSize;

        private Rect _defaultUVRect = new Rect(0f, 0f, 1f, 1f);
        private Dictionary<int, Rect> _customUVs = new Dictionary<int, Rect>();
        private HashSet<int> _highlightEnabledCells = new HashSet<int>();

        private Texture2D _originalBaseTexture;
        private bool _textureCaptured = false;

        public float X0 => _shipLayout == null ? 0 : _shipLayout.Rect.xMin;
        public float Y0 => _shipLayout == null ? 0 : _shipLayout.Rect.yMin;
        public float Width => _shipLayout == null ? 0 : _shipLayout.Rect.Width * _cellSize;
        public float Height => _shipLayout == null ? 0 : _shipLayout.Rect.Height * _cellSize;
        public int OriginalSize => _shipLayout.OriginalSize;

        public Vector3 ContentOffset => _content.localPosition;

        private LockedCellsMeshBuilder _lockedCellBuilder;
        private Model.IShipLayoutModel _shipLayout;
        private Vector2Int _selectedPosition;

        public void Initialize(Model.IShipLayoutModel layout, Sprite sprite, float cellSize)
        {
            _cellSize = cellSize;
            _shipLayout = layout;
            _selection.SetMesh(null);
            if (_shipLayout != null) _shipLayout.Database = _database;

            GenerateMesh();
            GenerateModules();
            GenerateWeaponClasses();

            _shipImage.gameObject.SetActive(sprite != null);
            if (sprite != null)
            {
                _shipImage.sprite = sprite;
                var size = _shipLayout == null ? 0 : _shipLayout.OriginalSize;
                var offsetX = _shipLayout == null ? 0 : (0.5f * size - _shipLayout.Rect.xMin) * _cellSize;
                var offsetY = _shipLayout == null ? 0 : (0.5f * size - _shipLayout.Rect.yMin) * _cellSize;

                _shipImage.transform.localPosition = new Vector3(offsetX, -offsetY, _shipImage.transform.localPosition.z);
                _shipImage.transform.localScale = size * _cellSize * Vector3.one;
            }

            _content.localPosition = new Vector3(-Width / 2, Height / 2, 0);
        }

        public void GenerateModules()
        {
            if (_shipLayout == null)
            {
                _modules.Initialize(_cellSize, 0, 0);
                return;
            }

            _modules.Initialize(_cellSize, _shipLayout.Rect.xMin, _shipLayout.Rect.yMin);
            foreach (var item in _shipLayout.Components)
                _modules.AddComponent(item.X, item.Y, item.Data, false);

            _modules.UpdateMesh();
            GenerateLockedCells();
        }

        public void UpdateComponent(Model.IComponentModel component)
        {
            if (_lockedCellBuilder == null) return;

            bool result;
            if (component.Locked)
                result = _lockedCellBuilder.TryAddElement(component.Data.Layout, component.X, component.Y);
            else
                result = _lockedCellBuilder.TryRemoveElement(component.Data.Layout, component.X, component.Y);

            if (result)
                _lockedCells.SetMesh(_lockedCellBuilder.CreateMesh());
        }

        public void ShowSelection(Vector2Int position, GameDatabase.DataModel.Component component)
        {
            if (component == null || _shipLayout == null)
            {
                _selection.SetMesh(null);
                return;
            }

            if (position == _selectedPosition) return;
            _selectedPosition = position;

            var cellValidator = new CellValidator(_shipLayout, component);
            var builder = new SelectionMeshBuilder(cellValidator, _cellSize, _shipLayout.Rect.xMin, _shipLayout.Rect.yMin);
            builder.ValidCellColor = _validCellColor;
            builder.InvalidCellColor = _invalidCellColor;

            builder.DefaultUVRect = _defaultUVRect;
            builder.CustomUVs = _customUVs;
            builder.HighlightEnabledCells = _highlightEnabledCells;

            builder.Build(component.Layout, position.x, position.y);
            _selection.SetMesh(builder.CreateMesh());
        }

        public void AddComponent(Model.IComponentModel component)
        {
            _modules.AddComponent(component.X, component.Y, component.Data);
            _weaponClasses.AddComponent(component.X, component.Y, component.Data.Layout);
        }

        public void RemoveComponent(Model.IComponentModel component)
        {
            GenerateModules();
            _weaponClasses.RemoveComponent(component.X, component.Y, component.Data.Layout);
        }

        public void GenerateWeaponClasses()
        {
            _weaponClasses.Cleanup();
            if (_shipLayout == null) return;
            _weaponClasses.Initialize(_cellSize, new LayoutAdapter(_shipLayout));
            foreach (var item in _shipLayout.Components)
                _weaponClasses.AddComponent(item.X, item.Y, item.Data.Layout);
        }

        private void GenerateMesh()
        {
            _body.SetMesh(null);
            if (_shipLayout == null) return;

            var builder = new ShipMeshBuilder(_cellSize);
            builder.OuterCellColor = _outerCellColor;
            builder.InnerCellColor = _innerCellColor;
            builder.EngineCellColor = _engineCellColor;
            builder.WeaponCellColor = _weaponCellColor;

            MeshRenderer bodyRenderer = _body.GetComponent<MeshRenderer>();

            if (!_textureCaptured)
            {
                _originalBaseTexture = bodyRenderer.sharedMaterial.mainTexture as Texture2D;
                _textureCaptured = true;
            }

            List<Texture2D> texturesToPack = new List<Texture2D>();
            if (_originalBaseTexture != null) texturesToPack.Add(_originalBaseTexture);

            Dictionary<int, int> symbolToTexIndex = new Dictionary<int, int>();

            if (_database != null && _database.CellSettings != null)
            {
                foreach (var cellData in _database.CellSettings.Cells)
                {
                    if (!string.IsNullOrEmpty(cellData.Symbol) && !string.IsNullOrEmpty(cellData.Image))
                    {
                        // Strip file extensions to safely load via Resources
                        string cleanName = cellData.Image.Replace(".png", "").Replace(".jpg", "");
                        Sprite s = Resources.Load<Sprite>(cleanName);

                        if (s != null && s.texture != null)
                        {
                            if (!texturesToPack.Contains(s.texture))
                            {
                                texturesToPack.Add(s.texture);
                                symbolToTexIndex[cellData.Symbol[0]] = texturesToPack.Count - 1;
                            }
                            else
                            {
                                symbolToTexIndex[cellData.Symbol[0]] = texturesToPack.IndexOf(s.texture);
                            }
                        }
                    }
                }
            }

            _customUVs.Clear();
            _highlightEnabledCells.Clear();

            if (texturesToPack.Count > 1)
            {
                Texture2D atlas = new Texture2D(2048, 2048, TextureFormat.RGBA32, false);
                Rect[] rects = atlas.PackTextures(texturesToPack.ToArray(), 2, 2048);

                if (rects != null && rects.Length > 0)
                {
                    Material atlasMat = new Material(bodyRenderer.sharedMaterial);
                    atlasMat.mainTexture = atlas;
                    bodyRenderer.material = atlasMat;

                    MeshRenderer selectionRenderer = _selection.GetComponent<MeshRenderer>();
                    if (selectionRenderer != null && selectionRenderer.sharedMaterial != null)
                    {
                        Material selectionMat = new Material(selectionRenderer.sharedMaterial);
                        selectionMat.mainTexture = atlas;
                        selectionRenderer.material = selectionMat;
                    }

                    if (_originalBaseTexture != null)
                    {
                        builder.DefaultUVRect = rects[0];
                        _defaultUVRect = rects[0];
                    }

                    foreach (var cellData in _database.CellSettings.Cells)
                    {
                        if (!string.IsNullOrEmpty(cellData.Symbol))
                        {
                            int cellChar = cellData.Symbol[0];
                            Rect uvRect = builder.DefaultUVRect;

                            if (!string.IsNullOrEmpty(cellData.Image) && symbolToTexIndex.ContainsKey(cellChar))
                            {
                                int texIndex = symbolToTexIndex[cellChar];
                                uvRect = rects[texIndex];
                            }

                            Color c1 = cellData.Color;
                            if (c1.a == 0f) c1 = Color.white;

                            builder.CustomCells[cellChar] = new ShipMeshBuilder.CustomCellInfo
                            {
                                Color1 = c1,
                                Color2 = cellData.Color2,
                                Color3 = cellData.Color3,
                                Color4 = cellData.Color4,
                                UVRect = uvRect,
                                MergeCells = cellData.MergeCells
                            };

                            _customUVs[cellChar] = uvRect;

                            if (cellData.EnableCustomShapeHighlight)
                                _highlightEnabledCells.Add(cellChar);
                        }
                    }
                }
            }
            else
            {
                MeshRenderer selectionRenderer = _selection.GetComponent<MeshRenderer>();
                if (selectionRenderer != null && selectionRenderer.sharedMaterial != null)
                {
                    Material selectionMat = new Material(selectionRenderer.sharedMaterial);
                    selectionMat.mainTexture = _originalBaseTexture;
                    selectionRenderer.material = selectionMat;
                }

                if (_database != null && _database.CellSettings != null)
                {
                    foreach (var cellData in _database.CellSettings.Cells)
                    {
                        if (!string.IsNullOrEmpty(cellData.Symbol))
                        {
                            int cellChar = cellData.Symbol[0];
                            Color c1 = cellData.Color;
                            if (c1.a == 0f) c1 = Color.white;

                            builder.CustomCells[cellChar] = new ShipMeshBuilder.CustomCellInfo
                            {
                                Color1 = c1,
                                Color2 = cellData.Color2,
                                Color3 = cellData.Color3,
                                Color4 = cellData.Color4,
                                UVRect = builder.DefaultUVRect,
                                MergeCells = cellData.MergeCells
                            };

                            _customUVs[cellChar] = builder.DefaultUVRect;

                            if (cellData.EnableCustomShapeHighlight)
                                _highlightEnabledCells.Add(cellChar);
                        }
                    }
                }
            }

            builder.Build(new LayoutAdapter(_shipLayout));
            _body.SetMesh(builder.CreateMesh());
        }

        private void GenerateLockedCells()
        {
            _lockedCells.SetMesh(null);
            if (_shipLayout == null) return;

            _lockedCellBuilder = new LockedCellsMeshBuilder(_cellSize, _shipLayout.Rect.xMin, _shipLayout.Rect.yMin, _lockSize);
            _lockedCellBuilder.Color = _lockedCellColor;

            foreach (var item in _shipLayout.Components)
                if (item.Locked)
                    _lockedCellBuilder.TryAddElement(item.Data.Layout, item.X, item.Y);

            _lockedCells.SetMesh(_lockedCellBuilder.CreateMesh());
        }

        private class CellValidator : SelectionMeshBuilder.ICellValidator
        {
            private readonly Model.IShipLayoutModel _model;
            private readonly GameDatabase.DataModel.Component _component;

            public CellValidator(Model.IShipLayoutModel model, GameDatabase.DataModel.Component component)
            {
                _model = model;
                _component = component;
            }

            public bool IsValid(int x, int y)
            {
                return _model.IsCellCompatible(x, y, _component);
            }

            public bool IsVisible(int x, int y)
            {
                return _model.Rect.IsInsideRect(x, y);
            }

            public int GetCellId(int x, int y)
            {
                return (int)_model.Cell(x, y);
            }
        }

        private class LayoutAdapter : ShipMeshBuilder.ILayout
        {
            private readonly Model.IShipLayoutModel _model;
            public LayoutAdapter(Model.IShipLayoutModel model) => _model = model;
            public ref readonly LayoutRect Rect => ref _model.Rect;
            public CellType this[int x, int y] => _model.Cell(x, y);
            public string GetWeaponClasses(int x, int y) => _model.Barrel(x, y)?.WeaponClass;
        }
    }
}