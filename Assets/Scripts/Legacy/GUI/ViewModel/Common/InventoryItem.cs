using Economy.ItemType;
using Economy.Products;
using Services.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace ViewModel.Common
{
    public class InventoryItem : MonoBehaviour
    {
        [SerializeField] private Image Icon;
        [SerializeField] private Text NameText;
        [SerializeField] private Text DescriptionText;
        [SerializeField] private GameObject QuantityPanel;
        [SerializeField] private Text QuantityText;
        [SerializeField] private RectTransform OutOfStockPanel;
        [SerializeField] private GameObject RankPanel;
        [SerializeField] private Text RankText;
        [SerializeField] private PricePanel PricePanel;
        [SerializeField] private Selectable Selectable;

        public IProduct Product { get; private set; }

        public void Initialize(IProduct item, bool notEnoughMoney, IResourceLocator resourceLocator)
        {
            Product = item;
            var type = item.Type;
            var color = Gui.Theme.UiTheme.Current.GetQualityColor(type.Quality);

            Icon.sprite = resourceLocator.GetSprite(type.Icon);
            Icon.color = type.Color;

            NameText.text = type.Name;
            NameText.color = color;

            DescriptionText.text = type.Description;
            DescriptionText.color = color;
            DescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(type.Description));

            if (PricePanel != null)
                PricePanel.Initialize(type, item.Price, notEnoughMoney);

            if (QuantityPanel != null)
            {
                QuantityText.text = item.Quantity.ToString();
                QuantityPanel.SetActive(item.Quantity > 1);
            }

            if (OutOfStockPanel != null)
                OutOfStockPanel.gameObject.SetActive(!notEnoughMoney && item.Quantity <= 0);

            if (Selectable != null)
                Selectable.interactable = !notEnoughMoney;

            UpdateRank(type as ShipItemBase);
        }

        private void UpdateRank(ShipItemBase ship)
        {
            if (RankPanel == null)
                return;

            var hasRank = ship != null && ship.Rank >= 1;
            RankPanel.SetActive(hasRank);

            if (hasRank)
                RankText.text = ship.Rank.ToString();
        }
    }
}