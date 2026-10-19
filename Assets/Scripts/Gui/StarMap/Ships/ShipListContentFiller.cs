using System;
using System.Collections.Generic;
using System.Linq;
using Constructor.Ships;
using GameDatabase.Enums;
using Services.Localization;
using Services.Resources;
using ShipEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Gui.StarMap
{
    public class ShipListContentFiller : MonoBehaviour, IContentFiller, IShipCategoryHandler
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IResourceLocator _resourceLocator;

        [SerializeField] private ShipListItem ItemPrefab;
        [SerializeField] private ShipGroupListItem GroupPrefab;

        [Header("Category Icons")]
        [SerializeField] private List<CategoryIcon> _categoryIcons = new List<CategoryIcon>();

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;
        [SerializeField] private Button _searchButton;

        private readonly List<CategoryData> _rootCategories = new List<CategoryData>();
        private readonly List<DisplayItem> _displayItems = new List<DisplayItem>();
        private readonly Dictionary<IShip, bool> _shipAvailability = new Dictionary<IShip, bool>();

        private CategoryData _activeCategory = null;
        private string _searchQuery = "";

        public event Action OnListChanged;

        public IShip SelectedShip { get; set; }

        public int SelectedShipIndex => _displayItems.FindIndex(item => !item.IsCategory && item.Ship == SelectedShip);

        private void Start()
        {
            // Bind search button click
            if (_searchButton != null)
                _searchButton.onClick.AddListener(ExecuteSearch);

            // Also allow executing search by pressing Enter
            if (_searchInput != null)
                _searchInput.onEndEdit.AddListener(_ => ExecuteSearch());
        }

        public void InitializeShips(IEnumerable<KeyValuePair<IShip, bool>> ships)
        {
            if (ItemPrefab) ItemPrefab.gameObject.SetActive(false);
            if (GroupPrefab) GroupPrefab.gameObject.SetActive(false);

            if (!GroupPrefab)
                Debug.LogWarning("ShipListContentFiller: GroupPrefab is not assigned, ships are listed without folders", this);

            _shipAvailability.Clear();
            var shipList = new List<IShip>();

            foreach (var pair in ships)
            {
                _shipAvailability[pair.Key] = pair.Value;
                shipList.Add(pair.Key);
            }

            // Capture current navigation state before rebuilding
            SizeClass? activeSize = null;
            string activeSubKey = null;

            if (_activeCategory != null)
            {
                var sampleShip = _activeCategory.Ships.FirstOrDefault() ??
                                 _activeCategory.SubCategories.SelectMany(sc => sc.Ships).FirstOrDefault();

                if (sampleShip != null)
                {
                    activeSize = sampleShip.Model.SizeClass;
                    if (_activeCategory.Parent != null)
                    {
                        string faction = sampleShip.Model.Faction != null ? sampleShip.Model.Faction.Name : "NoFaction";
                        activeSubKey = sampleShip.Model.Id.ToString() + "_" + faction;
                    }
                }
            }

            BuildCategories(shipList);

            // Re-link active category to the newly created instance
            _activeCategory = null;
            if (activeSize.HasValue)
            {
                var newRootCat = _rootCategories.FirstOrDefault(c =>
                    c.Ships.Any(s => s.Model.SizeClass == activeSize.Value) ||
                    c.SubCategories.Any(sc => sc.Ships.Any(s => s.Model.SizeClass == activeSize.Value)));

                if (newRootCat != null)
                {
                    if (activeSubKey != null)
                    {
                        _activeCategory = newRootCat.SubCategories.FirstOrDefault(sc =>
                            sc.Ships.Any(s => (s.Model.Id.ToString() + "_" + (s.Model.Faction != null ? s.Model.Faction.Name : "NoFaction")) == activeSubKey))
                            ?? newRootCat;
                    }
                    else
                    {
                        _activeCategory = newRootCat;
                    }
                }
            }

            RebuildDisplayList();
        }

        public void InitializeShips(IEnumerable<IShip> ships)
        {
            InitializeShips(ships.Select(s => new KeyValuePair<IShip, bool>(s, true)));
        }

        private void BuildCategories(List<IShip> ships)
        {
            _rootCategories.Clear();

            var groupedBySize = ships
                .Where(s => s.Model.SizeClass != SizeClass.Undefined)
                .GroupBy(s => s.Model.SizeClass)
                .OrderBy(g => g.Key);

            foreach (var sizeGroup in groupedBySize)
            {
                string sizeName = _localization.GetString($"$Class{sizeGroup.Key}");
                if (string.IsNullOrEmpty(sizeName) || sizeName.StartsWith("$"))
                    sizeName = sizeGroup.Key.ToString();
                else if (sizeName.Length > 0)
                    sizeName = char.ToUpper(sizeName[0]) + sizeName.Substring(1);

                var sizeCategory = new CategoryData
                {
                    Name = $"{sizeName} ({sizeGroup.Count()})",
                    Icon = GetIconForSize(sizeGroup.Key),
                    Parent = null
                };

                var groupedByModelAndFaction = sizeGroup.GroupBy(s =>
                {
                    string factionName = s.Model.Faction != null ? s.Model.Faction.Name : "NoFaction";
                    return s.Model.Id.ToString() + "_" + factionName;
                });

                foreach (var modelGroup in groupedByModelAndFaction)
                {
                    if (modelGroup.Count() == 1)
                    {
                        sizeCategory.Ships.Add(modelGroup.First());
                    }
                    else
                    {
                        var firstShip = modelGroup.First();
                        Sprite shipIcon = _resourceLocator.GetSprite(firstShip.Model.IconImage) ??
                                          _resourceLocator.GetSprite(firstShip.Model.ModelImage);

                        string shipName = _localization.Localize(firstShip.Model.OriginalName);
                        string factionText = "";
                        if (firstShip.Model.Faction != null)
                        {
                            string factionLocalized = _localization.Localize(firstShip.Model.Faction.Name);
                            factionText = $" [{factionLocalized}]";
                        }

                        var subCategory = new CategoryData
                        {
                            Name = $"{shipName}{factionText} ({modelGroup.Count()})",
                            Icon = shipIcon,
                            Parent = sizeCategory,
                            Ships = modelGroup.ToList()
                        };
                        sizeCategory.SubCategories.Add(subCategory);
                    }
                }

                _rootCategories.Add(sizeCategory);
            }
        }

        private Sprite GetIconForSize(SizeClass sizeClass)
        {
            if (_categoryIcons == null || _categoryIcons.Count == 0)
                return null;

            var match = _categoryIcons.FirstOrDefault(i => i.SizeClass == sizeClass);
            return match.Icon;
        }

        public void OpenCategory(CategoryData category)
        {
            _activeCategory = category;
            RebuildDisplayList();
            OnListChanged?.Invoke();
        }

        public void GoBack()
        {
            if (_activeCategory != null)
                _activeCategory = _activeCategory.Parent;

            RebuildDisplayList();
            OnListChanged?.Invoke();
        }

        public void ExecuteSearch()
        {
            if (_searchInput != null)
            {
                _searchQuery = _searchInput.text != null ? _searchInput.text.Trim().ToLower() : "";

                // Reset navigation to show global search results
                _activeCategory = null;

                RebuildDisplayList();
                OnListChanged?.Invoke();
            }
        }

        private void RebuildDisplayList()
        {
            _displayItems.Clear();

            // Folders need the group prefab, so without it every ship is listed directly.
            if (!GroupPrefab)
            {
                AddAllShips();
                return;
            }

            // Search Filter Mode
            if (!string.IsNullOrEmpty(_searchQuery))
            {
                foreach (var rootCat in _rootCategories)
                {
                    foreach (var ship in rootCat.Ships)
                        CheckAndAddShipToSearch(ship);

                    foreach (var subCat in rootCat.SubCategories)
                    {
                        foreach (var ship in subCat.Ships)
                            CheckAndAddShipToSearch(ship);
                    }
                }
                return;
            }

            // Category Navigation Mode
            if (_activeCategory == null)
            {
                foreach (var cat in _rootCategories)
                    _displayItems.Add(new DisplayItem { IsCategory = true, IsBackButton = false, Category = cat });
            }
            else
            {
                _displayItems.Add(new DisplayItem { IsCategory = true, IsBackButton = true, Category = _activeCategory });

                foreach (var subCat in _activeCategory.SubCategories)
                    _displayItems.Add(new DisplayItem { IsCategory = true, IsBackButton = false, Category = subCat });

                foreach (var ship in _activeCategory.Ships)
                {
                    bool canInstall = !_shipAvailability.TryGetValue(ship, out var available) || available;
                    _displayItems.Add(new DisplayItem { IsCategory = false, IsBackButton = false, Ship = ship, CanInstall = canInstall });
                }
            }
        }

        private void AddAllShips()
        {
            foreach (var rootCat in _rootCategories)
            {
                foreach (var ship in rootCat.Ships)
                    AddShipIfMatches(ship);

                foreach (var subCat in rootCat.SubCategories)
                {
                    foreach (var ship in subCat.Ships)
                        AddShipIfMatches(ship);
                }
            }
        }

        private void AddShipIfMatches(IShip ship)
        {
            if (string.IsNullOrEmpty(_searchQuery))
            {
                bool canInstall = !_shipAvailability.TryGetValue(ship, out var available) || available;
                _displayItems.Add(new DisplayItem { IsCategory = false, IsBackButton = false, Ship = ship, CanInstall = canInstall });
            }
            else
            {
                CheckAndAddShipToSearch(ship);
            }
        }

        private void CheckAndAddShipToSearch(IShip ship)
        {
            string customName = ship.Name != null ? ship.Name.ToLower() : "";
            string originalName = ship.Model != null ? _localization.Localize(ship.Model.OriginalName).ToLower() : "";
            string factionName = ship.Model?.Faction != null ? _localization.Localize(ship.Model.Faction.Name).ToLower() : "";

            if (customName.Contains(_searchQuery) || originalName.Contains(_searchQuery) || factionName.Contains(_searchQuery))
            {
                bool canInstall = !_shipAvailability.TryGetValue(ship, out var available) || available;
                _displayItems.Add(new DisplayItem { IsCategory = false, IsBackButton = false, Ship = ship, CanInstall = canInstall });
            }
        }

        public int GetItemCount() => _displayItems.Count;
        public int GetItemType(int index) => _displayItems[index].IsCategory ? 1 : 0;

        public GameObject GetListItem(int index, int itemType, GameObject obj)
        {
            var itemData = _displayItems[index];

            if (itemType == 1)
            {
                if (obj == null) obj = Instantiate(GroupPrefab.gameObject);
                var groupItem = obj.GetComponent<ShipGroupListItem>();
                groupItem.Initialize(itemData.Category, this, itemData.IsBackButton);
                groupItem.gameObject.SetActive(true);
                return obj;
            }
            else
            {
                if (obj == null) obj = Instantiate(ItemPrefab.gameObject);
                var shipItem = obj.GetComponent<ShipListItem>();
                UpdateItem(shipItem, itemData);
                return obj;
            }
        }

        private void UpdateItem(ShipListItem item, DisplayItem itemData)
        {
            item.gameObject.SetActive(true);
            item.Initialize(itemData.Ship, _localization, _resourceLocator, itemData.CanInstall);

            var toggle = item.GetComponent<Toggle>();
            if (toggle != null)
                toggle.isOn = SelectedShip == itemData.Ship;
        }

        public struct DisplayItem
        {
            public bool IsCategory;
            public bool IsBackButton;
            public CategoryData Category;
            public IShip Ship;
            public bool CanInstall;
        }
    }
}