using System;
using System.Collections.Generic;
using System.Linq;

namespace Gui.StarMap
{
    public enum ItemKind
    {
        Other,
        Component,
        Satellite,
        Ship,
        Artifact,
        Fuel
    }

    public readonly struct ItemFacts
    {
        public readonly ItemKind Kind;
        public readonly string ComponentGroup;
        public readonly char WeaponSlot;
        public readonly string TypeId;

        public ItemFacts(ItemKind kind, string componentGroup, char weaponSlot, string typeId)
        {
            Kind = kind;
            ComponentGroup = componentGroup;
            WeaponSlot = weaponSlot;
            TypeId = typeId;
        }
    }

    public readonly struct CategoryFill
    {
        public readonly int CategoryId;
        public readonly IEnumerable<string> Keys;

        public CategoryFill(int categoryId, IEnumerable<string> keys)
        {
            CategoryId = categoryId;
            Keys = keys;
        }
    }

    // Maps an item to a category id through "Fill" keys. The most specific key wins; on a duplicate key the lowest category id wins.
    // Keys: item:<typeId>, weapon:<slot>, weapon, defend, energy, engine, drone, special, component, satellite, ship, artifact, fuel.
    public sealed class InventoryCategoryMatcher
    {
        public const string WeaponGroup = "weapon";
        public const string ItemPrefix = "item:";

        private readonly Dictionary<string, int> _owners = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly List<string> _conflicts = new List<string>();

        public InventoryCategoryMatcher(IEnumerable<CategoryFill> fills)
        {
            foreach (var fill in fills.OrderBy(f => f.CategoryId))
            {
                if (fill.Keys == null)
                    continue;

                foreach (var raw in fill.Keys)
                {
                    var key = Normalize(raw);
                    if (key.Length == 0)
                        continue;

                    int owner;
                    if (!_owners.TryGetValue(key, out owner))
                        _owners[key] = fill.CategoryId;
                    else if (owner != fill.CategoryId)
                        _conflicts.Add($"Key '{key}' is claimed by categories {owner} and {fill.CategoryId}; {owner} wins");
                }
            }
        }

        public IReadOnlyList<string> Conflicts => _conflicts;

        // Returns 0 when no category claims the item.
        public int Resolve(ItemFacts facts)
        {
            string key;
            return Resolve(facts, out key);
        }

        // Also reports which key matched, so callers can tell a group match from a specific one.
        public int Resolve(ItemFacts facts, out string matchedKey)
        {
            foreach (var key in Keys(facts))
            {
                int id;
                if (_owners.TryGetValue(key, out id))
                {
                    matchedKey = key;
                    return id;
                }
            }

            matchedKey = null;
            return 0;
        }

        // Returns the id of the category that owns the key, or 0.
        public int GetOwner(string key)
        {
            int id;
            return _owners.TryGetValue(Normalize(key), out id) ? id : 0;
        }

        public bool IsClaimed(string key)
        {
            return GetOwner(key) != 0;
        }

        // Most specific first.
        public static IEnumerable<string> Keys(ItemFacts facts)
        {   
            if (!string.IsNullOrEmpty(facts.TypeId))
                yield return ItemPrefix + Normalize(facts.TypeId);

            switch (facts.Kind)
            {
                case ItemKind.Component:
                    if (!string.IsNullOrEmpty(facts.ComponentGroup))
                    {
                        if (facts.ComponentGroup == WeaponGroup)
                        {
                            if (facts.WeaponSlot == '-' || facts.WeaponSlot == '\0')
                            {
                                yield return WeaponGroup + ":all";
                                yield return WeaponGroup + ":-";
                            }
                            else
                            {
                                // Exact slot letter, then lowercase fallback
                                yield return WeaponGroup + ":" + facts.WeaponSlot;

                                var lower = char.ToLowerInvariant(facts.WeaponSlot);
                                if (lower != facts.WeaponSlot)
                                    yield return WeaponGroup + ":" + lower;
                            }
                        }

                        yield return facts.ComponentGroup;
                    }

                    yield return "component";
                    break;

                case ItemKind.Satellite:
                    yield return "satellite";
                    break;

                case ItemKind.Ship:
                    yield return "ship";
                    break;

                case ItemKind.Artifact:
                    yield return "artifact";
                    break;

                case ItemKind.Fuel:
                    yield return "fuel";
                    break;
            }
        }

        // Keys are case-insensitive, except the slot letter in "weapon:<slot>".
        private static string Normalize(string key)
        {
            if (key == null)
                return string.Empty;

            var trimmed = key.Trim();
            if (!trimmed.StartsWith(WeaponGroup + ":", StringComparison.OrdinalIgnoreCase))
                return trimmed.ToLowerInvariant();

            return WeaponGroup + ":" + trimmed.Substring(WeaponGroup.Length + 1).Trim();
        }
    }
}