using System.Collections.Generic;
using System.Linq;
using Combat.Component.Ship;
using Combat.Domain;
using Services.Localization;
using Services.Resources;
using UnityEngine;
using UnityEngine.UI;
using ViewModel;
using Zenject;

namespace Gui.Combat
{
    public class ShipList : MonoBehaviour
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IResourceLocator _resourceLocator;

        [SerializeField] private ScrollRect ShipsArea;
        [SerializeField] private CanvasGroup CanvasGroup;
        [SerializeField] private Text ShipNameText;

        [Header("Silhouette")]
        [SerializeField] private Color _silhouetteColor = Color.black;

        public IShipInfo SelectedShip => _selectedIndex >= 0 ? _fleet.Ships[_selectedIndex] : null;

        public int SelectedShipIndex
        {
            get => _selectedIndex;
            set
            {
                _targetIndex = Mathf.Clamp(value, 0, _maxScrollIndex);
                UpdateSelection();
            }
        }

        private int _maxScrollIndex = 99;
        private int _maxNormalIndex = 99;
        private Material _defaultShipMaterial;
        private bool _isClamping;

        public void Initialize(IFleetModel fleet, int activeShipIndex, int maxScrollAllowed = 99, int maxNormalIndex = 99, int maxListCount = 24)
        {
            _fleet = fleet;
            _maxScrollIndex = maxScrollAllowed;
            _maxNormalIndex = maxNormalIndex;

            if (activeShipIndex < 0)
            {
                activeShipIndex = fleet.Ships.FindIndex(s => s.Status != ShipStatus.Destroyed);
                if (activeShipIndex < 0) activeShipIndex = 0;
            }

            _targetIndex = activeShipIndex;

            IEnumerator<IShipInfo> enumerator;
            if (fleet.Ships.Count <= maxListCount)
            {
                _firstShipIndex = 0;
                enumerator = fleet.Ships.Take(maxListCount).GetEnumerator();
            }
            else
            {
                _firstShipIndex = Mathf.Max(_targetIndex - maxListCount / 2, 0);
                enumerator = fleet.Ships.Skip(_firstShipIndex).Take(maxListCount).GetEnumerator();
            }

            int currentShipIndex = _firstShipIndex;

            RectTransform item = null;
            foreach (Transform transform in ShipsArea.content)
            {
                item = transform.GetComponent<RectTransform>();
                if (enumerator.MoveNext())
                {
                    bool isSilhouette = currentShipIndex > _maxNormalIndex;
                    UpdateShipItem(item, enumerator.Current, isSilhouette);
                    currentShipIndex++;
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }

            while (enumerator.MoveNext())
            {
                var newItem = (RectTransform)Instantiate(item);
                newItem.SetParent(item.parent);
                newItem.localScale = Vector3.one;

                bool isSilhouette = currentShipIndex > _maxNormalIndex;
                UpdateShipItem(newItem, enumerator.Current, isSilhouette);
                currentShipIndex++;
            }

            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(ShipsArea.content);

            ShipsArea.horizontalNormalizedPosition = 0f;
            ShipsArea.velocity = Vector2.zero;

            UpdateSelection();
        }

        public void OnContentMoved(Vector2 position)
        {
            ClampScrollPosition();
            UpdateSelection();
        }

        private void UpdateSelection()
        {
            var areaWidth = ShipsArea.GetComponent<RectTransform>().rect.width;
            var currentPosition = ShipsArea.horizontalNormalizedPosition * (ShipsArea.content.rect.width - areaWidth) + areaWidth / 2;

            if (_targetIndex >= 0)
            {
                var item = ShipsArea.content.GetChild(_targetIndex - _firstShipIndex).GetComponent<RectTransform>();
                _selectedIndex = _targetIndex;
                _selectedDistance = currentPosition - item.anchoredPosition.x;

                if (Mathf.Abs(_selectedDistance) < areaWidth * 0.01f)
                    _targetIndex = -1;
            }
            else
            {
                var deltaMin = float.MaxValue;
                int index = _firstShipIndex;
                _selectedIndex = -1;

                foreach (RectTransform item in ShipsArea.content)
                {
                    if (!item.gameObject.activeSelf)
                        continue;

                    var delta = currentPosition - item.anchoredPosition.x;

                    // Restrict snapping to ships within allowed scroll range
                    if (index <= _maxScrollIndex)
                    {
                        if (Mathf.Abs(delta) < Mathf.Abs(deltaMin) && _fleet.Ships[index].Status != ShipStatus.Destroyed)
                        {
                            deltaMin = delta;
                            _selectedIndex = index;
                        }
                    }
                    index++;
                }

                if (_selectedIndex == -1)
                {
                    index = _firstShipIndex;
                    foreach (RectTransform item in ShipsArea.content)
                    {
                        if (!item.gameObject.activeSelf) continue;
                        if (index <= _maxScrollIndex)
                        {
                            var delta = currentPosition - item.anchoredPosition.x;
                            if (Mathf.Abs(delta) < Mathf.Abs(deltaMin))
                            {
                                deltaMin = delta;
                                _selectedIndex = index;
                            }
                        }
                        index++;
                    }
                }

                _selectedDistance = deltaMin;
            }

            if (ShipNameText != null && _selectedIndex >= 0)
            {
                if (_selectedIndex > _maxNormalIndex)
                    ShipNameText.text = "???";
                else
                    ShipNameText.text = _localization.GetString(_fleet.Ships[_selectedIndex].ShipData.Name);
            }
        }

        private void UpdateShipItem(RectTransform item, IShipInfo ship, bool isSilhouette)
        {
            item.gameObject.SetActive(true);
            item.localScale = Vector3.one;
            var viewModel = item.GetComponent<SelectShipPanelItemViewModel>();

            viewModel.Icon.sprite = _resourceLocator.GetSprite(ship.ShipData.Model.ModelImage);

            if (_defaultShipMaterial == null && viewModel.Icon.material != null)
                _defaultShipMaterial = viewModel.Icon.material;

            if (isSilhouette)
            {
                // Disabling material flattens sprite to solid silhouette
                viewModel.Icon.material = null;
                viewModel.Icon.color = _silhouetteColor;

                viewModel.DisabledIcon.gameObject.SetActive(false);
                viewModel.ConditionText.gameObject.SetActive(false);
                viewModel.SetClass(0);
                viewModel.SetLevel(0);
            }
            else
            {
                viewModel.Icon.material = _defaultShipMaterial;
                viewModel.Icon.color = ship.ShipData.ColorScheme.HsvColor;

                viewModel.DisabledIcon.gameObject.SetActive(ship.Status == ShipStatus.Destroyed);
                viewModel.ConditionText.gameObject.SetActive(ship.Condition > 0 && ship.Condition < 1);
                viewModel.ConditionText.text = Mathf.FloorToInt(ship.Condition * 100) + "%";
                viewModel.SetClass(ship.ShipData.ExtraThreatLevel);
                viewModel.SetLevel(ship.ShipData.Experience.Level);
            }
        }

        private void Update()
        {
            var delta = _selectedDistance * 5;
            var x = Mathf.Lerp(ShipsArea.velocity.x, delta, 0.1f);
            ShipsArea.velocity = Mathf.Abs(x) > 1 ? new Vector2(x, 0) : Vector2.zero;
        }

        private void LateUpdate()
        {
            ClampScrollPosition();
        }

        private void ClampScrollPosition()
        {
            // Do not restrict while initial centering is in progress
            if (_targetIndex >= 0)
                return;

            if (_isClamping || ShipsArea == null || ShipsArea.content == null || ShipsArea.content.childCount == 0)
                return;

            var areaWidth = ShipsArea.GetComponent<RectTransform>().rect.width;
            var contentWidth = ShipsArea.content.rect.width;
            var scrollableWidth = contentWidth - areaWidth;

            if (scrollableWidth <= 0.001f)
                return;

            var firstItem = ShipsArea.content.GetChild(0).GetComponent<RectTransform>();
            int maxChildIndex = Mathf.Clamp(_maxScrollIndex - _firstShipIndex, 0, ShipsArea.content.childCount - 1);
            var maxItem = ShipsArea.content.GetChild(maxChildIndex).GetComponent<RectTransform>();

            if (firstItem == null || maxItem == null)
                return;

            float minNorm = (firstItem.anchoredPosition.x - areaWidth / 2f) / scrollableWidth;
            float maxNorm = (maxItem.anchoredPosition.x - areaWidth / 2f) / scrollableWidth;

            if (minNorm > maxNorm)
            {
                float temp = minNorm;
                minNorm = maxNorm;
                maxNorm = temp;
            }

            // Right clamp limit
            if (ShipsArea.horizontalNormalizedPosition > maxNorm)
            {
                _isClamping = true;
                ShipsArea.horizontalNormalizedPosition = maxNorm;
                if (ShipsArea.velocity.x < 0)
                    ShipsArea.velocity = Vector2.zero;
                _isClamping = false;
            }
            // Left clamp limit
            else if (ShipsArea.horizontalNormalizedPosition < minNorm)
            {
                _isClamping = true;
                ShipsArea.horizontalNormalizedPosition = minNorm;
                if (ShipsArea.velocity.x > 0)
                    ShipsArea.velocity = Vector2.zero;
                _isClamping = false;
            }
        }

        private int _targetIndex = -1;
        private float _selectedDistance;
        private int _selectedIndex;
        private int _firstShipIndex;
        private IFleetModel _fleet;
    }
}