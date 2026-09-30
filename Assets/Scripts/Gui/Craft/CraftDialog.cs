using CommonComponents;
using DataModel.Technology;
using Economy.Products;
using Galaxy;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameServices.Database;
using GameServices.Gui;
using GameServices.Player;
using GameServices.Random;
using GameServices.Research;
using Services.Audio;
using Services.Gui;
using Services.Localization;
using Services.Messenger;
using Services.ObjectPool;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Gui.Craft
{
    public class CraftDialog : MonoBehaviour
    {
        [Inject] private readonly ISoundPlayer _soundPlayer;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly IRandom _random;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly Research _research;
        [Inject] private readonly ITechnologies _technologies;
        [Inject] private readonly MotherShip _motherShip;
        [Inject] private readonly IGameObjectFactory _gameObjectFactory;
        [Inject] private readonly GuiHelper _helper;
        [Inject] private readonly Galaxy.StarMap _starMap;

        [Inject]
        private void Initialize(IMessenger messenger)
        {
            messenger.AddListener<Money>(EventType.MoneyValueChanged, value => UpdateResources());
            messenger.AddListener<Money>(EventType.StarsValueChanged, value => UpdateResources());
            messenger.AddListener(EventType.TechPointsChanged, UpdateResources);
        }

        [SerializeField] private LayoutGroup _technologyList;
        [SerializeField] private Image _factionIcon;
        [SerializeField] private Text _factionText;
        [SerializeField] private Text _levelText;
        [SerializeField] private ToggleGroup _techGroup;
        [SerializeField] private AudioClip _buySound;
        [SerializeField] private CraftPanel _commonCraftPanel;
        [SerializeField] private CraftPanel _improvedCraftPanel;
        [SerializeField] private CraftPanel _superiorCraftPanel;
        [SerializeField] private Text _creditsText;
        [SerializeField] private Text _starsText;
        [SerializeField] private Text _techsText;
        [SerializeField] private Image _techsIcon;

        [Header("Sorting Bar (Top of TechList)")]
        [SerializeField] private GameObject _sortingButton;
        [SerializeField] private Text _sortTitleText;
        [SerializeField] private Text _sortModeText;

        [Header("Navigation: Category, Back & Info")]
        [SerializeField] private GameObject _categoryTemplate;
        [SerializeField] private GameObject _buttonsBar;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _infoButton;

        [Header("Category Layout")]
        [SerializeField] private float _categorySpacing = -16f;

        [Header("Free Stars Tech Panel")]
        [SerializeField] private GameObject _freeTechsPanel;
        [SerializeField] private Text _freeTechsText;
        [SerializeField] private Image _freeTechsIcon;

        [Header("Category Icons")]
        [SerializeField] private Sprite _weaponCategoryIcon;
        [SerializeField] private Sprite _armorCategoryIcon;
        [SerializeField] private Sprite _energyCategoryIcon;
        [SerializeField] private Sprite _engineCategoryIcon;
        [SerializeField] private Sprite _droneCategoryIcon;
        [SerializeField] private Sprite _shipCategoryIcon;
        [SerializeField] private Sprite _satelliteCategoryIcon;
        [SerializeField] private Sprite _otherCategoryIcon;

        private ScrollRect _scrollRect;
        private RectTransform _itemsContent;
        private RectTransform _categoryContent;
        private Dictionary<TechCategory, List<ITechnology>> _categorizedTechs = new Dictionary<TechCategory, List<ITechnology>>();
        private TechCategory? _currentCategory = null;
        private SortMode _currentSortMode = SortMode.Default;
        private Faction _freeStarsFaction;

        public void OnItemCreated(IProduct item)
        {
            UpdateTechPanel();
            _soundPlayer.Play(_buySound);
            _helper.ShowItemInfoWindow(item);
        }

        public void OnTechItemSelected(ITechnology tech)
        {
            _selectedTech = tech;
            UpdateTechPanel();
        }

        public void OnTechItemDeselected(ITechnology tech)
        {
            _selectedTech = null;
            UpdateTechPanel();
        }

        public void InfoButtonClicked()
        {
            if (_selectedTech == null) return;

            IProduct product = null;
            if (_selectedTech is TechnologyBase techBase)
            {
                try
                {
                    product = techBase.CreateItem(CraftItemQuality.Common, null);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("CraftDialog: Failed to create preview item: " + ex.Message);
                }
            }

            if (product != null)
            {
                _helper.ShowItemInfoWindow(product);
            }
        }

        public void InitializeWindow(WindowArgs args)
        {
            if (args.Count >= 2)
            {
                _faction = args.Get<Faction>(0);
                _level = args.Get<int>(1);
            }
            else
            {
                var star = _motherShip.CurrentStar;
                _faction = star.Region.Faction;
                _level = Mathf.Max(5, star.Level);
            }

            EnsureFreeStarsFaction();
            EnsureUiReferences();
            EnsureNavigationContainers();
            EnsureSortButton();
            EnsureButtonsBar();

            var color = _faction.Color;
            _factionText.text = _localization.GetString(_faction.Name);
            _factionIcon.color = _faction.Color;
            _techsIcon.color = color;
            _levelText.text = _level.ToString();

            // Load workshop technologies
            var techs = _technologies.All.ForWorkshop(_faction).Where(_research.IsTechResearched).ToList();

            _categorizedTechs = techs
                .GroupBy(t => t.GetCategory())
                .ToDictionary(g => g.Key, g => g.ToList());

            UpdateResources();
            AdjustScrollbarBounds();
            ShowCategoriesView();
        }

        private void EnsureFreeStarsFaction()
        {
            if (_freeStarsFaction != null) return;

            if (_starMap != null)
            {
                try
                {
                    var homeStar = _starMap.GetStarById(0);
                    if (homeStar.Region != null)
                        _freeStarsFaction = homeStar.Region.Faction;
                }
                catch { }
            }

            if (_freeStarsFaction == null)
            {
                _freeStarsFaction = Faction.Empty;
            }
        }

        private void UpdateResources()
        {
            if (!gameObject.activeSelf) return;

            _creditsText.text = _playerResources.Money.ToString();
            _starsText.text = _playerResources.Stars.ToString();

            int factionPoints = _research.GetAvailablePoints(_faction);
            _techsText.text = factionPoints.ToString();

            EnsureFreeStarsFaction();

            bool isAlienBase = _faction != null &&
                               _faction != Faction.Empty &&
                               !_faction.Equals(_freeStarsFaction) &&
                               !_faction.Id.Equals(_freeStarsFaction.Id);

            if (_freeTechsPanel != null)
            {
                if (isAlienBase)
                {
                    int freePoints = Math.Max(
                        _research.GetAvailablePoints(_freeStarsFaction),
                        _research.GetAvailablePoints(Faction.Empty)
                    );

                    _freeTechsPanel.SetActive(true);

                    if (_freeTechsText != null)
                        _freeTechsText.text = freePoints.ToString();

                    if (_freeTechsIcon != null)
                        _freeTechsIcon.color = Color.white;
                }
                else
                {
                    _freeTechsPanel.SetActive(false);
                }
            }
        }

        private void UpdateTechnology(ViewModel.CraftListItem item, ITechnology tech)
        {
            item.InitializeForCraft(tech, _level);

            var texts = item.GetComponentsInChildren<Text>(true);
            if (texts.Length > 1 && texts[1] != null)
            {
                texts[1].verticalOverflow = VerticalWrapMode.Overflow;

                string factionLabel = _localization.GetString("$Faction");
                if (string.IsNullOrEmpty(factionLabel) || factionLabel.StartsWith("$"))
                    factionLabel = "$Faction";

                string factionName = TechCategoryHelper.GetLocalizedFactionName(tech.Faction, _localization);
                string factionLine = $"{factionLabel}: {factionName}".ToUpper();

                string typeLine;
                if (tech is ShipTechnology shipTech)
                {
                    string shipLabel = _localization.GetString("$Ship");
                    string shipClass = TechCategoryHelper.GetShipClassName(shipTech.Ship, _localization);
                    typeLine = !string.IsNullOrEmpty(shipClass) ? $"{shipLabel} ({shipClass})" : shipLabel;
                }
                else
                {
                    typeLine = tech.GetDescription(_localization);
                }

                texts[1].text = $"{factionLine}\n{typeLine}".ToUpper();
            }
        }

        private void ShowCategoriesView()
        {
            _currentCategory = null;
            _selectedTech = null;
            UpdateTechPanel();

            if (_sortingButton != null) _sortingButton.SetActive(false);
            if (_buttonsBar != null) _buttonsBar.SetActive(false);
            if (_technologyList != null) _technologyList.gameObject.SetActive(false);

            if (_categoryContent != null)
            {
                _categoryContent.gameObject.SetActive(true);
                if (_scrollRect != null)
                {
                    _scrollRect.content = _categoryContent;
                    _scrollRect.verticalNormalizedPosition = 1f;
                }

                BuildCategoryButtons();
            }

            RebuildParentLayout();
        }

        private void ShowCategoryItemsView(TechCategory category)
        {
            _currentCategory = category;
            _selectedTech = null;
            UpdateTechPanel();

            if (_categoryContent != null) _categoryContent.gameObject.SetActive(false);

            if (_sortingButton != null)
            {
                _sortingButton.SetActive(true);
                UpdateSortButtonDisplay();
            }

            if (_buttonsBar != null) _buttonsBar.SetActive(true);

            if (_backButton != null)
            {
                _backButton.gameObject.SetActive(true);
                UpdateBackButtonDisplay(category);
            }

            if (_infoButton != null)
            {
                _infoButton.gameObject.SetActive(false);
            }

            if (_technologyList != null)
            {
                _technologyList.gameObject.SetActive(true);
                if (_scrollRect != null)
                {
                    _scrollRect.content = _itemsContent;
                    _scrollRect.verticalNormalizedPosition = 1f;
                }

                var list = _categorizedTechs.ContainsKey(category) ? _categorizedTechs[category] : new List<ITechnology>();
                var sortedList = TechSorter.Sort(list, _currentSortMode, _localization);

                _technologyList.transform.InitializeElements<ViewModel.CraftListItem, ITechnology>(
                    sortedList, UpdateTechnology, _gameObjectFactory);

                _techGroup.SetAllTogglesOff();
            }

            RebuildParentLayout();
        }

        private void BuildCategoryButtons()
        {
            if (_categoryContent == null || _categoryTemplate == null) return;

            var vlg = _categoryContent.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) vlg.spacing = _categorySpacing;

            foreach (Transform child in _categoryContent)
                Destroy(child.gameObject);

            foreach (TechCategory category in Enum.GetValues(typeof(TechCategory)))
            {
                if (!_categorizedTechs.ContainsKey(category) || _categorizedTechs[category].Count == 0)
                    continue;

                int count = _categorizedTechs[category].Count;
                var catCard = Instantiate(_categoryTemplate, _categoryContent);
                catCard.name = $"Category_{category}";
                catCard.SetActive(true);

                var toggle = catCard.GetComponent<Toggle>();
                if (toggle != null)
                {
                    toggle.group = null;
                    DestroyImmediate(toggle);
                }

                var focus = catCard.transform.Find("Focus");
                if (focus != null) focus.gameObject.SetActive(false);

                var btn = catCard.GetComponent<Button>() ?? catCard.AddComponent<Button>();
                var targetCat = category;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ShowCategoryItemsView(targetCat));

                var texts = catCard.GetComponentsInChildren<Text>(true);
                if (texts.Length > 0 && texts[0] != null)
                    texts[0].text = category.GetTitle(_localization);
                if (texts.Length > 1 && texts[1] != null)
                    texts[1].text = _localization.GetString("$AvailableBlueprints", count);

                Image icon = catCard.transform.Find("Left/Icon")?.GetComponent<Image>()
                          ?? catCard.transform.Find("Left/Image")?.GetComponent<Image>()
                          ?? catCard.transform.Find("Icon")?.GetComponent<Image>()
                          ?? catCard.GetComponentInChildren<Image>();

                if (icon != null)
                {
                    var sprite = GetCategorySprite(category);
                    if (sprite != null)
                    {
                        icon.sprite = sprite;
                        icon.overrideSprite = sprite;
                        icon.preserveAspect = true;
                        icon.gameObject.SetActive(true);
                    }
                    else
                    {
                        Debug.LogWarning($"[CraftDialog] Sprite for category '{category}' is not assigned in inspector.");
                    }
                }
            }
        }

        private Sprite GetCategorySprite(TechCategory category)
        {
            switch (category)
            {
                case TechCategory.Weapon: return _weaponCategoryIcon;
                case TechCategory.Armor: return _armorCategoryIcon;
                case TechCategory.Energy: return _energyCategoryIcon;
                case TechCategory.Engine: return _engineCategoryIcon;
                case TechCategory.Drone: return _droneCategoryIcon;
                case TechCategory.Ship: return _shipCategoryIcon;
                case TechCategory.Satellite: return _satelliteCategoryIcon;
                default: return _otherCategoryIcon;
            }
        }

        private void CycleSortMode()
        {
            _currentSortMode = (SortMode)(((int)_currentSortMode + 1) % 5);
            UpdateSortButtonDisplay();

            if (_currentCategory.HasValue)
            {
                ShowCategoryItemsView(_currentCategory.Value);
            }
        }

        private void UpdateSortButtonDisplay()
        {
            if (_sortTitleText != null)
                _sortTitleText.text = _localization.GetString("$Sorting");

            if (_sortModeText != null)
                _sortModeText.text = TechSorter.GetModeTitle(_currentSortMode, _localization);
        }

        private void EnsureUiReferences()
        {
            _itemsContent = _technologyList.GetComponent<RectTransform>();
            _scrollRect = _technologyList.GetComponentInParent<ScrollRect>();

            var techList = _scrollRect != null ? _scrollRect.transform.parent : transform.Find("Content/TechList");

            if (_categoryTemplate == null && _itemsContent != null)
            {
                var catTr = _itemsContent.Find("Category");
                if (catTr != null) _categoryTemplate = catTr.gameObject;
            }

            if (_categoryTemplate != null) _categoryTemplate.SetActive(false);

            if (_sortingButton == null && techList != null)
            {
                var sortTr = techList.Find("SortingButton");
                if (sortTr != null) _sortingButton = sortTr.gameObject;
            }

            if (_buttonsBar == null && techList != null)
            {
                var barTr = techList.Find("ButtonsBar");
                if (barTr != null) _buttonsBar = barTr.gameObject;
            }

            if (_buttonsBar != null)
            {
                if (_backButton == null) _backButton = _buttonsBar.transform.Find("BackButton")?.GetComponent<Button>();
                if (_infoButton == null) _infoButton = _buttonsBar.transform.Find("InfoButton")?.GetComponent<Button>();
            }

            if (_freeTechsPanel == null)
            {
                var freeTr = transform.Find("Header/FreeTechPanel") ??
                             transform.Find("Header/FreeTechsPanel") ??
                             transform.Find("Header/FreePanel");
                if (freeTr != null) _freeTechsPanel = freeTr.gameObject;
            }

            if (_freeTechsPanel != null)
            {
                if (_freeTechsText == null) _freeTechsText = _freeTechsPanel.GetComponentInChildren<Text>();
                if (_freeTechsIcon == null) _freeTechsIcon = _freeTechsPanel.GetComponentInChildren<Image>();
            }
        }

        private void EnsureButtonsBar()
        {
            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(ShowCategoriesView);
            }

            if (_infoButton != null)
            {
                _infoButton.onClick.RemoveAllListeners();
                _infoButton.onClick.AddListener(InfoButtonClicked);
                _infoButton.gameObject.SetActive(false);
            }
        }

        private void EnsureSortButton()
        {
            if (_sortingButton == null) return;

            _sortingButton.transform.SetSiblingIndex(0);

            if (_sortTitleText == null)
            {
                var left = _sortingButton.transform.Find("Left/Name") ?? _sortingButton.transform.Find("Left");
                if (left != null) _sortTitleText = left.GetComponentInChildren<Text>();
            }

            if (_sortModeText == null)
            {
                var right = _sortingButton.transform.Find("Right/Name") ?? _sortingButton.transform.Find("Right");
                if (right != null) _sortModeText = right.GetComponentInChildren<Text>();
            }

            var toggle = _sortingButton.GetComponent<Toggle>();
            if (toggle != null)
            {
                toggle.group = null;
                DestroyImmediate(toggle);
            }

            var btn = _sortingButton.GetComponent<Button>() ?? _sortingButton.AddComponent<Button>();
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(CycleSortMode);

            UpdateSortButtonDisplay();
            _sortingButton.SetActive(false);
        }

        private void EnsureNavigationContainers()
        {
            if (_scrollRect == null || _itemsContent == null) return;

            if (_scrollRect.transform.parent != null)
            {
                var listLayout = _scrollRect.transform.parent.GetComponent<VerticalLayoutGroup>();
                if (listLayout != null)
                {
                    listLayout.childControlHeight = true;
                    listLayout.childForceExpandHeight = false;
                }
            }

            if (_categoryContent == null)
            {
                var existing = _scrollRect.viewport != null
                    ? _scrollRect.viewport.Find("CategoryContent")
                    : _scrollRect.transform.Find("CategoryContent");

                if (existing != null)
                {
                    _categoryContent = existing.GetComponent<RectTransform>();
                }
                else
                {
                    var catGo = new GameObject("CategoryContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                    catGo.transform.SetParent(_scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.transform, false);
                    _categoryContent = catGo.GetComponent<RectTransform>();
                    _categoryContent.anchorMin = _itemsContent.anchorMin;
                    _categoryContent.anchorMax = _itemsContent.anchorMax;
                    _categoryContent.pivot = _itemsContent.pivot;
                    _categoryContent.sizeDelta = _itemsContent.sizeDelta;
                }
            }

            var vlg = _categoryContent.GetComponent<VerticalLayoutGroup>() ?? _categoryContent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = _categorySpacing;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;

            var csf = _categoryContent.GetComponent<ContentSizeFitter>() ?? _categoryContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollLe = _scrollRect.GetComponent<LayoutElement>() ?? _scrollRect.gameObject.AddComponent<LayoutElement>();
            scrollLe.flexibleHeight = 1f;
        }

        private void UpdateBackButtonDisplay(TechCategory category)
        {
            if (_backButton == null) return;

            var nameText = _backButton.transform.Find("Right/Name")?.GetComponent<Text>();
            var descText = _backButton.transform.Find("Right/Description")?.GetComponent<Text>();

            if (nameText != null) nameText.text = _localization.GetString("$BackToCategories");
            if (descText != null) descText.text = category.GetTitle(_localization);
        }

        private void AdjustScrollbarBounds()
        {
            if (_scrollRect == null) return;

            var scrollbar = _scrollRect.verticalScrollbar;
            if (scrollbar == null)
            {
                scrollbar = _scrollRect.GetComponentInChildren<Scrollbar>(true) ??
                            _scrollRect.transform.parent.GetComponentInChildren<Scrollbar>(true);
            }

            if (scrollbar != null)
            {
                var scrollbarRect = scrollbar.GetComponent<RectTransform>();
                if (scrollbarRect != null)
                {
                    scrollbarRect.anchorMin = new Vector2(scrollbarRect.anchorMin.x, 0f);
                    scrollbarRect.anchorMax = new Vector2(scrollbarRect.anchorMax.x, 1f);
                    scrollbarRect.offsetMin = new Vector2(scrollbarRect.offsetMin.x, 6f);
                    scrollbarRect.offsetMax = new Vector2(scrollbarRect.offsetMax.x, -6f);
                }
            }
        }

        private void RebuildParentLayout()
        {
            if (_scrollRect != null && _scrollRect.transform.parent != null)
            {
                var parentRect = _scrollRect.transform.parent.GetComponent<RectTransform>();
                if (parentRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
            }
        }

        private void UpdateTechPanel()
        {
            if (_infoButton != null)
            {
                _infoButton.gameObject.SetActive(_selectedTech != null);
            }

            if (_selectedTech == null)
            {
                _commonCraftPanel.Cleanup();
                _improvedCraftPanel.Cleanup();
                _superiorCraftPanel.Cleanup();
                return;
            }

            _commonCraftPanel.Initialize(_selectedTech, _level);
            _improvedCraftPanel.Initialize(_selectedTech, _level);
            _superiorCraftPanel.Initialize(_selectedTech, _level);
        }

        private Faction _faction;
        private ObscuredInt _level;
        private ITechnology _selectedTech;
    }
}