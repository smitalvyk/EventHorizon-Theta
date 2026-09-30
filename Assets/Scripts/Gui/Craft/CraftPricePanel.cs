using CommonComponents.Utils;
using DataModel.Technology;
using GameDatabase.DataModel;
using GameServices.Player;
using GameServices.Research;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace Gui.Craft
{
    public class CraftPricePanel : MonoBehaviour
    {
        [Inject] private readonly PlayerResources _resources;
        [Inject] private readonly Research _research;

        [SerializeField] private GameObject _starsPanel;
        [SerializeField] private Text _starsText;
        [SerializeField] private GameObject _creditsPanel;
        [SerializeField] private Text _creditsText;
        [SerializeField] private GameObject _techsPanel;
        [SerializeField] private Text _techsText;
        [SerializeField] private Image _techsImage;
        [SerializeField] private Color _enoughColor;
        [SerializeField] private Color _notEnoughColor;

        public void Initialize(CraftingPrice price, Faction faction)
        {
            const int PriceDigits = 7;

            // Credits
            _creditsPanel.SetActive(price.Credits > 0);
            _creditsText.text = price.Credits.ToString(PriceDigits);
            var enoughMoney = _resources.Money >= price.Credits;
            _creditsText.color = enoughMoney ? _enoughColor : _notEnoughColor;

            // Stars currency
            _starsPanel.SetActive(price.Stars > 0);
            _starsText.text = price.Stars.ToString(PriceDigits);
            var enoughStars = _resources.Stars >= price.Stars;
            _starsText.color = enoughStars ? _enoughColor : _notEnoughColor;

            // Tech points with available balance
            var targetFaction = (faction == null || faction == Faction.Empty || faction.Id.ToString() == "0")
                ? Faction.Empty
                : faction;

            int availableTechs = _research.GetAvailablePoints(targetFaction);
            if (availableTechs == 0 && targetFaction != Faction.Empty)
            {
                availableTechs = _research.GetAvailablePoints(Faction.Empty);
            }

            _techsPanel.SetActive(price.Techs > 0 || availableTechs > 0);
            _techsText.text = $"{ValueFormatter.AsMoney(price.Techs)} ({availableTechs})";

            var enoughTechs = availableTechs >= price.Techs;
            _techsText.color = enoughTechs ? _enoughColor : _notEnoughColor;

            if (_techsImage != null)
            {
                _techsImage.color = targetFaction.Color;
            }

            HaveEnoughResources = enoughTechs && enoughMoney && enoughStars;
        }

        public bool HaveEnoughResources { get; private set; }
    }
}