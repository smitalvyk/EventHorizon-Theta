using System.Linq;
using Economy.Products;
using GameDatabase;
using GameModel.Quests;
using GameServices.Gui;
using GameServices.Player;
using Services.Audio;
using Services.Gui;
using Services.Localization;
using Services.Resources;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using ViewModel.Common;
using Zenject;

namespace Gui.StarMap
{
    public class CargoHold : MonoBehaviour
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly ISoundPlayer _soundPlayer;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly GuiHelper _helper;
        [Inject] private readonly InventoryFactory _inventoryFactory;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly IDatabase _database;

        [SerializeField] private ToggleGroup ItemsGroup;
        [SerializeField] private AudioClip Sound;
        [SerializeField] private GameObject QuantityPanel;
        [SerializeField] private Slider QuantitySlider;
        [SerializeField] private PricePanel PricePanel;
        [SerializeField] private GameObject DescriptionPanel;
        [SerializeField] private Text QuantityText;
        [SerializeField] private Text NameText;
        [FormerlySerializedAs("DescrtiptionText")]
        [SerializeField] private Text DescriptionText;
        [SerializeField] private Image Icon;
        [SerializeField] private GameObject EmptyLabel;
        [SerializeField] private Text MoneyText;
        [SerializeField] private Text StarText;
        [SerializeField] private GameObject StarPanel;
        [SerializeField] private Button ScrapButton;

        [SerializeField] private ItemContentFiller ContentFiller;
        [SerializeField] private ListScrollRect ItemList;

        private int _quantity = 1;
        private IInventory _inventory;
        private IProduct _selectedProduct;

        private void OnEnable()
        {
            if (ContentFiller != null)
                ContentFiller.OnListChanged += OnContentListChanged;
        }

        private void OnDisable()
        {
            if (ContentFiller != null)
                ContentFiller.OnListChanged -= OnContentListChanged;
        }

        public void Initialize(WindowArgs args)
        {
            _inventory = _inventoryFactory.CreateCargoHoldInventory();
            DescriptionPanel.SetActive(false);

            if (ItemsGroup != null)
                ItemsGroup.SetAllTogglesOff();

            UpdateItems(true);
        }

        public void OnItemSelected(InventoryItem item)
        {
            _selectedProduct = item.Product;
            _quantity = 1;
            ContentFiller.OnItemSelected(item);
            UpdateButtons();
        }

        public void OnItemDeselected()
        {
            _selectedProduct = null;
            ContentFiller.OnItemSelected(null);
            UpdateButtons();
        }

        public void MoreInfoButtonClicked()
        {
            if (_selectedProduct != null)
                _helper.ShowItemInfoWindow(_selectedProduct);
        }

        public void ScrapButtonClicked()
        {
            if (_selectedProduct == null)
                return;

            _selectedProduct.Sell(_quantity);
            _soundPlayer.Play(Sound);
            UpdateItems(false);
        }

        // Wire the exit button to this method: it steps back through search and categories before closing.
        public void CloseButtonClicked()
        {
            if (ContentFiller != null && ContentFiller.TryNavigateBack())
                return;

            var window = GetComponent<IWindow>();
            if (window != null)
                window.Close();
        }

        public void OnQuantityChanged(float value)
        {
            _quantity = Mathf.RoundToInt(value);
            QuantityText.text = _quantity.ToString();

            if (_selectedProduct != null)
                PricePanel.Initialize(_selectedProduct.Type, _selectedProduct.Price * _quantity);
        }

        private void OnContentListChanged()
        {
            ItemList.RefreshContent();

            _selectedProduct = ContentFiller.SelectedProduct;
            UpdateButtons();
        }

        private void UpdateItems(bool clearSelection)
        {
            ContentFiller.InitializeItems(_inventory.Items, true, clearSelection);
            ItemList.RefreshContent();

            if (EmptyLabel != null)
                EmptyLabel.SetActive(ContentFiller.GetItemCount() == 0);

            // The list is virtualized, so the selection comes from data, not from toggles that happen to be on screen.
            _selectedProduct = ContentFiller.SelectedProduct;

            UpdateButtons();
            UpdateStats();
        }

        private void UpdateStats()
        {
            var premiumEnabled = _database.EconomySettings == null || _database.EconomySettings.EnablePremiumCurrency;

            MoneyText.text = _playerResources.Money.ToString();
            StarPanel.SetActive(premiumEnabled);
            StarText.text = _playerResources.Stars.ToString();
        }

        private void UpdateButtons()
        {
            var quantity = _selectedProduct != null ? _selectedProduct.Quantity : 0;
            var canScrap = _selectedProduct != null && _selectedProduct.Price.Amount > 0;
            var canScrapMultiple = canScrap && quantity > 1;

            ScrapButton.gameObject.SetActive(canScrap);
            ScrapButton.interactable = canScrap;
            QuantityPanel.SetActive(canScrapMultiple);

            if (QuantitySlider != null && QuantitySlider.transform.parent != null)
                QuantitySlider.transform.parent.gameObject.SetActive(canScrapMultiple);

            if (canScrapMultiple)
            {
                QuantitySlider.maxValue = quantity;
                QuantitySlider.SetValueWithoutNotify(1);
                OnQuantityChanged(1);
            }
            else
            {
                _quantity = 1;
            }

            UpdateItemDescription(_selectedProduct);
        }

        private void UpdateItemDescription(IProduct product)
        {
            DescriptionPanel.SetActive(product != null);
            if (product == null)
                return;

            var type = product.Type;
            var color = Gui.Theme.UiTheme.Current.GetQualityColor(type.Quality);

            Icon.sprite = _resourceLocator.GetSprite(type.Icon);
            Icon.color = type.Color;

            NameText.text = _localization.GetString(type.Name);
            NameText.color = color;

            DescriptionText.text = type.Description;
            DescriptionText.color = color;
            DescriptionText.gameObject.SetActive(!string.IsNullOrEmpty(type.Description));

            PricePanel.Initialize(type, product.Price);
            PricePanel.gameObject.SetActive(product.Price.Amount > 0);
        }
    }
}