using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;
using GameDatabase.Enums;
using GameDatabase.Model;
using Services.Resources;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using ViewModel;
using Zenject;
using Constructor.Model;

namespace Gui.ShipService
{
    public class ShipLayoutPanel : MonoBehaviour, IPointerClickHandler
    {
        [Inject] private readonly IResourceLocator _resourceLocator;

        public BlockViewModel WeaponBlock;
        public BlockViewModel InnerBlock;
        public BlockViewModel OuterBlock;
        public BlockViewModel IoBlock;
        public BlockViewModel EngineBlock;
        public BlockViewModel CustomBlock;
        public BlockViewModel Selection;
        public Image BackgroundImage;

        [SerializeField] public int MinBlockSize = 64;
        [SerializeField] public Vector2 BlockSize => new Vector2(MinBlockSize, MinBlockSize);

        [SerializeField] public BlockSelectedEvent _onBlockSelected = new BlockSelectedEvent();

        [Inject] private readonly GameDatabase.IDatabase _database;

        [Serializable]
        public struct CustomSpriteBinding
        {
            public string ImageName;
            public Sprite Sprite;
        }

        [Header("Custom Sprites")]
        public List<CustomSpriteBinding> CustomSprites = new List<CustomSpriteBinding>();

        [Serializable]
        public class BlockSelectedEvent : UnityEvent<int, int> { };

        public void Reset()
        {
            _layout = new ShipLayoutAdapter(new Layout(string.Empty));
            Cleanup();

            var layoutElement = GetComponent<LayoutElement>();
            layoutElement.minWidth = 0;
            layoutElement.minHeight = 0;
        }

        public void Initialize(IShipLayout layout)
        {
            _layout = layout;
            Cleanup();
            CreateLayout();
        }

        public void ClearSelection() => Selection.gameObject.SetActive(false);

        public void OnPointerClick(PointerEventData eventData)
        {
            GetComponentPosition(eventData.position, 1, out int x, out int y);
            if ((CellType)_layout[x, y] != Layout.CustomizableCell) return;

            _onBlockSelected.Invoke(x, y);
            Selection.gameObject.SetActive(true);
            SetBlockLayout(Selection.RectTransform, x, y, 1);
        }

        private void GetComponentPosition(Vector2 point, int componentSize, out int x, out int y)
        {
            var center = transform.position;
            var scale = RectTransformHelpers.GetScreenSizeScale(GetComponent<RectTransform>());

            x = Mathf.RoundToInt((0.5f + (point.x - center.x) * scale.x / _width) * _layout.Size - 0.5f * componentSize);
            y = Mathf.RoundToInt((0.5f - (point.y - center.y) * scale.y / _height) * _layout.Size - 0.5f * componentSize);
        }

        private void OnEnable()
        {
            WeaponBlock.gameObject.SetActive(false);
            InnerBlock.gameObject.SetActive(false);
            OuterBlock.gameObject.SetActive(false);
            IoBlock.gameObject.SetActive(false);
            EngineBlock.gameObject.SetActive(false);
            CustomBlock.gameObject.SetActive(false);
            Cleanup();
        }

        private void CreateLayout()
        {
            var areaSize = _layout.Size * MinBlockSize;
            var layoutElement = GetComponent<LayoutElement>();

            layoutElement.minWidth = areaSize;
            layoutElement.minHeight = areaSize;

            _width = areaSize;
            _height = areaSize;
            _blocks.Clear();

            for (int i = _layout.Rect.yMin; i <= _layout.Rect.yMax; ++i)
            {
                for (int j = _layout.Rect.xMin; j <= _layout.Rect.xMax; ++j)
                {
                    var item = CreateBlock(_layout[j, i]);
                    _blocks.Add(item);
                    if (item != null) SetBlockLayout(item.RectTransform, j, i, 1);
                }
            }
        }

        private BlockViewModel CreateBlock(CellType cell)
        {
            switch (cell)
            {
                case CellType.Outer: return GameObject.Instantiate(OuterBlock);
                case CellType.Inner: return GameObject.Instantiate(InnerBlock);
                case CellType.InnerOuter: return GameObject.Instantiate(IoBlock);
                case CellType.Weapon:
                case Layout.CustomWeaponCell: return GameObject.Instantiate(WeaponBlock);
                case CellType.Engine: return GameObject.Instantiate(EngineBlock);
                case Layout.CustomizableCell: return GameObject.Instantiate(CustomBlock);
            }

            try
            {
                if (_database?.CellSettings != null)
                {
                    foreach (var c in _database.CellSettings.Cells)
                    {
                        if (c != null && !string.IsNullOrEmpty(c.Symbol) && c.Symbol[0] == (int)cell)
                        {
                            var customItem = GameObject.Instantiate(InnerBlock);
                            var image = customItem.GetComponent<Image>();

                            if (image != null)
                            {
                                var effect = image.gameObject.AddComponent<CellColorEffect>();
                                effect.C1 = c.Color;
                                effect.C2 = c.Color2;
                                effect.C3 = c.Color3;
                                effect.C4 = c.Color4;

                                image.color = Color.white;

                                if (!string.IsNullOrEmpty(c.Image))
                                {
                                    var binding = CustomSprites.Find(x => x.ImageName == c.Image);
                                    Sprite foundSprite = binding.Sprite ?? Resources.Load<Sprite>(c.Image) ?? _resourceLocator?.GetSprite(c.Image);

                                    if (foundSprite != null)
                                    {
                                        image.sprite = foundSprite;
                                        image.type = Image.Type.Simple;
                                        image.preserveAspect = true;
                                    }
                                }
                            }
                            return customItem;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private void SetBlockLayout(RectTransform item, int x, int y, int size)
        {
            item.SetParent(RectTransform);
            item.localScale = Vector3.one;
            item.gameObject.SetActive(true);
            item.anchoredPosition = new Vector2(x * _width / _layout.Size, -y * _height / _layout.Size);
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, BlockSize.x * size);
            item.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, BlockSize.y * size);
        }

        private void Cleanup()
        {
            foreach (Transform child in transform)
            {
                if (child == WeaponBlock.transform || child == InnerBlock.transform || child == OuterBlock.transform ||
                    child == EngineBlock.transform || child == IoBlock.transform || child == CustomBlock.transform ||
                    child == Selection.transform || child == BackgroundImage.transform) continue;
                Destroy(child.gameObject);
            }
            _blocks.Clear();
            ClearSelection();
        }

        private RectTransform RectTransform => _rectTransform ??= GetComponent<RectTransform>();

        private IShipLayout _layout;
        private float _width;
        private float _height;
        private RectTransform _rectTransform;
        private readonly List<BlockViewModel> _blocks = new List<BlockViewModel>();
    }

    public class CellColorEffect : BaseMeshEffect
    {
        public Color C1 = Color.white;
        public Color C2 = Color.clear;
        public Color C3 = Color.clear;
        public Color C4 = Color.clear;

        private bool IsColorPresent(Color c) => c.a > 0.01f && !(c.r <= 0.01f && c.g <= 0.01f && c.b <= 0.01f);

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount < 4) return;

            UIVertex v = new UIVertex();
            vh.PopulateUIVertex(ref v, 0); var p0 = v.position; var uv0 = v.uv0;
            vh.PopulateUIVertex(ref v, 1); var p1 = v.position; var uv1 = v.uv0;
            vh.PopulateUIVertex(ref v, 2); var p2 = v.position; var uv2 = v.uv0;
            vh.PopulateUIVertex(ref v, 3); var p3 = v.position; var uv3 = v.uv0;

            vh.Clear();

            bool hasC2 = IsColorPresent(C2), hasC3 = IsColorPresent(C3), hasC4 = IsColorPresent(C4);
            var pC = (p0 + p2) * 0.5f; var uvC = (uv0 + uv2) * 0.5f;

            if (hasC4 && hasC3 && hasC2)
            {
                AddTriangle(vh, p1, uv1, p2, uv2, pC, uvC, C1);
                AddTriangle(vh, p3, uv3, p0, uv0, pC, uvC, C2);
                AddTriangle(vh, p0, uv0, p1, uv1, pC, uvC, C3);
                AddTriangle(vh, p2, uv2, p3, uv3, pC, uvC, C4);
            }
            else if (hasC3 && hasC2)
            {
                AddTriangle(vh, p1, uv1, p2, uv2, p0, uv0, C1);
                AddTriangle(vh, p3, uv3, pC, uvC, p0, uv0, C2);
                AddTriangle(vh, p3, uv3, p2, uv2, pC, uvC, C3);
            }
            else if (hasC2)
            {
                AddTriangle(vh, p1, uv1, p2, uv2, p0, uv0, C1);
                AddTriangle(vh, p3, uv3, p0, uv0, p2, uv2, C2);
            }
            else
            {
                AddTriangle(vh, p1, uv1, p2, uv2, p0, uv0, C1);
                AddTriangle(vh, p3, uv3, p0, uv0, p2, uv2, C1);
            }
        }

        private void AddTriangle(VertexHelper vh, Vector3 p1, Vector2 uv1, Vector3 p2, Vector2 uv2, Vector3 p3, Vector2 uv3, Color color)
        {
            int i = vh.currentVertCount;
            UIVertex v = new UIVertex();
            v.color = color;
            v.position = p1; v.uv0 = uv1; vh.AddVert(v);
            v.position = p2; v.uv0 = uv2; vh.AddVert(v);
            v.position = p3; v.uv0 = uv3; vh.AddVert(v);
            vh.AddTriangle(i, i + 1, i + 2);
        }
    }
}