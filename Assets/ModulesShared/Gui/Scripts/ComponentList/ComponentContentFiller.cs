using System;
using System.Collections.Generic;
using Constructor;
using Services.Localization;
using Services.ObjectPool;
using Services.Resources;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zenject;

namespace Gui.ComponentList
{
    public class ComponentContentFiller : MonoBehaviour, IContentFiller
    {
        [Inject] private readonly IGameObjectFactory _gameObjectFactory;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly ILocalization _localization;

        [SerializeField] private ComponentListItemBase _itemPrefab;
        [SerializeField] private GroupListItem _groupPrefab;
        [SerializeField] private bool _showParents = true;
        [SerializeField] private bool _showSelected = false;

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;

        [SerializeField] private ItemSelectedEvent _itemSelectedEvent = new ItemSelectedEvent();

        [Serializable]
        public class ItemSelectedEvent : UnityEvent<ComponentInfo> { }

        private ComponentInfo _selectedItem;
        private IComponentTreeNode _node;
        private IComponentQuantityProvider _quantityProvider;
        private readonly List<IComponentTreeNode> _nodes = new List<IComponentTreeNode>();
        private readonly List<ComponentInfo> _components = new List<ComponentInfo>();

        private string _searchQuery = "";

        private void Awake()
        {
            _itemPrefab.gameObject.SetActive(false);
            _groupPrefab.gameObject.SetActive(false);
        }

        public void InitializeItems(IComponentTreeNode node)
        {
            _node = node;
            _quantityProvider = node.QuantityProvider;

            // Reset search state
            _searchQuery = "";
            if (_searchInput != null) _searchInput.text = "";

            RebuildData();
        }

        public void ExecuteSearch()
        {
            if (_searchInput != null)
            {
                _searchQuery = _searchInput.text != null ? _searchInput.text.ToLower() : "";
                RebuildData();
            }
        }

        private void RebuildData()
        {
            _components.Clear();
            _nodes.Clear();

            if (!string.IsNullOrEmpty(_searchQuery))
            {
                // Search Mode: find items from root recursively
                var root = _node;
                while (root.Parent != null)
                    root = root.Parent;

                FindComponentsRecursive(root);
            }
            else
            {
                // Navigation Mode: show current folder items
                foreach (var item in _node.Components)
                    if (_quantityProvider.GetQuantity(item) > 0)
                        _components.Add(item);

                if (_showParents)
                    for (var parent = _node.Parent; parent != null; parent = parent.Parent)
                        if (parent.ShouldNotExpand())
                            _nodes.Insert(0, parent);

                if (_showSelected && _node.IsVisible)
                    _nodes.Add(_node);

                _nodes.AddRange(_node.Nodes);
            }

            if (_components.IndexOf(SelectedItem) < 0)
                SelectedItem = ComponentInfo.Empty;
        }

        private void FindComponentsRecursive(IComponentTreeNode node)
        {
            foreach (var item in node.Components)
            {
                if (_quantityProvider.GetQuantity(item) > 0)
                {
                    // Filter by localized name
                    string itemName = item.GetName(_localization).ToLower();

                    if (itemName.Contains(_searchQuery))
                    {
                        if (!_components.Contains(item))
                            _components.Add(item);
                    }
                }
            }

            foreach (var child in node.Nodes)
            {
                FindComponentsRecursive(child);
            }
        }

        public GameObject GetListItem(int index, int itemType, GameObject obj)
        {
            if (obj == null)
            {
                obj = _gameObjectFactory.Create(itemType == 0 ? _groupPrefab.gameObject : _itemPrefab.gameObject);
            }

            if (itemType == 0)
            {
                var item = obj.GetComponent<GroupListItem>();
                UpdateItem(item, _nodes[index]);
            }
            else
            {
                var item = obj.GetComponent<ComponentListItemBase>();
                var component = _components[index - _nodes.Count];
                UpdateItem(item, component);
            }

            return obj;
        }

        public int GetItemCount() { return _nodes.Count + _components.Count; }
        public int GetItemType(int index) { return index < _nodes.Count ? 0 : 1; }

        public ComponentInfo SelectedItem
        {
            get { return _selectedItem; }
            set
            {
                if (_selectedItem.Equals(value))
                    return;

                _selectedItem = value;
                _itemSelectedEvent.Invoke(SelectedItem);
            }
        }

        public void OnItemSelected(ComponentListItemBase item)
        {
            if (!enabled) return;
            SelectedItem = item.Component;
        }

        private void UpdateItem(ComponentListItemBase item, ComponentInfo component)
        {
            item.gameObject.SetActive(true);
            item.Initialize(component, _quantityProvider.GetQuantity(component));
            item.Selected = component.Equals(SelectedItem);
        }

        private void UpdateItem(GroupListItem item, IComponentTreeNode node)
        {
            item.gameObject.SetActive(true);
            item.Initialize(node, _node);
        }
    }
}