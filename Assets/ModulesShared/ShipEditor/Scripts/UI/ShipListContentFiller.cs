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
    public class ShipListContentFiller : MonoBehaviour, IContentFiller
    {
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly ILocalization _localization;

        [Inject] private readonly GameDatabase.IDatabase _database;

        [SerializeField] private ShipItem _itemPrefab;
        [SerializeField] private ShipGroupListItem _groupPrefab;

        [Header("Category Icons")]
        [SerializeField] private List<CategoryIcon> _categoryIcons = new List<CategoryIcon>();

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;

        private List<CategoryData> _rootCategories = new List<CategoryData>();
        private List<DisplayItem> _displayItems = new List<DisplayItem>();

        private CategoryData _activeCategory = null;
        private string _searchQuery = "";

        public event System.Action OnListChanged;

        public void Initialize(IEnumerable<IShip> ships)
        {
            _itemPrefab.gameObject.SetActive(false);
            if (_groupPrefab) _groupPrefab.gameObject.SetActive(false);

            _rootCategories.Clear();

            var groupedBySize = ships.GroupBy(s => s.Model.SizeClass).OrderBy(g => g.Key);

            foreach (var sizeGroup in groupedBySize)
            {
                var sizeCategory = new CategoryData
                {
                    Name = sizeGroup.Key.ToString(),
                    Icon = GetIconForSize(sizeGroup.Key),
                    Parent = null
                };

                // === ГЛАВНОЕ ИЗМЕНЕНИЕ ===
                // Теперь мы группируем корабли не только по ID корпуса, но и по их уникальной фракции!
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

                        Sprite shipIcon = _resourceLocator.GetSprite(firstShip.Model.IconImage);
                        if (shipIcon == null)
                        {
                            shipIcon = _resourceLocator.GetSprite(firstShip.Model.ModelImage);
                        }

                        string shipName = _localization.Localize(firstShip.Model.OriginalName);
                        string factionText = "";
                        if (firstShip.Model.Faction != null)
                        {
                            string factionLocalized = _localization.Localize(firstShip.Model.Faction.Name);
                            factionText = $" [{factionLocalized}]";
                        }

                        var subCategory = new CategoryData
                        {
                            Name = shipName + factionText,
                            Icon = shipIcon,
                            Parent = sizeCategory,
                            Ships = modelGroup.ToList()
                        };
                        sizeCategory.SubCategories.Add(subCategory);
                    }
                }

                _rootCategories.Add(sizeCategory);
            }

            // Reset search state
            _activeCategory = null;
            _searchQuery = "";

            if (_searchInput != null) _searchInput.text = "";

            RebuildDisplayList();
        }

        private Sprite GetIconForSize(SizeClass sizeClass)
        {
            if (_categoryIcons == null || _categoryIcons.Count == 0) return null;
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
                _searchQuery = _searchInput.text != null ? _searchInput.text.ToLower() : "";

                // Reset navigation for global search
                if (!string.IsNullOrEmpty(_searchQuery))
                    _activeCategory = null;

                RebuildDisplayList();
                OnListChanged?.Invoke();
            }
        }

        private void RebuildDisplayList()
        {
            _displayItems.Clear();

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                // Search Mode
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

            // Navigation Mode
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
                    _displayItems.Add(new DisplayItem { IsCategory = false, IsBackButton = false, Ship = ship });
            }
        }

        private void CheckAndAddShipToSearch(IShip ship)
        {
            // Filter by name and faction
            string shipName = _localization.Localize(ship.Model.OriginalName).ToLower();

            string factionName = "";
            if (ship.Model.Faction != null)
            {
                factionName = _localization.Localize(ship.Model.Faction.Name).ToLower();
            }

            if (shipName.Contains(_searchQuery) || factionName.Contains(_searchQuery))
            {
                _displayItems.Add(new DisplayItem { IsCategory = false, IsBackButton = false, Ship = ship });
            }
        }

        public int GetItemCount() => _displayItems.Count;
        public int GetItemType(int index) => _displayItems[index].IsCategory ? 1 : 0;

        public GameObject GetListItem(int index, int itemType, GameObject obj)
        {
            var itemData = _displayItems[index];

            if (itemType == 1)
            {
                if (obj == null) obj = Instantiate(_groupPrefab.gameObject);
                var groupItem = obj.GetComponent<ShipGroupListItem>();
                groupItem.Initialize(itemData.Category, this, itemData.IsBackButton);
                groupItem.gameObject.SetActive(true);
                return obj;
            }
            else
            {
                if (obj == null) obj = Instantiate(_itemPrefab.gameObject);
                var shipItem = obj.GetComponent<ShipItem>();
                shipItem.Initialize(itemData.Ship, _resourceLocator, _localization, _database);
                shipItem.gameObject.SetActive(true);
                return obj;
            }
        }
    }

    [System.Serializable]
    public struct CategoryIcon { public SizeClass SizeClass; public Sprite Icon; }
    public class CategoryData { public string Name; public Sprite Icon; public CategoryData Parent; public List<CategoryData> SubCategories = new List<CategoryData>(); public List<IShip> Ships = new List<IShip>(); }
    public struct DisplayItem { public bool IsCategory; public bool IsBackButton; public CategoryData Category; public IShip Ship; }
}