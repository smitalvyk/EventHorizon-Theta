using System.Collections.Generic;
using System.Linq;
using Constructor;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;

namespace Gui.ComponentList
{
    public class RootNode : IComponentTreeNode
    {
        private readonly IDatabase _database;

        public RootNode(IComponentQuantityProvider quantityProvider, IDatabase database = null)
        {
            _database = database;

            if (database != null)
                _weaponNode = new WeaponNode(this, database.WeaponSlots);

            _quantityProvider = quantityProvider;
            _armorNode = CreateNode("$GroupArmor", new SpriteId("icons/icon_shield", SpriteId.Type.Default));
            _energyNode = CreateNode("$GroupEnergy", new SpriteId("icons/icon_battery", SpriteId.Type.Default));
            _droneNode = CreateNode("$GroupDrones", new SpriteId("icons/icon_drone", SpriteId.Type.Default));
            _engineNode = CreateNode("$GroupEngines", new SpriteId("icons/icon_engine", SpriteId.Type.Default));
            _specialNode = CreateNode("$GroupSpecial", new SpriteId("icons/icon_gear", SpriteId.Type.Default));

            IsVisible = true;

            LoadCustomCategoriesSafe();
        }

        public void AddNode(IComponentTreeNode node, bool inTheEnd = false)
        {
            if (inTheEnd) _extraNodes2.Add(node);
            else _extraNodes1.Add(node);
        }

        public IComponentTreeNode Parent { get { return null; } }
        public IComponentQuantityProvider QuantityProvider { get { return _quantityProvider; } }

        public IComponentTreeNode Weapon { get { return _weaponNode; } }
        public IComponentTreeNode Armor { get { return _armorNode; } }
        public IComponentTreeNode Drone { get { return _droneNode; } }
        public IComponentTreeNode Engine { get { return _engineNode; } }
        public IComponentTreeNode Energy { get { return _energyNode; } }
        public IComponentTreeNode Special { get { return _specialNode; } }

        public string Name { get { return "$GroupAll"; } }
        public SpriteId Icon { get { return new SpriteId("icons/icon_gear", SpriteId.Type.Default); } }
        public UnityEngine.Color Color { get { return CommonNode.DefaultColor; } }
        public bool IsVisible { get; set; }

        public void Add(ComponentInfo componentInfo)
        {
            int categoryId = (int)componentInfo.Data.DisplayCategory;

            if (categoryId >= 7 && categoryId <= 99 && _parsedCustomCategories != null)
            {
                var customData = _parsedCustomCategories.FirstOrDefault(c => c.Id == categoryId);
                if (customData != null)
                {
                    var node = GetOrCreateCustomNode(customData);
                    node.Add(componentInfo);
                    _count = -1;
                    return;
                }
            }

            if (componentInfo.Data.Weapon != null)
            {
                _weaponNode.Add(componentInfo);
                return;
            }

            switch (componentInfo.Data.DisplayCategory)
            {
                case ComponentCategory.Defense: _armorNode.Add(componentInfo); break;
                case ComponentCategory.Energy: _energyNode.Add(componentInfo); break;
                case ComponentCategory.Engine: _engineNode.Add(componentInfo); break;
                case ComponentCategory.Drones: _droneNode.Add(componentInfo); break;
                default: _specialNode.Add(componentInfo); break;
            }

            _count = -1;
        }

        private class CustomCategoryInfo
        {
            public int Id;
            public string Name;
            public string Icon;
            public int ParentId;
            public bool AlwaysShow;
        }

        private List<CustomCategoryInfo> _parsedCustomCategories;

        private void LoadCustomCategoriesSafe()
        {
            if (_parsedCustomCategories != null) return;
            _parsedCustomCategories = new List<CustomCategoryInfo>();

            if (_database == null) return;

            var prop = _database.GetType().GetProperty("CustomCategorySettings");
            if (prop == null) return;

            var settings = prop.GetValue(_database, null);
            if (settings == null) return;

            System.Collections.IEnumerable list = null;
            var field = settings.GetType().GetField("Categories");
            if (field != null) list = field.GetValue(settings) as System.Collections.IEnumerable;
            else
            {
                var property = settings.GetType().GetProperty("Categories");
                if (property != null) list = property.GetValue(settings, null) as System.Collections.IEnumerable;
            }

            if (list == null) return;

            foreach (var item in list)
            {
                if (item == null) continue;
                var t = item.GetType();

                var idVal = GetMemberValue(t, item, "Id");
                var nameVal = GetMemberValue(t, item, "Name");
                var iconVal = GetMemberValue(t, item, "Icon");
                var parentIdVal = GetMemberValue(t, item, "ParentId");
                var alwaysShowVal = GetMemberValue(t, item, "AlwaysShow");

                var catInfo = new CustomCategoryInfo
                {
                    Id = idVal != null ? System.Convert.ToInt32(idVal) : 0,
                    Name = nameVal != null ? nameVal.ToString() : "",
                    Icon = iconVal != null ? iconVal.ToString() : "",
                    ParentId = parentIdVal != null ? System.Convert.ToInt32(parentIdVal) : 0,
                    AlwaysShow = alwaysShowVal != null ? System.Convert.ToBoolean(alwaysShowVal) : true
                };

                _parsedCustomCategories.Add(catInfo);
            }
        }

        private object GetMemberValue(System.Type type, object instance, string memberName)
        {
            var field = type.GetField(memberName);
            if (field != null) return field.GetValue(instance);

            var prop = type.GetProperty(memberName);
            if (prop != null) return prop.GetValue(instance, null);

            return null;
        }

        private readonly Dictionary<int, DynamicCategoryNode> _allCustomNodes = new Dictionary<int, DynamicCategoryNode>();
        private readonly List<DynamicCategoryNode> _rootDynamicNodes = new List<DynamicCategoryNode>();

        private DynamicCategoryNode GetOrCreateCustomNode(CustomCategoryInfo catData)
        {
            if (_allCustomNodes.TryGetValue(catData.Id, out var existingNode))
                return existingNode;

            SpriteId icon = string.IsNullOrEmpty(catData.Icon) ? new SpriteId("icons/icon_gear", SpriteId.Type.Default) : new SpriteId(catData.Icon, SpriteId.Type.Default);

            var node = new DynamicCategoryNode(catData.Name, icon, this, catData.AlwaysShow);
            _allCustomNodes.Add(catData.Id, node);

            if (catData.ParentId >= 7 && catData.ParentId <= 99)
            {
                var parentData = _parsedCustomCategories.FirstOrDefault(c => c.Id == catData.ParentId);
                if (parentData != null)
                {
                    var parentNode = GetOrCreateCustomNode(parentData);
                    parentNode.AddSubFolder(node);
                    node.SetParent(parentNode);
                    return node;
                }
            }

            _rootDynamicNodes.Add(node);
            return node;
        }

        public int ItemCount
        {
            get
            {
                if (_count < 0) _count = Children.GetItemCount();
                return _count;
            }
        }

        public IEnumerable<IComponentTreeNode> Nodes { get { return Children.ChildrenNodes(); } }
        public IEnumerable<ComponentInfo> Components { get { return Children.ChildrenComponents(); } }
        public void Clear() { Children.Clear(); }

        private IEnumerable<IComponentTreeNode> Children
        {
            get
            {
                foreach (var node in _extraNodes1) yield return node;

                yield return _weaponNode;
                yield return _armorNode;
                yield return _energyNode;
                yield return _droneNode;
                yield return _engineNode;
                yield return _specialNode;

                foreach (var rootNode in _rootDynamicNodes) yield return rootNode;

                foreach (var node in _extraNodes2) yield return node;
            }
        }

        private IComponentTreeNode CreateNode(string name, SpriteId icon) { return new CommonNode(name, icon, this); }

        private int _count = -1;
        private readonly IComponentTreeNode _weaponNode;
        private readonly IComponentTreeNode _armorNode;
        private readonly IComponentTreeNode _energyNode;
        private readonly IComponentTreeNode _droneNode;
        private readonly IComponentTreeNode _engineNode;
        private readonly IComponentTreeNode _specialNode;
        private readonly IComponentQuantityProvider _quantityProvider;
        private readonly List<IComponentTreeNode> _extraNodes1 = new List<IComponentTreeNode>();
        private readonly List<IComponentTreeNode> _extraNodes2 = new List<IComponentTreeNode>();
    }

    public class DynamicCategoryNode : IComponentTreeNode
    {
        private readonly IComponentQuantityProvider _quantityProvider;
        private readonly bool _alwaysShow;
        private readonly string _name;
        private readonly SpriteId _icon;
        private IComponentTreeNode _parent;
        private int _count = -1;

        private readonly List<DynamicCategoryNode> _subFolders = new List<DynamicCategoryNode>();
        private readonly Dictionary<int, IComponentTreeNode> _components = new Dictionary<int, IComponentTreeNode>();

        public DynamicCategoryNode(string name, SpriteId icon, IComponentTreeNode parent, bool alwaysShow)
        {
            _name = name;
            _icon = icon;
            _parent = parent;
            _alwaysShow = alwaysShow;

            if (_alwaysShow)
                _quantityProvider = new CustomQuantityProviderWrapper(parent.QuantityProvider);
            else
                _quantityProvider = parent.QuantityProvider;
        }

        public void SetParent(IComponentTreeNode parent) { _parent = parent; }
        public void AddSubFolder(DynamicCategoryNode subFolder) { _subFolders.Add(subFolder); }

        public IComponentTreeNode Parent { get { return _parent; } }
        public IComponentQuantityProvider QuantityProvider { get { return _quantityProvider; } }

        public string Name { get { return _name; } }
        public SpriteId Icon { get { return _icon; } }
        public UnityEngine.Color Color { get { return CommonNode.DefaultColor; } }
        public bool IsVisible => true;

        public void Add(ComponentInfo componentInfo)
        {
            var component = componentInfo.Data;
            if (!_components.TryGetValue(component.Id.Value, out var node))
            {
                node = new ComponentNode(component, this);
                _components.Add(component.Id.Value, node);
            }
            node.Add(componentInfo);
            _count = -1;
        }

        public int ItemCount
        {
            get
            {
                if (_alwaysShow) return 999;

                if (_count < 0) _count = Children.GetItemCount();
                return _count;
            }
        }

        public IEnumerable<IComponentTreeNode> Nodes { get { return Children.ChildrenNodes(); } }
        public IEnumerable<ComponentInfo> Components { get { return Children.ChildrenComponents(); } }
        private IEnumerable<IComponentTreeNode> Children => _subFolders.Cast<IComponentTreeNode>().Concat(_components.Values);

        public void Clear()
        {
            foreach (var folder in _subFolders)
            {
                folder.Clear(); 
            }
            _components.Values.Clear();
            _count = -1;
        }

        private class CustomQuantityProviderWrapper : IComponentQuantityProvider
        {
            private readonly IComponentQuantityProvider _provider;
            public CustomQuantityProviderWrapper(IComponentQuantityProvider provider) { _provider = provider; }
            public int GetQuantity(ComponentInfo component)
            {
                return _provider != null ? _provider.GetQuantity(component) : 0;
            }
        }
    }

    public class WeaponNode : IComponentTreeNode
    {
        public WeaponNode(IComponentTreeNode parent, WeaponSlots weaponSlots)
        {
            _parent = parent;

            foreach (var slot in weaponSlots.Slots)
                if (_groupMap.TryAdd(slot.Letter, _groups.Count))
                    _groups.Add(CreateNode(slot.Name, slot.Icon));
                else
                    GameDiagnostics.Trace.LogError($"Duplicate weapon slot - {slot.Letter}");

            _groupMap.Add(default, _groups.Count);
            _groups.Add(CreateNode(weaponSlots?.DefaultSlotName, weaponSlots.DefaultSlotIcon));
        }

        public IComponentTreeNode Parent { get { return _parent; } }
        public IComponentQuantityProvider QuantityProvider { get { return _parent.QuantityProvider; } }

        public string Name { get { return "$GroupWeapon"; } }
        public SpriteId Icon { get { return new SpriteId("textures/icons/icon_weapon", SpriteId.Type.Default); } }
        public UnityEngine.Color Color { get { return CommonNode.DefaultColor; } }

        public void Add(ComponentInfo componentInfo)
        {
            var weapon = componentInfo.Data.Weapon;
            if (weapon == null) return;

            if (!_groupMap.TryGetValue((char)componentInfo.Data.WeaponSlotType, out var groupId))
                groupId = _groups.Count - 1;

            _groups[groupId].Add(componentInfo);
        }

        public int ItemCount { get { if (_count < 0) _count = Children.GetItemCount(); return _count; } }
        public IEnumerable<IComponentTreeNode> Nodes { get { return Children.ChildrenNodes(); } }
        public IEnumerable<ComponentInfo> Components { get { return Children.ChildrenComponents(); } }
        public void Clear() { Children.Clear(); }
        public bool IsVisible => true;

        private IEnumerable<IComponentTreeNode> Children => _groups;
        private IComponentTreeNode CreateNode(string name, SpriteId icon) { return new CommonNode(name, icon, this); }

        private int _count = -1;
        private readonly IComponentTreeNode _parent;
        private readonly Dictionary<char, int> _groupMap = new();
        private readonly List<IComponentTreeNode> _groups = new();
    }

    public class ComponentNode : IComponentTreeNode
    {
        public ComponentNode(GameDatabase.DataModel.Component component, IComponentTreeNode parent)
        {
            _component = component;
            _parent = parent;
        }

        public IComponentTreeNode Parent { get { return _parent; } }
        public IComponentQuantityProvider QuantityProvider { get { return _parent.QuantityProvider; } }

        public string Name { get { return _component.Name; } }
        public SpriteId Icon { get { return _component.Icon; } }
        public UnityEngine.Color Color { get { return _component.Color; } }
        public bool IsVisible => true;

        public void Add(ComponentInfo componentInfo) { _components.Add(componentInfo); }
        public int ItemCount { get { return _components.Count; } }
        public IEnumerable<IComponentTreeNode> Nodes { get { return Enumerable.Empty<IComponentTreeNode>(); } }
        public IEnumerable<ComponentInfo> Components { get { return _components; } }
        public void Clear() { _components.Clear(); }

        private readonly GameDatabase.DataModel.Component _component;
        private readonly IComponentTreeNode _parent;
        private readonly HashSet<ComponentInfo> _components = new HashSet<ComponentInfo>();
    }

    public class CommonNode : IComponentTreeNode
    {
        public CommonNode(string name, SpriteId icon, IComponentTreeNode parent)
        {
            _parent = parent;
            _name = name;
            _icon = icon;
        }

        public IComponentTreeNode Parent { get { return _parent; } }
        public IComponentQuantityProvider QuantityProvider { get { return _parent.QuantityProvider; } }

        public string Name { get { return _name; } }
        public SpriteId Icon { get { return _icon; } }
        public UnityEngine.Color Color { get { return DefaultColor; } }
        public bool IsVisible => true;

        public void Add(ComponentInfo componentInfo)
        {
            var component = componentInfo.Data;
            IComponentTreeNode node;
            if (!_components.TryGetValue(component.Id.Value, out node))
            {
                node = new ComponentNode(component, this);
                _components.Add(component.Id.Value, node);
            }

            node.Add(componentInfo);
            _count = -1;
        }

        public int ItemCount { get { if (_count < 0) _count = _components.Values.GetItemCount(); return _count; } }
        public IEnumerable<IComponentTreeNode> Nodes { get { return _components.Values.Where(ComponentTreeNodeExtensions.ShouldNotExpand); } }
        public IEnumerable<ComponentInfo> Components { get { return _components.Values.ChildrenComponents(); } }
        public void Clear() { _components.Values.Clear(); }

        private int _count;
        private readonly string _name;
        private readonly SpriteId _icon;
        private readonly IComponentTreeNode _parent;
        private readonly Dictionary<int, IComponentTreeNode> _components = new Dictionary<int, IComponentTreeNode>();

        public static readonly UnityEngine.Color DefaultColor = Gui.Theme.UiTheme.Current.GetColor(Theme.ThemeColor.ButtonIcon);
    }

    public class ComponentListNode : IComponentTreeNode
    {
        public ComponentListNode(string name, SpriteId icon, IComponentTreeNode parent)
        {
            _parent = parent;
            _name = name;
            _icon = icon;
        }

        public IComponentTreeNode Parent { get { return _parent; } }
        public IComponentQuantityProvider QuantityProvider { get { return _parent.QuantityProvider; } }

        public string Name { get { return _name; } }
        public SpriteId Icon { get { return _icon; } }
        public UnityEngine.Color Color { get { return CommonNode.DefaultColor; } }
        public bool IsVisible => true;

        public void Add(ComponentInfo componentInfo) { _components.Add(componentInfo); }
        public int ItemCount { get { return _components.Count; } }
        public IEnumerable<IComponentTreeNode> Nodes { get { return Enumerable.Empty<IComponentTreeNode>(); } }
        public IEnumerable<ComponentInfo> Components { get { return _components; } }
        public void Clear() { _components.Clear(); }

        private readonly string _name;
        private readonly SpriteId _icon;
        private readonly IComponentTreeNode _parent;
        private readonly HashSet<ComponentInfo> _components = new HashSet<ComponentInfo>();
    }
}