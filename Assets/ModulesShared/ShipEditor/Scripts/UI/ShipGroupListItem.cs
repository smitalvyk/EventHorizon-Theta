using UnityEngine;
using UnityEngine.UI;

namespace ShipEditor.UI
{
    public class ShipGroupListItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private GameObject _expandIcon;
        [SerializeField] private GameObject _collapseIcon;
        [SerializeField] private Text _nameText;
        [SerializeField] private Button _button;

        private CategoryData _category;
        private ShipListContentFiller _filler;
        private bool _isBackButton;

        private Color? _originalIconColor = null;

        public void Initialize(CategoryData category, ShipListContentFiller filler, bool isBackButton = false)
        {
            _category = category;
            _filler = filler;
            _isBackButton = isBackButton;

            if (_nameText) _nameText.text = category.Name;

            if (_icon)
            {
                // Cache initial prefab color
                if (_originalIconColor == null)
                    _originalIconColor = _icon.color;

                bool hasIcon = category.Icon != null;
                _icon.gameObject.SetActive(hasIcon);

                if (hasIcon)
                {
                    _icon.sprite = category.Icon;
                    // Blue for root, white for sub-categories
                    _icon.color = category.Parent == null ? _originalIconColor.Value : Color.white;
                }
            }

            if (_expandIcon) _expandIcon.SetActive(!isBackButton);
            if (_collapseIcon) _collapseIcon.SetActive(isBackButton);

            if (_button)
            {
                _button.onClick.RemoveAllListeners();
                _button.onClick.AddListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (_isBackButton)
                _filler.GoBack();
            else
                _filler.OpenCategory(_category);
        }
    }
}