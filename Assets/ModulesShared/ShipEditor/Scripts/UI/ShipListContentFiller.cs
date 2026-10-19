using System;
using System.Collections.Generic;
using System.Linq;
using Constructor.Ships;
using GameDatabase.Enums;
using Services.Localization;
using Services.Resources;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace ShipEditor.UI
{
    public class ShipListContentFiller : MonoBehaviour, IContentFiller, IShipCategoryHandler
    {
        private const int ShipItemType = 0;
        private const int CategoryItemType = 1;

        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly GameDatabase.IDatabase _database;

        [SerializeField] private ShipItem _itemPrefab;
        [SerializeField] private ShipGroupListItem _groupPrefab;

        [Header("Category Icons")]
        [SerializeField] private List<CategoryIcon> _categoryIcons = new List<CategoryIcon>();

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;

        private readonly List<CategoryData> _rootCategories = new List<CategoryData>();
        private readonly List<DisplayItem> _displayItems = new List<DisplayItem>();
        private readonly List<IShip> _searchPool = new List<IShip>();
        private readonly Dictionary<IShip, string> _searchText = new Dictionary<IShip, string>();

        private CategoryData _activeCategory;
        private string _searchQuery = string.Empty;

        public event Action OnListChanged;

        public void Initialize(IEnumerable<IShip> ships)
        {
            _itemPrefab.gameObject.SetActive(false);
            if (_groupPrefab)
                _groupPrefab.gameObject.SetActive(false);

            _rootCategories.Clear();
            _searchText.Clear();

            foreach (var sizeGroup in ships.GroupBy(s => s.Model.SizeClass).OrderBy(g => g.Key))
                _rootCategories.Add(CreateSizeCategory(sizeGroup.Key, sizeGroup));

            _activeCategory = null;
            _searchQuery = string.Empty;
            if (_searchInput != null)
                _searchInput.text = string.Empty;

            RebuildDisplayList();
        }

        public void OpenCategory(CategoryData category)
        {
            _activeCategory = category;
            NotifyListChanged();
        }

        public void GoBack()
        {
            if (_activeCategory != null)
                _activeCategory = _activeCategory.Parent;

            NotifyListChanged();
        }

        public void ExecuteSearch()
        {
            if (_searchInput == null)
                return;

            var query = _searchInput.text == null ? string.Empty : _searchInput.text.Trim();
            if (query == _searchQuery)
                return;

            _searchQuery = query;
            NotifyListChanged();
        }

        public int GetItemCount() => _displayItems.Count;

        public int GetItemType(int index) => _displayItems[index].IsCategory ? CategoryItemType : ShipItemType;

        public GameObject GetListItem(int index, int itemType, GameObject obj)
        {
            var itemData = _displayItems[index];

            if (itemType == CategoryItemType)
            {
                if (obj == null)
                    obj = Instantiate(_groupPrefab.gameObject);

                obj.GetComponent<ShipGroupListItem>().Initialize(itemData.Category, this, itemData.IsBackButton);
                obj.SetActive(true);
                return obj;
            }

            if (obj == null)
                obj = Instantiate(_itemPrefab.gameObject);

            obj.GetComponent<ShipItem>().Initialize(itemData.Ship, _resourceLocator, _localization, _database);
            obj.SetActive(true);
            return obj;
        }

        private CategoryData CreateSizeCategory(SizeClass sizeClass, IEnumerable<IShip> ships)
        {
            var category = new CategoryData { Name = sizeClass.ToString(), Icon = GetIconForSize(sizeClass) };

            foreach (var group in ships.GroupBy(GetGroupKey))
            {
                var members = group.ToList();
                if (members.Count == 1)
                    category.Ships.Add(members[0]);
                else
                    category.AddSubCategory(CreateModelCategory(members));
            }

            return category;
        }

        private CategoryData CreateModelCategory(List<IShip> members)
        {
            var model = members[0].Model;

            var icon = _resourceLocator.GetSprite(model.IconImage);
            if (icon == null)
                icon = _resourceLocator.GetSprite(model.ModelImage);

            var factionText = model.Faction != null ? $" [{_localization.Localize(model.Faction.Name)}]" : string.Empty;

            return new CategoryData
            {
                Name = _localization.Localize(model.OriginalName) + factionText,
                Icon = icon,
                Ships = members
            };
        }

        private static string GetGroupKey(IShip ship)
        {
            var faction = ship.Model.Faction;
            return ship.Model.Id + "|" + (faction != null ? "f:" + faction.Name : "-");
        }

        private Sprite GetIconForSize(SizeClass sizeClass)
        {
            foreach (var entry in _categoryIcons)
            {
                if (entry.SizeClass == sizeClass)
                    return entry.Icon;
            }

            return null;
        }

        private void NotifyListChanged()
        {
            RebuildDisplayList();
            OnListChanged?.Invoke();
        }

        private void RebuildDisplayList()
        {
            _displayItems.Clear();

            if (_searchQuery.Length > 0)
                AddSearchResults();
            else
                AddNavigationEntries();
        }

        private void AddSearchResults()
        {
            _searchPool.Clear();
            foreach (var root in _rootCategories)
                root.CollectShips(_searchPool);

            foreach (var ship in _searchPool)
            {
                if (MatchesSearch(ship))
                    _displayItems.Add(new DisplayItem { Ship = ship });
            }

            _searchPool.Clear();
        }

        private void AddNavigationEntries()
        {
            if (_activeCategory == null)
            {
                foreach (var category in _rootCategories)
                    _displayItems.Add(new DisplayItem { Category = category });
                return;
            }

            _displayItems.Add(new DisplayItem { Category = _activeCategory, IsBackButton = true });

            foreach (var sub in _activeCategory.SubCategories)
                _displayItems.Add(new DisplayItem { Category = sub });

            foreach (var ship in _activeCategory.Ships)
                _displayItems.Add(new DisplayItem { Ship = ship });
        }

        private bool MatchesSearch(IShip ship)
        {
            return GetSearchText(ship).IndexOf(_searchQuery, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSearchText(IShip ship)
        {
            string text;
            if (_searchText.TryGetValue(ship, out text))
                return text;

            var faction = ship.Model.Faction;
            text = _localization.Localize(ship.Model.OriginalName) + "\n" +
                   (faction != null ? _localization.Localize(faction.Name) : string.Empty);

            _searchText[ship] = text;
            return text;
        }
    }

    [Serializable]
    public struct CategoryIcon
    {
        public SizeClass SizeClass;
        public Sprite Icon;
    }

    public struct DisplayItem
    {
        public bool IsBackButton;
        public CategoryData Category;
        public IShip Ship;

        public bool IsCategory => Category != null;
    }
}