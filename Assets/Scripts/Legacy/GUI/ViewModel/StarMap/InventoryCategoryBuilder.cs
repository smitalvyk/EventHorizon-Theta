using System;
using System.Collections.Generic;
using System.Linq;
using Economy.Products;
using GameDatabase;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;
using Services.Localization;
using Services.Resources;
using UnityEngine;
using ItemTypes = Economy.ItemType;

namespace Gui.StarMap
{
    public sealed class InventoryCatalog
    {
        public readonly List<InventoryCategoryNode> Roots = new List<InventoryCategoryNode>();

        // Items shown at the top level without a folder. Used only when no categories are defined.
        public readonly List<IProduct> Loose = new List<IProduct>();
    }

    public class InventoryCategoryBuilder
    {
        public const int OtherId = 0;
        private const int AutoSlotIdBase = -1000;

        private const string OtherNameKey = "$Category_Other";
        private const string OtherIconPath = "icons/icon_gear";

        private readonly IDatabase _database;
        private readonly ILocalization _localization;
        private readonly IResourceLocator _resourceLocator;

        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();
        private readonly Dictionary<string, int> _questCategoryCache = new Dictionary<string, int>();
        private readonly List<NodeDef> _definitions = new List<NodeDef>();
        private readonly List<SlotDef> _slots = new List<SlotDef>();

        private InventoryCategoryMatcher _matcher;
        private NodeDef _other;
        private bool _definitionsReady;

        public InventoryCategoryBuilder(IDatabase database, ILocalization localization, IResourceLocator resourceLocator)
        {
            _database = database;
            _localization = localization;
            _resourceLocator = resourceLocator;
        }

        // Folders are ordered by id, "Other" goes last, empty branches are removed.
        // Without any defined categories every item is returned loose.
        public InventoryCatalog Build(IEnumerable<IProduct> products, bool sortById)
        {
            EnsureDefinitions();

            var catalog = new InventoryCatalog();
            var source = sortById
                ? products.OrderBy(p => p.Type.Id, StringComparer.Ordinal)
                : products
                    .OrderBy(p =>
                    {
                        var locName = _localization.GetString(p.Type.Name);
                        return string.IsNullOrEmpty(locName) || locName.StartsWith("$") ? p.Type.Id : locName;
                    }, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p =>
                    {
                        if (p.Type is Economy.ItemType.ComponentItem comp)
                        {
                            var info = comp.Component;
                            if (info.ItemQuality == Economy.ItemType.ItemQuality.Common)
                                return 25;
                            return (int)info.ModificationQuality * 10;
                        }
                        return (int)p.Type.Quality * 100;
                    })
                    .ThenBy(p => p.Type is Economy.ItemType.ShipItemBase ship ? ship.Rank : 0)
                    .ThenBy(p => p.Type is Economy.ItemType.ComponentItem comp ? comp.Component.Level : 0)
                    .ThenBy(p => p.Type.Id, StringComparer.Ordinal);

            if (_definitions.Count == 0)
            {
                catalog.Loose.AddRange(source);
                return catalog;
            }

            var nodes = new Dictionary<int, InventoryCategoryNode>();
            var created = new List<InventoryCategoryNode>();
            foreach (var definition in _definitions)
            {
                var node = CreateNode(definition);
                nodes[node.Id] = node;
                created.Add(node);
            }

            var other = CreateNode(_other);
            var roots = Link(nodes, created);
            var slotNodes = CreateSlotNodes(nodes);

            foreach (var product in source) Resolve(product, nodes, slotNodes, other).Items.Add(product);
            
            foreach (var root in roots)
            {
                if (root.Seal())
                    catalog.Roots.Add(root);
            }

            if (other.Seal()) catalog.Roots.Add(other);

            return catalog;
        }

        // Nests nodes by ParentId. Unknown parents, self-references and cycles produce roots.
        public static List<InventoryCategoryNode> Link(
            IDictionary<int, InventoryCategoryNode> nodes, IEnumerable<InventoryCategoryNode> candidates)
        {
            var roots = new List<InventoryCategoryNode>();

            foreach (var node in candidates.OrderBy(n => n.Id))
            {
                InventoryCategoryNode parent;
                if (node.ParentId > 0 && nodes.TryGetValue(node.ParentId, out parent) && !CreatesCycle(node, parent))
                    parent.AddSubCategory(node);
                else
                    roots.Add(node);
            }

            return roots;
        }

        private static bool CreatesCycle(InventoryCategoryNode node, InventoryCategoryNode parent)
        {
            for (ShipEditor.UI.CategoryData current = parent; current != null; current = current.Parent)
            {
                if (current == node)
                    return true;
            }

            return false;
        }

        // Weapon slot folders are created inside the category that owns the "weapon" key,
        // except for slots a category claims explicitly with "weapon:<slot>".
        private Dictionary<char, InventoryCategoryNode> CreateSlotNodes(Dictionary<int, InventoryCategoryNode> nodes)
        {
            var result = new Dictionary<char, InventoryCategoryNode>();

            InventoryCategoryNode owner;
            if (_slots.Count == 0 || !nodes.TryGetValue(_matcher.GetOwner(InventoryCategoryMatcher.WeaponGroup), out owner))
                return result;

            foreach (var slot in _slots)
            {
                var node = CreateNode(slot.Def);
                owner.AddSubCategory(node);
                result[slot.Letter] = node;
            }

            return result;
        }

        // Explicit quest item category first, then Fill keys, otherwise "Other".
        // A match on the plain "weapon" key goes to the slot folder when the weapon has a known slot.
        private InventoryCategoryNode Resolve(
            IProduct product,
            Dictionary<int, InventoryCategoryNode> nodes,
            Dictionary<char, InventoryCategoryNode> slotNodes,
            InventoryCategoryNode other)
        {
            InventoryCategoryNode node;

            var questCategory = GetQuestCategoryId(product);
            if (questCategory > 0 && nodes.TryGetValue(questCategory, out node))
                return node;

            var facts = CreateFacts(product);
            string matchedKey;
            var id = _matcher.Resolve(facts, out matchedKey);

            if (id <= 0 || !nodes.TryGetValue(id, out node))
                return other;

            if (matchedKey == InventoryCategoryMatcher.WeaponGroup)
            {
                char slotChar = (facts.WeaponSlot == '-' || facts.WeaponSlot == '\0') ? '\0' : facts.WeaponSlot;

                InventoryCategoryNode slotNode;
                if (slotNodes.TryGetValue(slotChar, out slotNode))
                    return slotNode;
            }

            return node;
        }

        private static ItemFacts CreateFacts(IProduct product)
        {
            var type = product.Type;

            var component = type as ItemTypes.ComponentItem;
            if (component != null)
            {
                var data = component.Component.Data;
                return new ItemFacts(ItemKind.Component, GetComponentGroup(data.DisplayCategory), data.WeaponSlotType, type.Id);
            }

            var kind = ItemKind.Other;
            if (type is ItemTypes.SatelliteItem)
                kind = ItemKind.Satellite;
            else if (type is ItemTypes.ShipItemBase)
                kind = ItemKind.Ship;
            else if (type is ItemTypes.ArtifactItem)
                kind = ItemKind.Artifact;
            else if (type is ItemTypes.FuelItem)
                kind = ItemKind.Fuel;

            return new ItemFacts(kind, null, '\0', type.Id);
        }

        private static string GetComponentGroup(ComponentCategory category)
        {
            switch (category)
            {
                case ComponentCategory.Weapon: return InventoryCategoryMatcher.WeaponGroup;
                case ComponentCategory.Defense: return "defend";
                case ComponentCategory.Energy: return "energy";
                case ComponentCategory.Engine: return "engine";
                case ComponentCategory.Drones: return "drone";
                case ComponentCategory.Special: return "special";
                default: return null;
            }
        }

        // Quest items and artifacts use the "a" id prefix (e.g. "a11" -> quest item 11).
        private int GetQuestCategoryId(IProduct product)
        {
            var rawId = product.Type != null ? product.Type.Id : null;
            if (string.IsNullOrEmpty(rawId) || !rawId.StartsWith("a", StringComparison.Ordinal))
                return 0;

            int categoryId;
            if (_questCategoryCache.TryGetValue(rawId, out categoryId))
                return categoryId;

            int id;
            if (int.TryParse(rawId.Substring(1), out id))
            {
                var questItem = _database.GetQuestItem(new ItemId<QuestItem>(id));
                if (questItem != null)
                    categoryId = questItem.InventoryCategory;
            }

            _questCategoryCache[rawId] = categoryId;
            return categoryId;
        }

        private void EnsureDefinitions()
        {
            if (_definitionsReady)
                return;

            _definitionsReady = true;

            var fills = new List<CategoryFill>();
            var settings = _database.ItemCategorySettings;

            if (settings != null)
            {
                var usedIds = new HashSet<int>();
                foreach (var category in settings.Categories)
                {
                    if (category.Id <= 0 || !usedIds.Add(category.Id))
                    {
                        Debug.LogWarning($"Inventory category '{category.Name}' skipped: id {category.Id} is invalid or already used");
                        continue;
                    }

                    _definitions.Add(new NodeDef(
                        category.Id, category.ParentId, _localization.GetString(category.Name), GetSprite(category.Icon)));

                    var keys = ConvertToKeys(category.Fill, category.Slots);
                    fills.Add(new CategoryFill(category.Id, keys));
                }
            }

            _matcher = new InventoryCategoryMatcher(fills);
            foreach (var conflict in _matcher.Conflicts)
                Debug.LogWarning("Inventory categories: " + conflict);

            _other = new NodeDef(OtherId, 0, _localization.GetString(OtherNameKey), GetSprite(OtherIconPath));

            LoadSlotDefinitions();
        }
        private static IEnumerable<string> ConvertToKeys(IEnumerable<CategoryTag> tags, string slots)
        {
            if (tags == null)
                yield break;

            foreach (var tag in tags)
            {
                switch (tag)
                {
                    case CategoryTag.Weapon:
                        if (string.IsNullOrEmpty(slots))
                        {
                            yield return InventoryCategoryMatcher.WeaponGroup;
                        }
                        else if (slots.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                        {
                            yield return InventoryCategoryMatcher.WeaponGroup + ":-";
                            yield return InventoryCategoryMatcher.WeaponGroup + ":all";
                        }
                        else
                        {
                            var splitSlots = slots.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var slot in splitSlots)
                                yield return InventoryCategoryMatcher.WeaponGroup + ":" + slot.Trim();
                        }
                        break;

                    case CategoryTag.Defend: yield return "defend"; break;
                    case CategoryTag.Energy: yield return "energy"; break;
                    case CategoryTag.Engine: yield return "engine"; break;
                    case CategoryTag.Drone: yield return "drone"; break;
                    case CategoryTag.Special: yield return "special"; break;
                    case CategoryTag.Satellite: yield return "satellite"; break;
                    case CategoryTag.Ship: yield return "ship"; break;
                    case CategoryTag.Artifact: yield return "artifact"; break;
                    case CategoryTag.Fuel: yield return "fuel"; break;
                }
            }
        }
        private void LoadSlotDefinitions()
        {
            var settings = _database.WeaponSlots;
            if (settings == null)
                return;

            var seen = new HashSet<char>();
            foreach (var slot in settings.Slots)
            {
                if (!seen.Add(slot.Letter) || IsSlotClaimed(slot.Letter))
                    continue;

                var def = new NodeDef(
                    AutoSlotIdBase - slot.Letter, 0, _localization.GetString(slot.Name), _resourceLocator.GetSprite(slot.Icon));
                _slots.Add(new SlotDef(slot.Letter, def));
            }

            if (!IsSlotClaimed('\0'))
            {
                var defaultDef = new NodeDef(
                    AutoSlotIdBase - 999, 0, _localization.GetString(settings.DefaultSlotName), _resourceLocator.GetSprite(settings.DefaultSlotIcon));
                _slots.Add(new SlotDef('\0', defaultDef));
            }
        }
        private bool IsSlotClaimed(char letter)
        {
            var prefix = InventoryCategoryMatcher.WeaponGroup + ":";
            return _matcher.IsClaimed(prefix + letter) || _matcher.IsClaimed(prefix + char.ToLowerInvariant(letter));
        }

        private Sprite GetSprite(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            Sprite sprite;
            if (_spriteCache.TryGetValue(path, out sprite))
                return sprite;

            sprite = _resourceLocator.GetSprite(new SpriteId(path, SpriteId.Type.Default));
            if (sprite == null)
                sprite = _resourceLocator.GetSprite(path);

            _spriteCache[path] = sprite;
            return sprite;
        }

        private static InventoryCategoryNode CreateNode(NodeDef def)
        {
            return new InventoryCategoryNode { Id = def.Id, ParentId = def.ParentId, Name = def.Name, Icon = def.Icon };
        }

        private readonly struct SlotDef
        {
            public readonly char Letter;
            public readonly NodeDef Def;

            public SlotDef(char letter, NodeDef def)
            {
                Letter = letter;
                Def = def;
            }
        }

        private readonly struct NodeDef
        {
            public readonly int Id;
            public readonly int ParentId;
            public readonly string Name;
            public readonly Sprite Icon;

            public NodeDef(int id, int parentId, string name, Sprite icon)
            {
                Id = id;
                ParentId = parentId;
                Name = name;
                Icon = icon;
            }
        }
    }
}