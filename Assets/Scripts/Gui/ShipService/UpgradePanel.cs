using Constructor.Ships;
using Economy;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameServices.Gui;
using GameServices.Player;
using Services.Audio;
using Services.Localization;
using UnityEngine;
using Services.Resources;
using UnityEngine.UI;
using ViewModel.Common;
using Zenject;
using System.Collections.Generic;

namespace Gui.ShipService
{
    public class UpgradePanel : MonoBehaviour
    {
        [SerializeField] private ShipLayoutPanel _shipLayout;
        [SerializeField] private ToggleGroup _cellToggleGroup;

        [SerializeField] private PricePanel _price1;
        [SerializeField] private PricePanel _price2;
        [SerializeField] private PricePanel _price3;

        [SerializeField] private Text _warningText;
        [SerializeField] private Text _nothingSelectedText;
        [SerializeField] private Text _selectCellTypeText;
        [SerializeField] private Button _addButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Toggle _outerBlockToggle;
        [SerializeField] private Toggle _innerBlockToggle;
        [SerializeField] private Toggle _engineBlockToggle;
        [SerializeField] private Toggle _weaponBlockToggle;

        [SerializeField] private AudioClip _buySound;

        [Inject] private readonly GuiHelper _guiHelper;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly PlayerInventory _playerInventory;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly ISoundPlayer _soundPlayer;

        [Inject] private readonly GameDatabase.IDatabase _database;

        private Dictionary<Toggle, CellType> _toggleToCellType = new Dictionary<Toggle, CellType>();
        private List<GameObject> _customToggles = new List<GameObject>();
        private bool _togglesSetup = false;

        public void Initialize(IShip ship, Faction faction, int level)
        {
            _ship = ship;
            _faction = faction;
            _level = level;
            _selectedBlockX = _invalidBlock;
            _selectedBlockY = _invalidBlock;
            _shipLayout.ClearSelection();
            _shipInfo = new ShipInformation(_ship, _faction, _level);

            SetupCustomToggles();
            UpdateControls();
        }

        private void SetupCustomToggles()
        {
            if (_togglesSetup) return;
            _togglesSetup = true;

            _toggleToCellType[_outerBlockToggle] = CellType.Outer;
            _toggleToCellType[_innerBlockToggle] = CellType.Inner;
            _toggleToCellType[_engineBlockToggle] = CellType.Engine;
            _toggleToCellType[_weaponBlockToggle] = CellType.Weapon;

            try
            {
                if (_database != null && _database.CellSettings != null)
                {
                    foreach (var c in _database.CellSettings.Cells)
                    {
                        if ((object)c != null && !string.IsNullOrEmpty(c.Symbol))
                        {
                            CellType customType = (CellType)c.Symbol[0];
                            UnityEngine.Color baseC = c.Color;

                            GameObject newToggleGo = Instantiate(_innerBlockToggle.gameObject, _innerBlockToggle.transform.parent);
                            newToggleGo.SetActive(true);

                            // Move to the end of the list and scale down to create margins
                            newToggleGo.transform.SetAsLastSibling();
                            newToggleGo.transform.localScale = new Vector3(0.95f, 0.95f, 1f);

                            Toggle newToggle = newToggleGo.GetComponent<Toggle>();
                            newToggle.group = _cellToggleGroup;

                            Image rootImage = newToggleGo.GetComponent<Image>();
                            Image targetGraphicImage = newToggle.targetGraphic as Image;
                            Image checkmarkImage = newToggle.graphic as Image;
                            Transform iconTransform = newToggleGo.transform.Find("Icon") ?? newToggleGo.transform.Find("ItemIcon");
                            Image iconImage = (iconTransform != null) ? iconTransform.GetComponent<Image>() : null;

                            if (!string.IsNullOrEmpty(c.Image))
                            {
                                Sprite customSprite = _resourceLocator.GetSprite(c.Image) ?? Resources.Load<Sprite>(c.Image);
                                if (customSprite != null)
                                {
                                    // 1. Base shape
                                    if (rootImage != null)
                                    {
                                        rootImage.sprite = customSprite;
                                        rootImage.type = Image.Type.Simple;
                                        rootImage.color = baseC;
                                        rootImage.transform.SetAsLastSibling();
                                    }

                                    // 2. Selection highlight (white overlay)
                                    if (checkmarkImage != null)
                                    {
                                        checkmarkImage.sprite = customSprite;
                                        checkmarkImage.type = Image.Type.Simple;
                                        checkmarkImage.color = new UnityEngine.Color(1f, 1f, 1f, 0.5f);

                                        checkmarkImage.rectTransform.anchorMin = Vector2.zero;
                                        checkmarkImage.rectTransform.anchorMax = Vector2.one;
                                        checkmarkImage.rectTransform.offsetMin = Vector2.zero;
                                        checkmarkImage.rectTransform.offsetMax = Vector2.zero;
                                        checkmarkImage.rectTransform.localScale = new Vector3(1.05f, 1.05f, 1f);

                                        checkmarkImage.transform.SetAsLastSibling();
                                    }

                                    // 3. Gear icon
                                    if (iconImage != null)
                                    {
                                        iconImage.gameObject.SetActive(true);
                                        iconImage.color = baseC;
                                        iconImage.transform.SetAsLastSibling();
                                    }

                                    // 4. Shadow mask container
                                    GameObject maskGo = new GameObject("ShadowMask");
                                    maskGo.transform.SetParent(newToggleGo.transform, false);
                                    RectTransform maskRt = maskGo.AddComponent<RectTransform>();
                                    maskRt.anchorMin = Vector2.zero;
                                    maskRt.anchorMax = Vector2.one;
                                    maskRt.offsetMin = Vector2.zero;
                                    maskRt.offsetMax = Vector2.zero;
                                    maskRt.localScale = Vector3.one;

                                    Image maskImg = maskGo.AddComponent<Image>();
                                    maskImg.sprite = customSprite;
                                    maskImg.type = Image.Type.Simple;

                                    Mask mask = maskGo.AddComponent<Mask>();
                                    mask.showMaskGraphic = false;
                                    maskGo.transform.SetAsLastSibling();

                                    // 5. Disabled state shadow (solid square cut by the mask)
                                    if (targetGraphicImage != null)
                                    {
                                        targetGraphicImage.enabled = true;
                                        targetGraphicImage.transform.SetParent(maskGo.transform, false);

                                        targetGraphicImage.sprite = null;
                                        targetGraphicImage.type = Image.Type.Simple;
                                        targetGraphicImage.color = UnityEngine.Color.white;

                                        targetGraphicImage.rectTransform.anchorMin = Vector2.zero;
                                        targetGraphicImage.rectTransform.anchorMax = Vector2.one;
                                        targetGraphicImage.rectTransform.offsetMin = Vector2.zero;
                                        targetGraphicImage.rectTransform.offsetMax = Vector2.zero;

                                        newToggle.targetGraphic = targetGraphicImage;
                                    }

                                    // Set transitions: disabled state uses 0.6 alpha shadow
                                    ColorBlock cb = newToggle.colors;
                                    cb.normalColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                    cb.highlightedColor = new UnityEngine.Color(1f, 1f, 1f, 0.1f);
                                    cb.pressedColor = new UnityEngine.Color(0f, 0f, 0f, 0.3f);
                                    cb.selectedColor = new UnityEngine.Color(0f, 0f, 0f, 0f);
                                    cb.disabledColor = new UnityEngine.Color(0f, 0f, 0f, 0.6f);
                                    cb.colorMultiplier = 1f;
                                    newToggle.colors = cb;
                                }
                            }
                            else
                            {
                                // Vanilla square cell fallback
                                if (rootImage != null) rootImage.color = baseC;
                                if (iconImage != null)
                                {
                                    iconImage.gameObject.SetActive(true);
                                    iconImage.color = baseC;
                                }
                                if (targetGraphicImage != null) targetGraphicImage.color = baseC;
                                if (checkmarkImage != null) checkmarkImage.color = baseC;
                            }

                            // Cleanup unused vanilla layers, preserving the new ShadowMask
                            foreach (var img in newToggleGo.GetComponentsInChildren<Image>(true))
                            {
                                if (img != rootImage &&
                                    img != targetGraphicImage &&
                                    img != checkmarkImage &&
                                    img != iconImage &&
                                    img.gameObject.name != "ShadowMask")
                                {
                                    img.enabled = false;
                                }
                            }

                            newToggle.onValueChanged.AddListener((isOn) => {
                                if (isOn) OnCellTypeSelected(newToggle);
                            });

                            _toggleToCellType[newToggle] = customType;
                            _customToggles.Add(newToggleGo);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MOD] Cell toggle generation error: {e.Message}");
            }
        }

        public void OnBlockSelected(int x, int y)
        {
            _selectedBlockX = x;
            _selectedBlockY = y;

            foreach (var kvp in _toggleToCellType)
            {
                kvp.Key.isOn = (kvp.Key == _outerBlockToggle);
            }

            _selectedCellType = CellType.Outer;
            UpdateControls();
        }

        public void OnCellTypeSelected(Toggle toggle)
        {
            if (_toggleToCellType.TryGetValue(toggle, out CellType type))
            {
                _selectedCellType = type;
            }
        }

        public void OnAddButtonClicked()
        {
            var isBlockSelected = _selectedBlockX != _invalidBlock && _selectedBlockY != _invalidBlock;
            if (!isBlockSelected || !_shipInfo.IsShipLevelEnough || !_shipInfo.IsShipyardLevelEnough ||
                !_shipInfo.Price1.IsEnough(_playerResources) || !_shipInfo.Price2.IsEnough(_playerResources)) return;
            if (!_ship.Model.LayoutModifications.TryAddCell(_selectedBlockX, _selectedBlockY, _selectedCellType)) return;

            _shipInfo.Price1.Withdraw(_playerResources);
            _shipInfo.Price2.Withdraw(_playerResources);

            _soundPlayer.Play(_buySound);
            _selectedBlockX = _selectedBlockY = _invalidBlock;
            _shipInfo = new ShipInformation(_ship, _faction, _level);
            _shipLayout.Initialize(_ship.Model.Layout);
            UpdateControls();
        }

        public void OnResetButtonClicked()
        {
            _guiHelper.ShowConfirmation(_localization.GetString("$RemoveShipCellsWarning"), ResetLayout);
        }

        public void ResetLayout()
        {
            if (!_shipInfo.CanReset || !_shipInfo.ResetPrice.IsEnough(_playerResources)) return;

            _ship.Model.LayoutModifications.Reset();
            Domain.Shipyard.ShipValidator.RemoveInvalidParts(_ship, new Domain.Shipyard.FleetPartsStorage(_playerInventory));

            _shipInfo.ResetPrice.Withdraw(_playerResources);
            _soundPlayer.Play(_buySound);
            _selectedBlockX = _selectedBlockY = _invalidBlock;
            _shipInfo = new ShipInformation(_ship, _faction, _level);
            _shipLayout.Initialize(_ship.Model.Layout);
            UpdateControls();
        }

        private void UpdateControls()
        {
            var isBlockSelected = _selectedBlockX != _invalidBlock && _selectedBlockY != _invalidBlock;

            if (!_shipInfo.IsShipLevelEnough)
            {
                _warningText.gameObject.SetActive(true);
                _warningText.text = _localization.GetString("$LowLevelText", _shipInfo.RequiredLevel.ToString());
            }
            else if (!_shipInfo.IsShipyardLevelEnough)
            {
                _warningText.gameObject.SetActive(true);
                _warningText.text = _localization.GetString("$RequiredShipyardLevel", _shipInfo.RequiredShipyardLevel, _localization.GetString(_ship.Model.Faction.Name));
            }
            else
            {
                _warningText.gameObject.SetActive(false);
            }

            _nothingSelectedText.gameObject.SetActive(!isBlockSelected && _shipInfo.IsShipLevelEnough);
            _selectCellTypeText.gameObject.SetActive(isBlockSelected);

            _cellToggleGroup.gameObject.SetActive(isBlockSelected);

            if (isBlockSelected)
            {
                var price1 = _shipInfo.Price1;
                var price2 = _shipInfo.Price2;

                _price1.Initialize(price1, price1.IsEnough(_playerResources));
                _price2.Initialize(price2, price2.IsEnough(_playerResources));
                _price2.gameObject.SetActive(price2.Amount > 0);

                _addButton.interactable = _shipInfo.IsShipLevelEnough && _shipInfo.IsShipyardLevelEnough && price1.IsEnough(_playerResources) && price2.IsEnough(_playerResources);

                foreach (var kvp in _toggleToCellType)
                {
                    kvp.Key.interactable = _ship.Model.LayoutModifications.IsCellValid(_selectedBlockX, _selectedBlockY, kvp.Value);
                }
            }
            else
            {
                _price1.Initialize(Currency.Credits);
                _price2.Initialize(Currency.Stars);
#if IAP_DISABLED
                _price2.gameObject.SetActive(false);
#else
                _price2.gameObject.SetActive(true);
#endif
                _addButton.interactable = false;
            }

            if (_shipInfo.CanReset)
                _price3.Initialize(_shipInfo.ResetPrice, _shipInfo.ResetPrice.IsEnough(_playerResources));
            else
#if IAP_DISABLED
                _price3.Initialize(Currency.Credits);
#else
                _price3.Initialize(Currency.Stars);
#endif

            _resetButton.interactable = _shipInfo.CanReset && _shipInfo.ResetPrice.IsEnough(_playerResources);
        }

        private CellType _selectedCellType = CellType.Outer;
        private int _selectedBlockX = _invalidBlock;
        private int _selectedBlockY = _invalidBlock;
        private IShip _ship;
        private int _level;
        private Faction _faction;
        private ShipInformation _shipInfo;
        private const int _invalidBlock = int.MaxValue;

        private struct ShipInformation
        {
            public ShipInformation(IShip ship, Faction shipyardFaction, int shipyardLevel)
            {
                _shipLevel = ship.Experience.Level;

                TotalCells = ship.Model.LayoutModifications.TotalExtraCells();
                Cells = ship.Model.LayoutModifications.ExtraCells();

#if IAP_DISABLED
                var starsAllowed = false;
#else
                var starsAllowed = true;
#endif

                Price1 = Price.Common(starsAllowed ? (Cells + 1) * 1000 : (Cells + 1) * 2000);
                Price2 = starsAllowed ? Price.Premium(1 + Cells / 2) : new Price(0, Currency.Credits);
                ResetPrice = starsAllowed ? Price.Premium(Cells) : Price.Common(Cells * 2000);

                RequiredLevel = TotalCells > 0 ? 5 + Mathf.Min(5 * Cells, 95 * Cells / TotalCells) : 0;

                RequiredShipyardLevel = Cells * 5 + 5;
                IsShipyardLevelEnough = shipyardLevel >= RequiredShipyardLevel &&
                    (ship.Model.Faction == Faction.Empty || ship.Model.Faction == shipyardFaction);
            }

            public bool CanReset { get { return Cells > 0; } }

            public bool IsShipLevelEnough { get { return _shipLevel >= RequiredLevel; } }

            public readonly bool IsShipyardLevelEnough;
            public readonly int RequiredShipyardLevel;
            public readonly int TotalCells;
            public readonly int Cells;
            public readonly Price Price1;
            public readonly Price Price2;
            public readonly Price ResetPrice;
            public readonly int RequiredLevel;

            private readonly int _shipLevel;
        }
    }
}