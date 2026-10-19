using System.Linq;
using CommonComponents;
using Economy;
using Economy.ItemType;
using Economy.Products;
using GameDatabase;
using GameDatabase.Enums;
using GameModel.Quests;
using GameServices.Gui;
using GameServices.Player;
using Gui.StarMap;
using Services.Audio;
using Services.Gui;
using Services.Localization;
using Services.Messenger;
using Services.Resources;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Zenject;

namespace ViewModel.Dialogs
{
    public class MarketDialog : MonoBehaviour
    {
        [Inject] private readonly IMessenger _messenger;
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly ISoundPlayer _soundPlayer;
        [Inject] private readonly PlayerResources _playerResources;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly GuiHelper _helper;
        [Inject] private readonly IDatabase _database;

        [SerializeField] private ToggleGroup ItemsGroup;
        [SerializeField] private Toggle BuyToggle;
        [SerializeField] private Toggle SellToggle;
        [SerializeField] private AudioClip Sound;
        [SerializeField] private Button BuyButton;
        [SerializeField] private Button SellButton;
        [SerializeField] private Button _sellJunkButton;
        [SerializeField] private GameObject QuantityPanel;
        [SerializeField] private Slider QuantitySlider;
        [SerializeField] private Common.PricePanel PricePanel;
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

        [SerializeField] private ItemContentFiller ContentFiller;
        [SerializeField] private ListScrollRect ItemList;

        private enum Mode { Buy, Sell }

        private Mode _mode = Mode.Buy;
        private int _quantity = 1;
        private IInventory _playerInventory;
        private IInventory _marketInventory;
        private IProduct _selectedProduct;
        private bool _premiumEnabled;
        private bool _subscribed;

        private bool IsInitialized => _marketInventory != null && _playerInventory != null;

        private void Awake()
        {
            if (ContentFiller == null)
                ContentFiller = GetComponentInChildren<ItemContentFiller>(true);
            if (ItemList == null)
                ItemList = GetComponentInChildren<ListScrollRect>(true);
        }

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

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                CloseButtonClicked();
        }

        public void Initialize(WindowArgs args)
        {
            _marketInventory = args.Get<IInventory>(0);
            _playerInventory = args.Get<IInventory>(1);
            _premiumEnabled = _database.EconomySettings == null || _database.EconomySettings.EnablePremiumCurrency;

            Subscribe();

            DescriptionPanel.SetActive(false);
            SellToggle.SetIsOnWithoutNotify(false);
            BuyToggle.SetIsOnWithoutNotify(true);
            SetMode(Mode.Buy);
        }

        public void OnItemSelected(Common.InventoryItem item)
        {
            _selectedProduct = item.Product;
            _quantity = 1;
            ContentFiller.OnItemSelected(item);
            UpdateButtons();
        }

        public void OnItemDeselected()
        {
            _selectedProduct = null;
            UpdateButtons();
        }

        public void MoreInfoButtonClicked()
        {
            if (_selectedProduct != null)
                _helper.ShowItemInfoWindow(_selectedProduct);
        }

        public void BuyButtonClicked()
        {
            if (_selectedProduct == null)
                return;

            _selectedProduct.Buy(_quantity);
            CompleteTrade();
        }

        public void SellButtonClicked()
        {
            if (_selectedProduct == null)
                return;

            _selectedProduct.Sell(_quantity);
            CompleteTrade();
        }

        public void SellJunkButtonClicked()
        {
            _helper.ShowConfirmation(_localization.GetString("$SellAllTrashConfirmation"), SellJunk);
        }

        public void CloseButtonClicked()
        {
            if (ContentFiller != null && ContentFiller.TryNavigateBack())
                return;

            GetComponent<IWindow>().Close();
        }

        public void OnBuyToggleValueChanged(bool selected)
        {
            if (selected && IsInitialized)
                SetMode(Mode.Buy);
        }

        public void OnSellToggleValueChanged(bool selected)
        {
            if (selected && IsInitialized)
                SetMode(Mode.Sell);
        }

        public void OnQuantityChanged(float value)
        {
            _quantity = Mathf.RoundToInt(value);
            QuantityText.text = _quantity.ToString();

            if (_selectedProduct != null)
                PricePanel.Initialize(_selectedProduct.Type, _selectedProduct.Price * _quantity);
        }

        private void SellJunk()
        {
            foreach (var product in _playerInventory.Items.Where(IsJunk).ToArray())
                product.Sell(product.Quantity);

            CompleteTrade();
        }

        private void CompleteTrade()
        {
            _soundPlayer.Play(Sound);
            UpdateItems(false);
        }

        private static bool IsJunk(IProduct product)
        {
            if (product.Quantity <= 0 || product.Type is ArtifactItem)
                return false;

            return product.Type.Quality < ItemQuality.Common;
        }

        private void SetMode(Mode mode)
        {
            _mode = mode;
            _selectedProduct = null;

            if (ItemsGroup != null)
                ItemsGroup.SetAllTogglesOff();

            UpdateItems(true);
        }

        private void Subscribe()
        {
            if (_subscribed)
                return;

            _messenger.AddListener(EventType.IapItemsRefreshed, OnIapItemsChanged);
            _messenger.AddListener<Money>(EventType.MoneyValueChanged, OnCurrencyChanged);
            _messenger.AddListener<Money>(EventType.StarsValueChanged, OnCurrencyChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _messenger.RemoveListener(EventType.IapItemsRefreshed, OnIapItemsChanged);
            _messenger.RemoveListener<Money>(EventType.MoneyValueChanged, OnCurrencyChanged);
            _messenger.RemoveListener<Money>(EventType.StarsValueChanged, OnCurrencyChanged);
            _subscribed = false;
        }

        private void OnCurrencyChanged(Money value)
        {
            UpdateStats();
        }

        private void OnIapItemsChanged()
        {
            _marketInventory.Refresh();
            UpdateItems(false);
        }

        private void OnContentListChanged()
        {
            ItemList.RefreshContent();

            if (!IsInitialized)
                return;

            _selectedProduct = ContentFiller.SelectedProduct;
            UpdateButtons();
        }

        private void UpdateItems(bool clearSelection)
        {
            var inventory = _mode == Mode.Buy ? _marketInventory : _playerInventory;
            ContentFiller.InitializeItems(inventory.Items, _mode == Mode.Sell, clearSelection);
            ItemList.RefreshContent();

            if (EmptyLabel != null)
                EmptyLabel.SetActive(ContentFiller.GetItemCount() == 0);

            RestoreSelection();
            UpdateButtons();
            UpdateStats();
        }

        // The list is virtualized, so the selection comes from data, not from toggles that happen to be on screen.
        private void RestoreSelection()
        {
            _selectedProduct = ContentFiller.SelectedProduct;
        }

        private void UpdateStats()
        {
            MoneyText.text = _playerResources.Money.ToString();
            StarPanel.SetActive(_premiumEnabled);
            StarText.text = _playerResources.Stars.ToString();
        }

        private void UpdateButtons()
        {
            var buying = _mode == Mode.Buy;

            BuyButton.gameObject.SetActive(buying);
            SellButton.gameObject.SetActive(!buying);
            _sellJunkButton.gameObject.SetActive(!buying);

            if (buying)
            {
                BuyButton.interactable = CanBuySelected();
            }
            else
            {
                SellButton.interactable = _selectedProduct != null;
                _sellJunkButton.interactable = _playerInventory.Items.Any(IsJunk);
            }

            UpdateQuantity(GetMaxQuantity());
            UpdateItemDescription(_selectedProduct);
        }

        private bool CanBuySelected()
        {
            return _selectedProduct != null &&
                   _selectedProduct.Price.IsEnough(_playerResources) &&
                   _selectedProduct.Type.MaxItemsToConsume > 0;
        }

        private int GetMaxQuantity()
        {
            if (_selectedProduct == null)
                return 0;

            if (_mode == Mode.Sell)
                return _selectedProduct.Quantity;

            var max = Mathf.Min(_selectedProduct.Quantity, _selectedProduct.Type.MaxItemsToConsume);
            if (_selectedProduct.Price.Amount > 0)
                max = Mathf.Min(max, _selectedProduct.Price.GetMaxItemsToWithdraw(_playerResources));

            return max;
        }

        private void UpdateQuantity(int max)
        {
            var visible = max > 1;
            QuantityPanel.SetActive(visible);
            QuantitySlider.gameObject.SetActive(visible);

            if (!visible)
            {
                _quantity = 1;
                return;
            }

            QuantitySlider.maxValue = max;
            QuantitySlider.SetValueWithoutNotify(1);
            OnQuantityChanged(1);
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
        }
    }
}