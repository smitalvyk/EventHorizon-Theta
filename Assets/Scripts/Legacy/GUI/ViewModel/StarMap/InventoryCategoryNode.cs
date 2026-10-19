using System.Collections.Generic;
using Economy.Products;
using ShipEditor.UI;

namespace Gui.StarMap
{
    public class InventoryCategoryNode : CategoryData
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public List<IProduct> Items { get; } = new List<IProduct>();
        public int TotalItemCount { get; private set; }

        // Removes empty branches, caches counts and the display title. Returns false if the node is empty.
        public bool Seal()
        {
            var total = Items.Count;

            for (var i = SubCategories.Count - 1; i >= 0; i--)
            {
                var child = SubCategories[i] as InventoryCategoryNode;
                if (child != null && child.Seal())
                {
                    total += child.TotalItemCount;
                    continue;
                }

                SubCategories.RemoveAt(i);
            }

            TotalItemCount = total;
            DisplayName = total > 0 ? $"{Name} ({total})" : Name;
            return total > 0;
        }

        public void CollectItems(List<IProduct> result)
        {
            result.AddRange(Items);
            foreach (var sub in SubCategories)
            {
                var child = sub as InventoryCategoryNode;
                if (child != null)
                    child.CollectItems(result);
            }
        }
    }
}