using System.Collections.Generic;
using Constructor.Ships;
using UnityEngine;

namespace ShipEditor.UI
{
    public class CategoryData
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public Sprite Icon { get; set; }
        public Color? Color { get; set; }
        public CategoryData Parent { get; set; }
        public List<CategoryData> SubCategories { get; set; } = new List<CategoryData>();
        public List<IShip> Ships { get; set; } = new List<IShip>();

        public string Title => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;

        public void AddSubCategory(CategoryData child)
        {
            child.Parent = this;
            SubCategories.Add(child);
        }

        public void CollectShips(List<IShip> result)
        {
            result.AddRange(Ships);
            foreach (var sub in SubCategories)
                sub.CollectShips(result);
        }
    }
}