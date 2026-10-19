using UnityEngine;
using UnityEngine.UI;

namespace ShipEditor.UI
{
    public interface IShipCategoryHandler
    {
        void OpenCategory(CategoryData category);
        void GoBack();
    }

    public class ShipGroupListItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _expandIcon;
        [SerializeField] private GameObject _collapseIcon;
        [SerializeField] private Text _nameText;
        [SerializeField] private Button _button;

        private CategoryData _category;
        private IShipCategoryHandler _handler;
        private bool _isBackButton;
        private Color? _originalIconColor;

        private void Awake()
        {
            if (_button)
                _button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (_button)
                _button.onClick.RemoveListener(OnClick);
        }

        public void Initialize(CategoryData category, IShipCategoryHandler handler, bool isBackButton = false)
        {
            _category = category;
            _handler = handler;
            _isBackButton = isBackButton;

            if (_nameText)
                _nameText.text = category.Title;

            UpdateIcon(category);

            if (_expandIcon)
                _expandIcon.SetActive(!isBackButton);
            if (_collapseIcon)
                _collapseIcon.SetActive(isBackButton);
        }

        private void UpdateIcon(CategoryData category)
        {
            if (!_icon)
                return;

            if (_originalIconColor == null)
                _originalIconColor = _icon.color;

            var hasIcon = category.Icon != null;
            _icon.gameObject.SetActive(hasIcon);
            if (!hasIcon)
                return;

            _icon.sprite = category.Icon;

            // Ship icons keep their own colors; folders and weapon slots use the theme color.
            if (category.Color.HasValue)
                _icon.color = category.Color.Value;
            else if (category.Ships != null && category.Ships.Count > 0)
                _icon.color = Color.white;
            else
                _icon.color = _originalIconColor.Value;
        }

        private void OnClick()
        {
            if (_handler == null)
                return;

            if (_isBackButton)
                _handler.GoBack();
            else
                _handler.OpenCategory(_category);
        }
    }
}