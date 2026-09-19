using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CommonComponents;
using DataModel.Technology;
using Economy.ItemType;
using Economy.Products;
using Galaxy;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
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
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Gui.Craft
{
    public class CraftDialog : MonoBehaviour
    {
        public enum TechCategory
        {
            Weapon = 0,
            Armor = 1,
            Energy = 2,
            Engine = 3,
            Drone = 4,
            Ship = 5,
            Satellite = 6,
            Other = 7
        }

        [Inject] private readonly ISoundPlayer _soundPlayer;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly IRandom _random;
        [Inject] private readonly ItemTypeFactory _factory;
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

        [Header("Header Info Button")]
        [SerializeField] private Button _infoButton;

        [Header("Free Stars Tech Panel")]
        [SerializeField] private GameObject _freeTechsPanel;
        [SerializeField] private Text _freeTechsText;
        [SerializeField] private Image _freeTechsIcon;

        [Header("Category Icons (Manual Assignment)")]
        [SerializeField] private Sprite _weaponCategoryIcon;
        [SerializeField] private Sprite _armorCategoryIcon;
        [SerializeField] private Sprite _energyCategoryIcon;
        [SerializeField] private Sprite _engineCategoryIcon;
        [SerializeField] private Sprite _droneCategoryIcon;
        [SerializeField] private Sprite _shipCategoryIcon;
        [SerializeField] private Sprite _satelliteCategoryIcon;
        [SerializeField] private Sprite _otherCategoryIcon;

        [Header("Back Button Icon")]
        [SerializeField] private Sprite _backButtonIcon;

        private ScrollRect _scrollRect;
        private RectTransform _itemsContent;
        private RectTransform _categoryContent;
        private GameObject _backButton;
        private float _singleItemHeight = 72f;
        private Dictionary<TechCategory, List<ITechnology>> _categorizedTechs = new Dictionary<TechCategory, List<ITechnology>>();
        private TechCategory? _currentCategory = null;

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
            if (_selectedTech == null)
                return;

            _soundPlayer.Play(_buySound);

            var product = ExtractProductFromObject(_commonCraftPanel) ??
                          ExtractProductFromObject(_improvedCraftPanel);

            if (product == null)
            {
                var itemType = ResolveActualItemType(_selectedTech);
                if (itemType != null)
                {
                    product = CreateProduct(itemType);
                }
            }

            if (product != null)
            {
                _helper.ShowItemInfoWindow(product);
                return;
            }

            Debug.LogWarning("CraftDialog: Failed to resolve item stats for tech: " + _selectedTech);
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
            EnsureUiBindings();
            EnsureNavigationContainers();

            if (_infoButton != null)
            {
                _infoButton.onClick.RemoveAllListeners();
                _infoButton.onClick.AddListener(InfoButtonClicked);
            }

            var color = _faction.Color;
            _factionText.text = _localization.GetString(_faction.Name);
            _factionIcon.color = _faction.Color;

            _techsIcon.color = color;
            _levelText.text = _level.ToString();

            // Collect researched technologies (preserve DB order, keep Free Stars at bottom)
            IEnumerable<ITechnology> techs = _technologies.All.ForWorkshop(_faction).Where(_research.IsTechResearched);

            if (!IsFreeStarsBase() && _freeStarsFaction != null)
            {
                var freeTechs = _technologies.All.ForWorkshop(_freeStarsFaction).Where(_research.IsTechResearched);
                techs = techs.Concat(freeTechs).Distinct();
            }

            _categorizedTechs = techs
                .GroupBy(GetTechCategory)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(t => IsFreeStars(t.Faction) ? 1 : 0).ToList()
                );

            UpdateResources();
            ShowCategoriesView();
        }

        private void ShowCategoriesView()
        {
            _currentCategory = null;
            _selectedTech = null;
            UpdateTechPanel();

            if (_backButton != null)
                _backButton.SetActive(false);

            if (_technologyList != null)
                _technologyList.gameObject.SetActive(false);

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

            if (_categoryContent != null)
                _categoryContent.gameObject.SetActive(false);

            if (_backButton != null)
            {
                _backButton.SetActive(true);
                UpdateBackButtonDisplay(category);
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
                _technologyList.transform.InitializeElements<ViewModel.CraftListItem, ITechnology>(
                    list, UpdateTechnology, _gameObjectFactory);

                _techGroup.SetAllTogglesOff();
            }

            RebuildParentLayout();
            _soundPlayer.Play(_buySound);
        }

        private void BuildCategoryButtons()
        {
            if (_categoryContent == null)
                return;

            foreach (Transform child in _categoryContent)
                Destroy(child.gameObject);

            var template = _itemsContent != null && _itemsContent.childCount > 0 ? _itemsContent.GetChild(0).gameObject : null;
            if (template == null)
                return;

            foreach (TechCategory category in Enum.GetValues(typeof(TechCategory)))
            {
                if (!_categorizedTechs.ContainsKey(category) || _categorizedTechs[category].Count == 0)
                    continue;

                int count = _categorizedTechs[category].Count;
                var catCard = Instantiate(template, _categoryContent);
                catCard.name = $"Category_{category}";
                catCard.SetActive(true);

                // Remove Focus highlight object completely so it never activates on categories
                var focus = catCard.transform.Find("Focus");
                if (focus != null)
                    DestroyImmediate(focus.gameObject);

                var vm = catCard.GetComponent<ViewModel.CraftListItem>();
                if (vm != null) DestroyImmediate(vm);

                var toggle = catCard.GetComponent<Toggle>();
                if (toggle != null) DestroyImmediate(toggle);

                var btn = catCard.GetComponent<Button>() ?? catCard.AddComponent<Button>();
                var targetCat = category;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => ShowCategoryItemsView(targetCat));

                var texts = catCard.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < texts.Length; i++)
                {
                    if (i == 0)
                    {
                        texts[i].text = GetCategoryTitle(category);
                        texts[i].gameObject.SetActive(true);
                    }
                    else if (i == 1)
                    {
                        texts[i].text = _localization.GetString("$AvailableBlueprints", count);
                        texts[i].gameObject.SetActive(true);
                    }
                    else
                    {
                        texts[i].text = string.Empty;
                        texts[i].gameObject.SetActive(false);
                    }
                }

                var icon = GetItemIcon(catCard);
                if (icon != null)
                {
                    ApplyThemedIcon(icon, GetCategorySprite(category));
                }
            }
        }

        private void EnsureNavigationContainers()
        {
            if (_categoryContent != null)
                return;

            _itemsContent = _technologyList.GetComponent<RectTransform>();
            _scrollRect = _technologyList.GetComponentInParent<ScrollRect>();

            if (_scrollRect == null || _itemsContent == null)
                return;

            if (_scrollRect.transform.parent != null)
            {
                var listLayout = _scrollRect.transform.parent.GetComponent<VerticalLayoutGroup>();
                if (listLayout != null)
                {
                    listLayout.childControlHeight = true;
                    listLayout.childForceExpandHeight = false;
                }
            }

            var template = _itemsContent.childCount > 0 ? _itemsContent.GetChild(0).gameObject : null;
            if (template != null)
            {
                var tr = template.GetComponent<RectTransform>();
                if (tr != null && tr.rect.height > 10)
                    _singleItemHeight = tr.rect.height;

                var tle = template.GetComponent<LayoutElement>();
                if (tle != null && tle.preferredHeight > 10)
                    _singleItemHeight = tle.preferredHeight;
            }

            var catGo = new GameObject("CategoryContent", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            catGo.transform.SetParent(_scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.transform, false);
            _categoryContent = catGo.GetComponent<RectTransform>();
            _categoryContent.anchorMin = _itemsContent.anchorMin;
            _categoryContent.anchorMax = _itemsContent.anchorMax;
            _categoryContent.pivot = _itemsContent.pivot;
            _categoryContent.sizeDelta = _itemsContent.sizeDelta;

            var vlg = catGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 8;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = catGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (template != null && _scrollRect.transform.parent != null)
            {
                _backButton = Instantiate(template, _scrollRect.transform.parent);
                _backButton.name = "BackButton";
                _backButton.transform.SetSiblingIndex(_scrollRect.transform.GetSiblingIndex());

                var backFocus = _backButton.transform.Find("Focus");
                if (backFocus != null)
                    DestroyImmediate(backFocus.gameObject);

                var vm = _backButton.GetComponent<ViewModel.CraftListItem>();
                if (vm != null) DestroyImmediate(vm);

                var toggle = _backButton.GetComponent<Toggle>();
                if (toggle != null) DestroyImmediate(toggle);

                var btn = _backButton.GetComponent<Button>() ?? _backButton.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() =>
                {
                    _soundPlayer.Play(_buySound);
                    ShowCategoriesView();
                });

                var le = _backButton.GetComponent<LayoutElement>() ?? _backButton.AddComponent<LayoutElement>();
                le.minHeight = _singleItemHeight;
                le.preferredHeight = _singleItemHeight;
                le.flexibleHeight = 0f;
                le.flexibleWidth = 1f;

                var backRect = _backButton.GetComponent<RectTransform>();
                backRect.sizeDelta = new Vector2(backRect.sizeDelta.x, _singleItemHeight);

                var scrollLe = _scrollRect.GetComponent<LayoutElement>() ?? _scrollRect.gameObject.AddComponent<LayoutElement>();
                scrollLe.flexibleHeight = 1f;

                _backButton.SetActive(false);
            }
        }

        private void UpdateBackButtonDisplay(TechCategory category)
        {
            if (_backButton == null) return;

            var backFocus = _backButton.transform.Find("Focus");
            if (backFocus != null)
                DestroyImmediate(backFocus.gameObject);

            var le = _backButton.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.minHeight = _singleItemHeight;
                le.preferredHeight = _singleItemHeight;
                le.flexibleHeight = 0f;
            }

            var backRect = _backButton.GetComponent<RectTransform>();
            if (backRect != null)
            {
                backRect.sizeDelta = new Vector2(backRect.sizeDelta.x, _singleItemHeight);
            }

            var texts = _backButton.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (i == 0)
                {
                    texts[i].text = _localization.GetString("$BackToCategories");
                    texts[i].gameObject.SetActive(true);
                }
                else if (i == 1)
                {
                    texts[i].text = _localization.GetString("$CurrentCategoryLabel", GetCategoryTitle(category));
                    texts[i].gameObject.SetActive(true);
                }
                else
                {
                    texts[i].text = string.Empty;
                    texts[i].gameObject.SetActive(false);
                }
            }

            var icon = GetItemIcon(_backButton);
            if (icon != null)
            {
                ApplyThemedIcon(icon, _backButtonIcon);
            }
        }

        private void ApplyThemedIcon(Image icon, Sprite customSprite)
        {
            if (icon == null) return;

            if (customSprite != null)
                icon.sprite = customSprite;

            icon.preserveAspect = true;

            var themedImage = icon.GetComponent("ThemedImage");
            if (themedImage != null)
            {
                var themeProp = themedImage.GetType().GetProperty("ThemeColor") ??
                                themedImage.GetType().GetProperty("themeColor");
                if (themeProp != null && themeProp.CanWrite)
                {
                    try
                    {
                        var enumType = themeProp.PropertyType;
                        var val = Enum.Parse(enumType, "ButtonIcon");
                        themeProp.SetValue(themedImage, val, null);
                    }
                    catch { }
                }
            }

            icon.color = new Color(0.35f, 0.95f, 1f, 1f);
            icon.gameObject.SetActive(true);
        }

        private void RebuildParentLayout()
        {
            if (_scrollRect != null && _scrollRect.transform.parent != null)
            {
                var parentRect = _scrollRect.transform.parent.GetComponent<RectTransform>();
                if (parentRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(parentRect);
                }
            }
        }

        private Image GetItemIcon(GameObject itemObj)
        {
            if (itemObj == null) return null;

            var iconTransform = itemObj.transform.Find("Icon") ?? itemObj.transform.Find("Image");
            if (iconTransform != null)
            {
                var img = iconTransform.GetComponent<Image>();
                if (img != null) return img;
            }

            var images = itemObj.GetComponentsInChildren<Image>(true);
            if (images.Length > 1)
                return images[1];

            return images.FirstOrDefault();
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

        private string GetCategoryTitle(TechCategory category)
        {
            switch (category)
            {
                case TechCategory.Weapon:
                    return _localization.GetString("$GroupWeapon");
                case TechCategory.Armor:
                    return _localization.GetString("$GroupArmor");
                case TechCategory.Energy:
                    return _localization.GetString("$GroupEnergy");
                case TechCategory.Engine:
                    return _localization.GetString("$GroupEngines");
                case TechCategory.Drone:
                    return _localization.GetString("$GroupDrones");
                case TechCategory.Ship:
                    return _localization.GetString("$GroupShips");
                case TechCategory.Satellite:
                    return _localization.GetString("$GroupSatellites");
                default:
                    return _localization.GetString("$MenuSpecial");
            }
        }

        private void UpdateTechnology(ViewModel.CraftListItem item, ITechnology tech)
        {
            item.InitializeForCraft(tech, _level);

            // Display ship class strictly on a new line
            if (_currentCategory == TechCategory.Ship || GetTechCategory(tech) == TechCategory.Ship)
            {
                string shipClassName = GetShipClassName(tech);
                if (!string.IsNullOrEmpty(shipClassName))
                {
                    var texts = item.GetComponentsInChildren<Text>(true);
                    if (texts.Length > 1 && texts[1] != null)
                    {
                        texts[1].verticalOverflow = VerticalWrapMode.Overflow;
                        string shipLabel = _localization.GetString("$Ship");
                        if (string.IsNullOrEmpty(shipLabel) || shipLabel.StartsWith("$"))
                            shipLabel = "КОРАБЛЬ";

                        texts[1].text = $"{shipLabel}\n{shipClassName.ToUpper()}";
                    }
                }
            }
        }

        // Resolves localized ship class name
        private string GetShipClassName(ITechnology tech)
        {
            if (tech == null)
                return string.Empty;

            try
            {
                var shipObj = GetMemberValue(tech, "Ship") ?? GetMemberValue(tech, "ShipBuild");
                if (shipObj != null)
                {
                    var sizeClassVal = GetMemberValue(shipObj, "SizeClass") ??
                                       GetMemberValue(shipObj, "ShipCategory") ??
                                       GetMemberValue(shipObj, "Class");

                    if (sizeClassVal != null)
                    {
                        string rawStr = sizeClassVal.ToString(); 
                        string classKey = rawStr.StartsWith("Class", StringComparison.OrdinalIgnoreCase)
                            ? "$" + rawStr
                            : "$Class" + rawStr;

                        string loc = _localization.GetString(classKey);
                        if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("$"))
                            return loc;

                        // Fallback without Class prefix
                        loc = _localization.GetString("$" + rawStr);
                        if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("$"))
                            return loc;

                        return rawStr;
                    }
                }
            }
            catch { }

            return string.Empty;
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

        private IItemType ResolveActualItemType(ITechnology tech)
        {
            if (tech == null)
                return null;

            try
            {
                var compObj = GetMemberValue(tech, "Component");
                if (compObj != null)
                {
                    var compInfoType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType("Constructor.ComponentInfo"))
                        .FirstOrDefault(t => t != null);

                    if (compInfoType != null)
                    {
                        object compInfo = null;
                        foreach (var ctor in compInfoType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            var pars = ctor.GetParameters();
                            if (pars.Length >= 1 && pars[0].ParameterType.IsAssignableFrom(compObj.GetType()))
                            {
                                var args = new object[pars.Length];
                                args[0] = compObj;
                                for (int i = 1; i < pars.Length; i++)
                                {
                                    args[i] = pars[i].HasDefaultValue ? pars[i].DefaultValue :
                                              pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
                                }
                                try { compInfo = ctor.Invoke(args); break; } catch { }
                            }
                        }

                        if (compInfo != null)
                        {
                            var createMethod = _factory.GetType().GetMethod("CreateComponentItem", new[] { compInfoType, typeof(bool) });
                            if (createMethod != null)
                            {
                                var item = createMethod.Invoke(_factory, new[] { compInfo, false }) as IItemType;
                                if (item != null)
                                    return item;
                            }
                        }
                    }
                }
            }
            catch { }

            try
            {
                var satObj = GetMemberValue(tech, "Satellite");
                if (satObj != null)
                {
                    var method = _factory.GetType().GetMethod("CreateSatelliteItem", new[] { satObj.GetType(), typeof(bool) });
                    if (method != null)
                    {
                        var item = method.Invoke(_factory, new[] { satObj, false }) as IItemType;
                        if (item != null)
                            return item;
                    }
                }
            }
            catch { }

            try
            {
                var shipObj = GetMemberValue(tech, "Ship") ?? GetMemberValue(tech, "ShipBuild");
                if (shipObj != null)
                {
                    foreach (var method in _factory.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (method.Name.Contains("ShipItem"))
                        {
                            var pars = method.GetParameters();
                            if (pars.Length >= 1 && pars[0].ParameterType.IsAssignableFrom(shipObj.GetType()))
                            {
                                var args = new object[pars.Length];
                                args[0] = shipObj;
                                for (int i = 1; i < pars.Length; i++)
                                {
                                    args[i] = pars[i].HasDefaultValue ? pars[i].DefaultValue :
                                              pars[i].ParameterType.IsValueType ? Activator.CreateInstance(pars[i].ParameterType) : null;
                                }
                                try
                                {
                                    var item = method.Invoke(_factory, args) as IItemType;
                                    if (item != null)
                                        return item;
                                }
                                catch { }
                            }
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private IProduct CreateProduct(IItemType itemType)
        {
            if (itemType == null)
                return null;

            try
            {
                var assembly = typeof(IProduct).Assembly;
                var productTypes = assembly.GetTypes()
                    .Where(t => typeof(IProduct).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .OrderBy(t => t.Name == "Product" ? 0 : 1);

                foreach (var pType in productTypes)
                {
                    foreach (var ctor in pType.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                    {
                        var pars = ctor.GetParameters();
                        if (pars.Length == 0)
                            continue;

                        if (pars[0].ParameterType.IsAssignableFrom(itemType.GetType()))
                        {
                            var args = new object[pars.Length];
                            args[0] = itemType;

                            for (int i = 1; i < pars.Length; i++)
                            {
                                if (pars[i].ParameterType == typeof(int))
                                    args[i] = 1;
                                else if (pars[i].HasDefaultValue)
                                    args[i] = pars[i].DefaultValue;
                                else if (pars[i].ParameterType.IsValueType)
                                    args[i] = Activator.CreateInstance(pars[i].ParameterType);
                                else
                                    args[i] = null;
                            }

                            var instance = ctor.Invoke(args) as IProduct;
                            if (instance != null)
                                return instance;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        private IProduct ExtractProductFromObject(object obj)
        {
            if (obj == null) return null;
            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

            foreach (var field in obj.GetType().GetFields(flags))
            {
                if (typeof(IProduct).IsAssignableFrom(field.FieldType))
                {
                    var p = field.GetValue(obj) as IProduct;
                    if (p != null) return p;
                }
            }

            foreach (var prop in obj.GetType().GetProperties(flags))
            {
                if (typeof(IProduct).IsAssignableFrom(prop.PropertyType) && prop.CanRead)
                {
                    try
                    {
                        var p = prop.GetValue(obj, null) as IProduct;
                        if (p != null) return p;
                    }
                    catch { }
                }
            }

            foreach (var field in obj.GetType().GetFields(flags))
            {
                var subVal = field.GetValue(obj);
                if (subVal != null && subVal != obj && !field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.FieldType != typeof(string))
                {
                    foreach (var subField in subVal.GetType().GetFields(flags))
                    {
                        if (typeof(IProduct).IsAssignableFrom(subField.FieldType))
                        {
                            var p = subField.GetValue(subVal) as IProduct;
                            if (p != null) return p;
                        }
                    }
                }
            }

            return null;
        }

        private void UpdateResources()
        {
            if (!gameObject.activeSelf)
                return;

            _creditsText.text = _playerResources.Money.ToString();
            _starsText.text = _playerResources.Stars.ToString();

            int factionPoints = _research.GetAvailablePoints(_faction);

            EnsureFreeStarsFaction();
            EnsureUiBindings();

            bool isAlienBase = !IsFreeStarsBase() && _freeStarsFaction != null;

            if (isAlienBase)
            {
                int freePoints = _research.GetAvailablePoints(_freeStarsFaction);

                if (_freeTechsPanel != null)
                {
                    _freeTechsPanel.SetActive(true);

                    if (_freeTechsText != null)
                        _freeTechsText.text = freePoints.ToString();

                    if (_freeTechsIcon != null)
                        _freeTechsIcon.color = _freeStarsFaction.Color;

                    _techsText.text = factionPoints.ToString();
                }
                else
                {
                    _techsText.text = $"{factionPoints} ({freePoints})";
                }
            }
            else
            {
                if (_freeTechsPanel != null)
                    _freeTechsPanel.SetActive(false);

                _techsText.text = factionPoints.ToString();
            }
        }

        private void EnsureUiBindings()
        {
            if (_freeTechsPanel == null)
                return;

            if (_freeTechsText == null)
                _freeTechsText = _freeTechsPanel.GetComponentInChildren<Text>();

            if (_freeTechsIcon == null)
                _freeTechsIcon = _freeTechsPanel.GetComponentInChildren<Image>();
        }

        private void EnsureFreeStarsFaction()
        {
            if (_freeStarsFaction != null)
                return;

            if (_starMap != null)
            {
                try
                {
                    var homeStar = _starMap.GetStarById(0);
                    _freeStarsFaction = homeStar.Region.Faction;
                }
                catch { }
            }

            if (_freeStarsFaction == null && _technologies != null)
            {
                foreach (var tech in _technologies.All)
                {
                    var f = tech.Faction;
                    if (f != null && IsFreeStars(f))
                    {
                        _freeStarsFaction = f;
                        break;
                    }
                }
            }
        }

        private TechCategory GetTechCategory(ITechnology tech)
        {
            if (tech == null)
                return TechCategory.Other;

            try
            {
                var compObj = GetMemberValue(tech, "Component");
                if (compObj != null)
                {
                    var dataObj = GetMemberValue(compObj, "Data") ?? compObj;
                    var catVal = GetMemberValue(dataObj, "DisplayCategory");
                    if (catVal != null)
                    {
                        var category = ParseCategoryString(catVal.ToString());
                        if (category != TechCategory.Other)
                            return category;
                    }

                    var nameObj = GetMemberValue(dataObj, "Name") ?? GetMemberValue(compObj, "Name");
                    if (nameObj != null)
                    {
                        var localized = _localization.GetString(nameObj.ToString());
                        var catByName = ParseCategoryByLocalizedText(localized);
                        if (catByName != TechCategory.Other)
                            return catByName;
                    }
                }

                var shipObj = GetMemberValue(tech, "Ship") ?? GetMemberValue(tech, "ShipBuild");
                if (shipObj != null)
                    return TechCategory.Ship;

                var satObj = GetMemberValue(tech, "Satellite");
                if (satObj != null)
                    return TechCategory.Satellite;

                var typeName = tech.GetType().Name;
                if (typeName.IndexOf("Ship", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TechCategory.Ship;

                if (typeName.IndexOf("Satellite", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TechCategory.Satellite;
            }
            catch { }

            return TechCategory.Other;
        }

        private TechCategory ParseCategoryString(string catName)
        {
            if (string.IsNullOrEmpty(catName))
                return TechCategory.Other;

            if (catName.IndexOf("Drone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Hangar", StringComparison.OrdinalIgnoreCase) >= 0)
                return TechCategory.Drone;

            if (catName.IndexOf("Weapon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Cannon", StringComparison.OrdinalIgnoreCase) >= 0)
                return TechCategory.Weapon;

            if (catName.IndexOf("Armor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Defense", StringComparison.OrdinalIgnoreCase) >= 0)
                return TechCategory.Armor;

            if (catName.IndexOf("Energy", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Reactor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Battery", StringComparison.OrdinalIgnoreCase) >= 0)
                return TechCategory.Energy;

            if (catName.IndexOf("Engine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                catName.IndexOf("Thruster", StringComparison.OrdinalIgnoreCase) >= 0)
                return TechCategory.Engine;

            return TechCategory.Other;
        }

        private bool ContainsLocalized(string text, params string[] keys)
        {
            foreach (var key in keys)
            {
                var localized = _localization.GetString(key);
                if (!string.IsNullOrEmpty(localized) && text.IndexOf(localized, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private TechCategory ParseCategoryByLocalizedText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return TechCategory.Other;
            if (ContainsLocalized(text, "$DroneBay", "$GroupDrones", "$ClassDrone"))
                return TechCategory.Drone;
            if (ContainsLocalized(text, "$Weapon", "$GroupWeapon", "$WeaponType"))
                return TechCategory.Weapon;
            if (ContainsLocalized(text, "$Armor", "$Shield", "$GroupArmor", "$MenuDefense"))
                return TechCategory.Armor;
            if (ContainsLocalized(text, "$Reactor", "$Energy", "$GroupEnergy", "$MenuEnergy"))
                return TechCategory.Energy;
            if (ContainsLocalized(text, "$Engine", "$GroupEngines"))
                return TechCategory.Engine;
            if (ContainsLocalized(text, "$Ship", "$GroupShips"))
                return TechCategory.Ship;
            if (ContainsLocalized(text, "$Satellite", "$GroupSatellites"))
                return TechCategory.Satellite;

            return TechCategory.Other;
        }

        private object GetMemberValue(object obj, string name)
        {
            if (obj == null || string.IsNullOrEmpty(name))
                return null;

            var type = obj.GetType();
            var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop != null)
                return prop.GetValue(obj, null);

            var field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null)
                return field.GetValue(obj);

            return null;
        }

        private bool IsFreeStars(Faction faction)
        {
            if (faction == null)
                return false;

            var idStr = faction.Id.ToString();
            return idStr == "0" ||
                   idStr.Equals("Empty", StringComparison.OrdinalIgnoreCase) ||
                   faction.Name == "$FreeStars" ||
                   faction.Name.IndexOf("Free", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsFreeStarsBase()
        {
            if (_faction == null)
                return true;

            if (IsFreeStars(_faction))
                return true;

            if (_freeStarsFaction != null)
                return _faction.Equals(_freeStarsFaction) || _faction.Id.Equals(_freeStarsFaction.Id);

            return false;
        }

        private Faction _faction;
        private Faction _freeStarsFaction;
        private ObscuredInt _level;
        private ITechnology _selectedTech;
    }
}