using Constructor.Ships;
using Services.Localization;
using Services.Resources;
using UnityEngine;
using UnityEngine.UI;
using GameDatabase.Enums;
using Zenject;

namespace ShipEditor.UI
{
    public class ShipItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _factionText;
        [SerializeField] private Text _classText;

        [Header("Standard Hangar Info")]
        [SerializeField] private GameObject _levelPanelRoot;
        [SerializeField] private Text _levelText;

        [Header("Sandbox Editor Icons")]
        [SerializeField] private GameObject _editorIconsPanel;
        [SerializeField] private Text _playerAccessText;
        [SerializeField] private Text _enemyAccessText;
        [SerializeField] private Text _difficultyText;
        [SerializeField] private Text _satelliteText;

        public void Initialize(IShip ship, IResourceLocator resourceLocator, ILocalization localization, GameDatabase.IDatabase database = null)
        {
            Ship = ship;

            var icon = resourceLocator.GetSprite(ship.Model.IconImage);
            if (icon != null)
            {
                _icon.sprite = icon;
                _icon.rectTransform.localScale = 1.4f * ship.Model.IconScale * Vector3.one;
            }
            else
            {
                _icon.sprite = resourceLocator.GetSprite(ship.Model.ModelImage);
                _icon.rectTransform.localScale = Vector3.one;
            }

            _icon.color = ship.ColorScheme.HsvColor;
            _name.text = localization.GetString(ship.Name);

            if (_factionText != null && _factionText.transform.parent != null)
            {
                bool hasFaction = ship.Model.Faction != null;
                _factionText.transform.parent.gameObject.SetActive(hasFaction);
                if (hasFaction) _factionText.text = localization.GetString(ship.Model.Faction.Name);
            }

            _classText.text = ship.Model.SizeClass.ToString(localization);

            bool isSandboxMode = !string.IsNullOrEmpty(ship.Name) && ship.Name.ToLower().Contains("shipbuild");

            if (isSandboxMode)
            {
                if (_levelPanelRoot != null) _levelPanelRoot.SetActive(false);
                if (_editorIconsPanel != null)
                {
                    _editorIconsPanel.SetActive(true);

                    string aiNumber = "0";
                    if (ship.CustomAi != null)
                    {
                        int aiId = ExtractNumberFromString(ship.CustomAi.Id.ToString());
                        if (aiId >= 0) aiNumber = aiId.ToString();
                    }

                    bool isForPlayer = true;
                    bool isForEnemy = true;
                    int diffClass = (int)ship.ExtraThreatLevel;

                    GameDatabase.IDatabase db = database ?? FindObjectOfType<SceneContext>()?.Container.Resolve<GameDatabase.IDatabase>();

                    if (db != null)
                    {
                        int buildId = ExtractNumberFromString(ship.Name);
                        if (buildId >= 0)
                        {
                            var buildData = db.GetShipBuild(new GameDatabase.Model.ItemId<GameDatabase.DataModel.ShipBuild>(buildId));
                            if (buildData != null)
                            {
                                isForPlayer = buildData.AvailableForPlayer;
                                isForEnemy = buildData.AvailableForEnemy;
                                diffClass = (int)buildData.DifficultyClass;
                            }
                        }
                    }

                    if (_playerAccessText != null) { _playerAccessText.text = isForPlayer ? "✔" : "X"; _playerAccessText.color = isForPlayer ? Color.green : Color.red; }
                    if (_enemyAccessText != null) { _enemyAccessText.text = isForEnemy ? "✔" : "X"; _enemyAccessText.color = isForEnemy ? Color.green : Color.red; }
                    if (_difficultyText != null) { _difficultyText.text = diffClass.ToString(); _difficultyText.color = diffClass > 0 ? Color.yellow : Color.white; }
                    if (_satelliteText != null) { _satelliteText.text = aiNumber; _satelliteText.color = aiNumber != "0" ? Color.cyan : Color.gray; }
                }
            }
            else
            {
                if (_editorIconsPanel != null) _editorIconsPanel.SetActive(false);
                if (_levelPanelRoot != null) _levelPanelRoot.SetActive(true);
                if (_levelText != null) _levelText.text = ship.Experience.Level > 0 ? ship.Experience.Level.ToString() : "0";
            }
        }

        // Extracts the first numeric block from the string without allocations
        private int ExtractNumberFromString(string text)
        {
            if (string.IsNullOrEmpty(text)) return -1;

            int result = 0;
            bool foundDigit = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    result = result * 10 + (c - '0');
                    foundDigit = true;
                }
                else if (foundDigit) break;
            }

            return foundDigit ? result : -1;
        }

        public IShip Ship { get; private set; }
    }
}