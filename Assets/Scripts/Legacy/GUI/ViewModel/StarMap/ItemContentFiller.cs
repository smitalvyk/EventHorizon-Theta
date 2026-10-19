using System;
using System.Collections.Generic;
using Economy;
using Economy.Products;
using GameDatabase;
using GameServices.Player;
using Services.Localization;
using Services.ObjectPool;
using Services.Resources;
using ShipEditor.UI;
using UnityEngine;
using UnityEngine.UI;
using ViewModel.Common;
using Zenject;

namespace Gui.StarMap
{
    public class ItemContentFiller : MonoBehaviour, IContentFiller, IShipCategoryHandler
    {
        private const int ProductItemType = 0;
        private const int CategoryItemType = 1;
        private const char LocalSearchPrefix = '@';

        [Inject] private readonly IDatabase _database;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly IGameObjectFactory _gameObjectFactory;
        [Inject] private readonly PlayerResources _playerResources;

        [SerializeField] private InventoryItem _itemPrefab;
        [SerializeField] private ShipGroupListItem _groupPrefab;

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;
        [SerializeField] private Button _searchButton;

        private readonly List<InventoryCategoryNode> _rootCategories = new List<InventoryCategoryNode>();
        private readonly List<IProduct> _looseItems = new List<IProduct>();
        private readonly List<DisplayEntry> _displayItems = new List<DisplayEntry>();
        private readonly List<IProduct> _searchPool = new List<IProduct>();
        private readonly Dictionary<IProduct, string> _searchText = new Dictionary<IProduct, string>();

        private InventoryCategoryBuilder _builder;
        private InventoryCategoryNode _activeCategory;
        private IProduct _selectedProduct;
        private string _searchQuery = string.Empty;
        private string _searchTerm = string.Empty;
        private bool _localSearch;
        private bool _sellMode;

        public event Action OnListChanged;

        public IProduct SelectedProduct
        {
            get
            {
                if (_selectedProduct == null)
                    return null;

                foreach (var entry in _displayItems)
                {
                    if (!entry.IsCategory && IsSameProduct(entry.Product, _selectedProduct))
                        return entry.Product;
                }

                return null;
            }
        }

        private InventoryCategoryBuilder Builder =>
            _builder ?? (_builder = new InventoryCategoryBuilder(_database, _localization, _resourceLocator));

        private void OnEnable()
        {
            if (_searchButton != null)
                _searchButton.onClick.AddListener(ExecuteSearch);
            if (_searchInput != null)
                _searchInput.onEndEdit.AddListener(OnSearchEndEdit);
        }

        private void OnDisable()
        {
            if (_searchButton != null)
                _searchButton.onClick.RemoveListener(ExecuteSearch);
            if (_searchInput != null)
                _searchInput.onEndEdit.RemoveListener(OnSearchEndEdit);
        }

        public void InitializeItems(IEnumerable<IProduct> items, bool sellMode = true, bool clearSelection = false)
        {
            if (_itemPrefab)
                _itemPrefab.gameObject.SetActive(false);
            if (_groupPrefab)
                _groupPrefab.gameObject.SetActive(false);

            _sellMode = sellMode;
            if (clearSelection)
                _selectedProduct = null;

            var previous = _activeCategory;

            var catalog = Builder.Build(items, !sellMode);
            _rootCategories.Clear();
            _rootCategories.AddRange(catalog.Roots);
            _looseItems.Clear();
            _looseItems.AddRange(catalog.Loose);
            _searchText.Clear();

            _activeCategory = previous != null ? FindCategory(_rootCategories, previous.Id) : null;
            RebuildDisplayList();
        }

        public void OpenCategory(CategoryData category)
        {
            var node = category as InventoryCategoryNode;
            if (node == null)
                return;

            _activeCategory = node;
            NotifyListChanged();
        }

        public void GoBack()
        {
            if (_activeCategory != null)
                _activeCategory = _activeCategory.Parent as InventoryCategoryNode;

            NotifyListChanged();
        }

        // One step back: clears the search first, then leaves the current category. Returns false at the root.
        public bool TryNavigateBack()
        {
            var hasSearchText = _searchInput != null && !string.IsNullOrEmpty(_searchInput.text);
            if (_searchQuery.Length > 0 || hasSearchText)
            {
                if (_searchInput != null)
                    _searchInput.text = string.Empty;

                SetSearchQuery(string.Empty);
                NotifyListChanged();
                return true;
            }

            if (_activeCategory == null)
                return false;

            GoBack();
            return true;
        }

        public void ExecuteSearch()
        {
            if (_searchInput == null || !SetSearchQuery(_searchInput.text))
                return;

            NotifyListChanged();
        }

        public void OnItemSelected(InventoryItem item)
        {
            _selectedProduct = item != null ? item.Product : null;
        }

        public int GetItemCount() => _displayItems.Count;

        public int GetItemType(int index) => _displayItems[index].IsCategory ? CategoryItemType : ProductItemType;

        public GameObject GetListItem(int index, int itemType, GameObject obj)
        {
            var entry = _displayItems[index];
            return itemType == CategoryItemType ? GetCategoryView(entry, obj) : GetProductView(entry, obj);
        }

        private GameObject GetCategoryView(DisplayEntry entry, GameObject obj)
        {
            if (obj == null)
                obj = _gameObjectFactory.Create(_groupPrefab.gameObject);

            obj.GetComponent<ShipGroupListItem>().Initialize(entry.Category, this, entry.IsBackButton);
            obj.SetActive(true);
            return obj;
        }

        private GameObject GetProductView(DisplayEntry entry, GameObject obj)
        {
            if (obj == null)
                obj = _gameObjectFactory.Create(_itemPrefab.gameObject);

            var product = entry.Product;
            var view = obj.GetComponent<InventoryItem>();
            obj.SetActive(true);
            view.Initialize(product, !(_sellMode || product.Price.IsEnough(_playerResources)), _resourceLocator);

            var toggle = obj.GetComponent<Toggle>();
            if (toggle != null)
                toggle.SetIsOnWithoutNotify(IsSameProduct(product, _selectedProduct));

            return obj;
        }

        private void OnSearchEndEdit(string text)
        {
            ExecuteSearch();
        }

        private bool SetSearchQuery(string raw)
        {
            var query = raw == null ? string.Empty : raw.Trim();
            if (query == _searchQuery)
                return false;

            _searchQuery = query;
            _localSearch = query.Length > 0 && query[0] == LocalSearchPrefix;
            _searchTerm = (_localSearch ? query.Substring(1) : query).Trim();
            return true;
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

            if (_localSearch && _activeCategory != null)
            {
                _displayItems.Add(DisplayEntry.ForCategory(_activeCategory, true));
                _activeCategory.CollectItems(_searchPool);
            }
            else
            {
                foreach (var root in _rootCategories)
                    root.CollectItems(_searchPool);

                _searchPool.AddRange(_looseItems);
            }

            foreach (var product in _searchPool)
            {
                if (MatchesSearch(product))
                    _displayItems.Add(DisplayEntry.ForProduct(product));
            }

            _searchPool.Clear();
        }

        private void AddNavigationEntries()
        {
            if (_activeCategory == null)
            {
                foreach (var category in _rootCategories)
                    _displayItems.Add(DisplayEntry.ForCategory(category, false));

                foreach (var product in _looseItems)
                    _displayItems.Add(DisplayEntry.ForProduct(product));

                return;
            }

            _displayItems.Add(DisplayEntry.ForCategory(_activeCategory, true));

            foreach (var sub in _activeCategory.SubCategories)
            {
                var child = sub as InventoryCategoryNode;
                if (child != null)
                    _displayItems.Add(DisplayEntry.ForCategory(child, false));
            }

            foreach (var product in _activeCategory.Items)
                _displayItems.Add(DisplayEntry.ForProduct(product));
        }

        private bool MatchesSearch(IProduct product)
        {
            if (_searchTerm.Length == 0)
                return true;

            return GetSearchText(product).IndexOf(_searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string GetSearchText(IProduct product)
        {
            string text;
            if (_searchText.TryGetValue(product, out text))
                return text;

            var type = product.Type;
            var description = string.IsNullOrEmpty(type.Description) ? string.Empty : _localization.GetString(type.Description);
            text = _localization.GetString(type.Name) + "\n" + description;

            _searchText[product] = text;
            return text;
        }

        // Inventories may recreate product objects on every read, so identity is the item type, not the instance.
        private static bool IsSameProduct(IProduct a, IProduct b)
        {
            if (ReferenceEquals(a, b))
                return true;

            if (a == null || b == null)
                return false;

            return string.Equals(a.Type.Id, b.Type.Id, StringComparison.Ordinal);
        }

        private static InventoryCategoryNode FindCategory(IEnumerable<CategoryData> nodes, int id)
        {
            foreach (var node in nodes)
            {
                var inventoryNode = node as InventoryCategoryNode;
                if (inventoryNode == null)
                    continue;

                if (inventoryNode.Id == id)
                    return inventoryNode;

                var found = FindCategory(inventoryNode.SubCategories, id);
                if (found != null)
                    return found;
            }

            return null;
        }

        private readonly struct DisplayEntry
        {
            public readonly InventoryCategoryNode Category;
            public readonly IProduct Product;
            public readonly bool IsBackButton;

            private DisplayEntry(InventoryCategoryNode category, IProduct product, bool isBackButton)
            {
                Category = category;
                Product = product;
                IsBackButton = isBackButton;
            }

            public bool IsCategory => Category != null;

            public static DisplayEntry ForCategory(InventoryCategoryNode category, bool isBackButton) =>
                new DisplayEntry(category, null, isBackButton);

            public static DisplayEntry ForProduct(IProduct product) =>
                new DisplayEntry(null, product, false);
        }
    }
}