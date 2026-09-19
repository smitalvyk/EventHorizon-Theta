using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using Constructor;
using Constructor.Modification;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using Services.Localization;
using Services.Resources;
using Constructor.Component;
using GameDatabase.Extensions;
using ShipEditor.Model;
using Gui.Theme;
using GameDatabase;

namespace ShipEditor.UI
{
    public class ComponentItem : MonoBehaviour
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IDatabase _database;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly IShipEditorModel _shipEditor;

        [SerializeField] private Image _icon;
        [SerializeField] private Text _name;
        [SerializeField] private Text _modification;
        [SerializeField] private Sprite _emptyIcon;
        [SerializeField] private LayoutGroup _stats;
        [SerializeField] private Text _description;
        [SerializeField] private GameObject _descriptionBlock;

        [SerializeField] private Text _sizeText;
        [SerializeField] private Image _requiredCellIcon;
        [SerializeField] private Text _requiredCellText;

        [Header("Layout Shape Preview")]
        [SerializeField] private GridLayoutGroup _layoutGrid;
        [SerializeField] private Image _cellTemplate;

        [SerializeField] private Color _weaponCellColor;
        [SerializeField] private Color _outerCellColor;
        [SerializeField] private Color _innerCellColor;
        [SerializeField] private Color _engineCellColor;
        [SerializeField] private Color _emptyCellColor;

        public void Initialize(ComponentInfo component)
        {
            _requiredCellIcon.color = GetCellColor(component.Data.CellType);
            _requiredCellText.text = component.Data.CellType == CellType.Weapon ? SlotTypeToString(component.Data.WeaponSlotType) : string.Empty;
            _sizeText.text = component.Data.Layout.CellCount.ToString();

            UpdateDescription(component);
            UpdateLayoutPreview(component);
        }

        private Color GetCellColor(CellType cellType)
        {
            switch (cellType)
            {
                case CellType.Weapon: return _weaponCellColor;
                case CellType.Outer: return _outerCellColor;
                case CellType.Inner: return _innerCellColor;
                case CellType.InnerOuter: return Color.Lerp(_innerCellColor, _outerCellColor, 0.5f);
                case CellType.Engine: return _engineCellColor;
                case CellType.Empty:
                default:
                    return _emptyCellColor;
            }
        }

        private static string SlotTypeToString(char type)
        {
            if (type == default)
                return string.Empty;

            return type.ToString();
        }

        public void Clear()
        {
            _icon.sprite = _emptyIcon;
            _icon.color = Color.white;
            _name.text = "-";

            if (_layoutGrid != null)
                _layoutGrid.gameObject.SetActive(false);
        }

        private void UpdateDescription(ComponentInfo info)
        {
            var component = info.CreateComponent(_shipEditor.Ship.Model.Layout.CellCount);
            component.Upgrades = _shipEditor.UpgradesProvider.GetComponentUpgrades(info.Data);

            _name.text = info.GetName(_localization);
            _name.color = UiTheme.Current.GetQualityColor(info.ItemQuality);

            _icon.sprite = _resourceLocator.GetSprite(info.Data.Icon);
            _icon.color = info.Data.Color;

            var modification = component.Modification ?? EmptyModification.Instance;
            _modification.gameObject.SetActive(!string.IsNullOrEmpty(_modification.text = modification.GetDescription(_localization)));
            _modification.color = UiTheme.Current.GetQualityColor(info.ItemQuality);

            if (_descriptionBlock)
            {
                if (string.IsNullOrEmpty(info.Data.Description))
                    _descriptionBlock.SetActive(false);
                else
                {
                    _descriptionBlock.SetActive(true);
                    _description.text = _localization.Localize(info.Data.Description);
                }
            }

            if (_stats)
            {
                _stats.transform.InitializeElements<NameValueItem, KeyValuePair<string, string>>(
                    GetDescription(component, _localization, _database.LocalizationSettings), UpdateTextField);
            }
        }
        // Render the component layout centered strictly by active/visible cells
        private void UpdateLayoutPreview(ComponentInfo component)
        {
            if (_layoutGrid == null || _cellTemplate == null)
                return;

            _layoutGrid.gameObject.SetActive(true);

            // Clean up previous cells, keeping the template
            foreach (Transform child in _layoutGrid.transform)
            {
                if (child != _cellTemplate.transform)
                    Destroy(child.gameObject);
            }

            var layout = component.Data.Layout;
            string rawData = layout.Data ?? "1";

            string[] rawRows;
            if (rawData.Contains('\n'))
            {
                rawRows = rawData.Split('\n');
            }
            else
            {
                string cleanData = rawData.Trim();
                int totalLength = cleanData.Length;

                int n = Mathf.CeilToInt(Mathf.Sqrt(totalLength));
                n = Mathf.Max(n, 1);

                int h = Mathf.CeilToInt((float)totalLength / n);
                rawRows = new string[h];

                for (int y = 0; y < h; y++)
                {
                    int start = y * n;
                    int length = Mathf.Min(n, totalLength - start);
                    rawRows[y] = length > 0 ? cleanData.Substring(start, length) : string.Empty;
                }
            }

            // 1. Find bounding box of only active cells (ignoring empty outer rows/columns)
            int minX = int.MaxValue;
            int maxX = -1;
            int minY = int.MaxValue;
            int maxY = -1;

            for (int y = 0; y < rawRows.Length; y++)
            {
                var row = rawRows[y].TrimEnd('\r');
                for (int x = 0; x < row.Length; x++)
                {
                    char c = row[x];
                    if (c != '0' && c != ' ')
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }

            // Fallback if no active cells were found
            if (maxX == -1)
            {
                minX = maxX = 0;
                minY = maxY = 0;
            }

            // Trimmed visible dimensions
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            // 2. Cell sizing rule: 48x48 if <= 4x4, -4px per unit above 4
            int maxDimension = Mathf.Max(width, height);
            float baseCellSize = 48f;
            float cellSizeValue = baseCellSize;

            if (maxDimension > 3)
            {
                cellSizeValue -= (maxDimension - 1) * 4f;
            }

            cellSizeValue = Mathf.Max(cellSizeValue, 8f);

            Vector2 cellSize = new Vector2(cellSizeValue, cellSizeValue);
            Vector2 spacing = _layoutGrid.spacing.x > 0 ? _layoutGrid.spacing : new Vector2(2f, 2f);

            _layoutGrid.cellSize = cellSize;
            _layoutGrid.spacing = spacing;
            _layoutGrid.childAlignment = TextAnchor.MiddleCenter;
            _layoutGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            _layoutGrid.constraintCount = width;

            // 3. Calculate exact dimensions for the trimmed grid
            float requiredGridW = width * cellSize.x + (width - 1) * spacing.x + _layoutGrid.padding.left + _layoutGrid.padding.right;
            float requiredGridH = height * cellSize.y + (height - 1) * spacing.y + _layoutGrid.padding.top + _layoutGrid.padding.bottom;

            var gridRect = _layoutGrid.GetComponent<RectTransform>();
            gridRect.sizeDelta = new Vector2(requiredGridW, requiredGridH);

            // Resize parent frame box if present
            var parentFrame = gridRect.parent as RectTransform;
            if (parentFrame != null && parentFrame != (RectTransform)transform)
            {
                float targetFrameW = Mathf.Max(parentFrame.sizeDelta.x, requiredGridW + 12f);
                float targetFrameH = Mathf.Max(55f, requiredGridH + 12f);

                parentFrame.sizeDelta = new Vector2(targetFrameW, targetFrameH);

                var layoutElement = parentFrame.GetComponent<LayoutElement>();
                if (layoutElement != null)
                {
                    layoutElement.preferredHeight = targetFrameH;
                    layoutElement.minHeight = targetFrameH;
                }
            }

            var cellColor = GetCellColor(component.Data.CellType);

            // 4. Instantiate only cells within the cropped active bounding box
            for (int y = minY; y <= maxY; y++)
            {
                var row = y < rawRows.Length ? rawRows[y].TrimEnd('\r') : string.Empty;
                for (int x = minX; x <= maxX; x++)
                {
                    var cell = Instantiate(_cellTemplate, _layoutGrid.transform);
                    var img = cell.GetComponent<Image>();

                    bool isCellActive = x < row.Length && row[x] != '0' && row[x] != ' ';

                    cell.gameObject.SetActive(true);
                    if (img != null)
                        img.color = isCellActive ? cellColor : Color.clear;
                }
            }

            _cellTemplate.gameObject.SetActive(false);
        }

        private void UpdateTextField(NameValueItem item, KeyValuePair<string, string> data)
        {
            item.Label.text = _localization.GetString(data.Key);
            item.Value.text = data.Value;
        }

        public static IEnumerable<KeyValuePair<string, string>> GetDescription(IComponent component, ILocalization localization, LocalizationSettings settings)
        {
            var stats = component.GetStats();

            // Armor and repair stats
            if (stats.ArmorPoints != 0)
                yield return new KeyValuePair<string, string>("$HitPoints", FormatFloat(stats.ArmorPoints));
            if (stats.ArmorRepairRate > 0)
                yield return new KeyValuePair<string, string>("$RepairRate", FormatFloat(stats.ArmorRepairRate));

            // Energy systems
            if (!Mathf.Approximately(stats.EnergyPoints, 0))
                yield return new KeyValuePair<string, string>("$Energy", FormatFloat(stats.EnergyPoints));

            if (stats.EnergyConsumption > 0)
                yield return new KeyValuePair<string, string>("$EnergyConsumption", FormatFloat(stats.EnergyConsumption));
            if (stats.EnergyRecharge > 0)
                yield return new KeyValuePair<string, string>("$RechargeRate", FormatFloat(stats.EnergyRecharge));

            // Shield systems
            if (!Mathf.Approximately(stats.ShieldPoints, 0))
                yield return new KeyValuePair<string, string>("$ShieldPoints", FormatFloat(stats.ShieldPoints));
            if (!Mathf.Approximately(stats.ShieldRechargeRate, 0))
                yield return new KeyValuePair<string, string>("$ShieldRechargeRate", FormatFloat(stats.ShieldRechargeRate));

            // Engines and boosters
            if (stats.EnginePower != 0)
                yield return new KeyValuePair<string, string>("$Velocity", FormatFloat(stats.EnginePower));
            if (stats.TurnRate != 0)
                yield return new KeyValuePair<string, string>("$TurnRate", FormatFloat(stats.TurnRate));
            if (stats.EnginePowerMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$EnginePowerModifier", stats.EnginePowerMultiplier.ToString());
            if (stats.TurnRateMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$TurnRateModifier", stats.TurnRateMultiplier.ToString());

            // Weapon boosters
            if (stats.WeaponDamageMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DamageModifier", stats.WeaponDamageMultiplier.ToString());
            if (stats.WeaponFireRateMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$FireRateModifier", stats.WeaponFireRateMultiplier.ToString());
            if (stats.WeaponRangeMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$RangeModifier", stats.WeaponRangeMultiplier.ToString());
            if (stats.WeaponEnergyCostMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$EnergyModifier", stats.WeaponEnergyCostMultiplier.ToString());
            if (stats.WeaponVelocityMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$WeaponVelocityModifier", stats.WeaponVelocityMultiplier.ToString());
            if (stats.WeaponAoeMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$WeaponAoeModifier", stats.WeaponAoeMultiplier.ToString());
            if (stats.WeaponImpulseMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$WeaponImpulseModifier", stats.WeaponImpulseMultiplier.ToString());
            if (stats.WeaponRecoilMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$WeaponRecoilModifier", stats.WeaponRecoilMultiplier.ToString());

            // Active devices boosters
            if (stats.DeviceCooldownMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DeviceCooldownModifier", stats.DeviceCooldownMultiplier.ToString());
            if (stats.DevicePowerMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DevicePowerModifier", stats.DevicePowerMultiplier.ToString());
            if (stats.DeviceRangeMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DeviceRangeModifier", stats.DeviceRangeMultiplier.ToString());

            // Cooldown delay modifiers
            if (stats.ArmorRepairCooldownMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$ArmorRepairCooldownModifier", stats.ArmorRepairCooldownMultiplier.ToString());
            if (stats.EnergyRechargeCooldownMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$EnergyRechargeCooldownModifier", stats.EnergyRechargeCooldownMultiplier.ToString());
            if (stats.ShieldRechargeCooldownMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$ShieldRechargeCooldownModifier", stats.ShieldRechargeCooldownMultiplier.ToString());

            // Autopilot presence
            if (stats.Autopilot)
                yield return new KeyValuePair<string, string>("$Autopilot", "+");

            // Ramming and energy absorption
            if (stats.RammingDamage != 0)
                yield return new KeyValuePair<string, string>("$RamDamage", FormatFloat(stats.RammingDamage));
            if (stats.RammingDamageMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$RamDamageModifier", stats.RammingDamageMultiplier.ToString());
            if (stats.EnergyAbsorption != 0)
                yield return new KeyValuePair<string, string>("$DamageAbsorption", FormatFloat(stats.EnergyAbsorption));

            // Damage resistances
            if (stats.KineticResistance != 0)
                yield return new KeyValuePair<string, string>("$KineticDamageResistance", FormatFloat(stats.KineticResistance));
            if (stats.ThermalResistance != 0)
                yield return new KeyValuePair<string, string>("$ThermalDamageResistance", FormatFloat(stats.ThermalResistance));
            if (stats.EnergyResistance != 0)
                yield return new KeyValuePair<string, string>("$EnergyDamageResistance", FormatFloat(stats.EnergyResistance));

            // Drone systems
            if (stats.DroneDamageMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DroneDamageModifier", stats.DroneDamageMultiplier.ToString());
            if (stats.DroneDefenseMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DroneDefenseModifier", stats.DroneDefenseMultiplier.ToString());
            if (stats.DroneRangeMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DroneRangeModifier", stats.DroneRangeMultiplier.ToString());
            if (stats.DroneSpeedMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DroneSpeedModifier", stats.DroneSpeedMultiplier.ToString());
            if (stats.DroneReconstructionSpeed > 0)
                yield return new KeyValuePair<string, string>("$DroneReconstructionTime", (1f / stats.DroneReconstructionSpeed).ToString("N1"));
            if (stats.DroneReconstructionTimeMultiplier.HasValue)
                yield return new KeyValuePair<string, string>("$DroneReconstructionTime", stats.DroneReconstructionTimeMultiplier.ToString());

            // Turrets and weapon platforms
            var platform = new DummyPlatform();
            component.UpdateWeaponPlatform(platform);
            if (platform.TurnRate != 0)
                yield return new KeyValuePair<string, string>("$TurretTurnRate", localization.GetString("$ValuePerSecond", FormatFloat(platform.TurnRate)));

            // Display turret auto-aiming arc
            if (platform.AutoAimingArc > 0)
                yield return new KeyValuePair<string, string>("$AutoAimingArc", platform.AutoAimingArc.ToString("0") + "°");

            // Display active device statistics
            if (component.Devices.Any())
            {
                foreach (var item in GetDeviceDescription(component.Devices.First()))
                    yield return item;
            }

            if (component.Weapons.Any())
            {
                var data = component.Weapons.First();
                var info = new WeaponDamageCalculator().CalculateWeaponDamage(data.Weapon.Stats, data.Ammunition, data.StatModifier);
                foreach (var item in GetWeaponDescription(info, settings))
                    yield return item;
            }

            if (component.WeaponsObsolete.Any())
            {
                var data = component.WeaponsObsolete.First();
                var info = new WeaponDamageCalculator().CalculateWeaponDamage(data.Key, data.Value);
                foreach (var item in GetWeaponDescription(info, settings))
                    yield return item;
            }

            if (component.DroneBays.Any())
                foreach (var item in GetDroneBayDescription(component.DroneBays.First(), localization))
                    yield return item;

            if (!Mathf.Approximately(stats.Weight + stats.WeightReduction, 0))
                yield return new KeyValuePair<string, string>("$Weight", Mathf.RoundToInt(stats.Weight + stats.WeightReduction).ToString());
        }

        private static IEnumerable<KeyValuePair<string, string>> GetDeviceDescription(DeviceStats device)
        {
            if (device.Cooldown > 0)
                yield return new KeyValuePair<string, string>("$DeviceCooldown", device.Cooldown.ToString(_floatFormat));
            if (device.Range > 0)
                yield return new KeyValuePair<string, string>("$DeviceRange", device.Range.ToString(_floatFormat));
            if (device.Power > 0)
                yield return new KeyValuePair<string, string>("$DevicePower", device.Power.ToString(_floatFormat));
        }

        private static IEnumerable<KeyValuePair<string, string>> GetWeaponDamageText(WeaponDamageCalculator.WeaponInfo data, LocalizationSettings settings)
        {
            var damageSuffix = data.Magazine <= 1 ? string.Empty : "х" + data.Magazine;

            if (data.Damage.Kinetic > 0)
                yield return new KeyValuePair<string, string>("$KineticDamage", data.Damage.Kinetic.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Kinetic > 0)
                yield return new KeyValuePair<string, string>("$KineticDPS", data.Dps.Kinetic.ToString(_floatFormat) + damageSuffix);

            if (data.Damage.Energy > 0)
                yield return new KeyValuePair<string, string>("$EnergyDamage", data.Damage.Energy.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Energy > 0)
                yield return new KeyValuePair<string, string>("$EnergyDPS", data.Dps.Energy.ToString(_floatFormat) + damageSuffix);

            if (data.Damage.Heat > 0)
                yield return new KeyValuePair<string, string>("$HeatDamage", data.Damage.Heat.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Heat > 0)
                yield return new KeyValuePair<string, string>("$HeatDPS", data.Dps.Heat.ToString(_floatFormat) + damageSuffix);

            if (data.Damage.Corrosive > 0)
                yield return new KeyValuePair<string, string>(settings.CorrosiveDamageText, data.Damage.Corrosive.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Corrosive > 0)
                yield return new KeyValuePair<string, string>(settings.CorrosiveDpsText, data.Dps.Corrosive.ToString(_floatFormat) + damageSuffix);

            if (data.Damage.Repair > 0)
                yield return new KeyValuePair<string, string>("$WeaponRepair", data.Damage.Repair.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Repair > 0)
                yield return new KeyValuePair<string, string>("$WeaponRepairPerSec", data.Dps.Repair.ToString(_floatFormat) + damageSuffix);

            if (data.Damage.Shield > 0)
                yield return new KeyValuePair<string, string>("$WeaponDamageShield", data.Damage.Shield.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Shield > 0)
                yield return new KeyValuePair<string, string>("$WeaponShieldPerSec", data.Dps.Shield.ToString(_floatFormat) + damageSuffix);
            else if (data.Damage.Shield < 0)
                yield return new KeyValuePair<string, string>("$WeaponShieldRecharge", (-data.Damage.Shield).ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.Shield < 0)
                yield return new KeyValuePair<string, string>("$WeaponShieldRechargePerSec", (-data.Dps.Shield).ToString(_floatFormat) + damageSuffix);

            if (data.Damage.EnergyDrain > 0)
                yield return new KeyValuePair<string, string>("$WeaponEnergyDrain", data.Damage.EnergyDrain.ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.EnergyDrain > 0)
                yield return new KeyValuePair<string, string>("$WeaponEnergyDrainPerSec", data.Dps.EnergyDrain.ToString(_floatFormat) + damageSuffix);
            else if (data.Damage.EnergyDrain < 0)
                yield return new KeyValuePair<string, string>("$WeaponEnergyRecharge", (-data.Damage.EnergyDrain).ToString(_floatFormat) + damageSuffix);
            else if (data.Dps.EnergyDrain < 0)
                yield return new KeyValuePair<string, string>("$WeaponEnergyRechargePerSec", (-data.Dps.EnergyDrain).ToString(_floatFormat) + damageSuffix);

            if (data.Effects.Contains(WeaponSpecialEffect.ShieldPierce))
                yield return new KeyValuePair<string, string>("$EffectShieldPierce", string.Empty);
            if (data.Effects.Contains(WeaponSpecialEffect.TeleportTarget))
                yield return new KeyValuePair<string, string>("$EffectTeleportTarget", string.Empty);
            if (data.Effects.Contains(WeaponSpecialEffect.SlowTarget))
                yield return new KeyValuePair<string, string>("$EffectSlowTarget", string.Empty);
            if (data.Effects.Contains(WeaponSpecialEffect.DisruptDrones))
                yield return new KeyValuePair<string, string>("$EffectDisruptDrones", string.Empty);
            if (data.Effects.Contains(WeaponSpecialEffect.ProgressiveDamage))
                yield return new KeyValuePair<string, string>("$EffectProgressiveDamage", string.Empty);
        }

        private static IEnumerable<KeyValuePair<string, string>> GetWeaponDescription(WeaponDamageCalculator.WeaponInfo data, LocalizationSettings settings)
        {
            foreach (var item in GetWeaponDamageText(data, settings))
                yield return item;

            if (data.EnergyCost > 0)
            {
                if (data.Continuous)
                    yield return new KeyValuePair<string, string>("$WeaponEPS", data.EnergyCost.ToString(_floatFormat));
                else
                    yield return new KeyValuePair<string, string>("$WeaponEnergy", data.EnergyCost.ToString(_floatFormat));
            }

            // Cooldown rendering is independent of energy cost
            if (data.FireRate > 0)
            {
                yield return new KeyValuePair<string, string>("$WeaponCooldown", (1.0f / data.FireRate).ToString(_floatFormat));
            }

            if (data.Range > 0)
                yield return new KeyValuePair<string, string>("$WeaponRange", data.Range.ToString(_floatFormat));
            if (data.BulletVelocity > 0)
                yield return new KeyValuePair<string, string>("$WeaponVelocity", data.BulletVelocity.ToString(_floatFormat));
            if (data.Impulse > 0)
                yield return new KeyValuePair<string, string>("$WeaponImpulse", (data.Impulse * 1000).ToString(_floatFormat));
            if (data.AreaOfEffect > 0)
                yield return new KeyValuePair<string, string>("$WeaponArea", data.AreaOfEffect.ToString(_floatFormat));
        }

        private static IEnumerable<KeyValuePair<string, string>> GetWeaponDamageText(Ammunition ammunition, WeaponStatModifier statModifier, ILocalization localization)
        {
            while (true)
            {
                var effect = ammunition.Effects.FirstOrDefault(item => item.Type == ImpactEffectType.Damage || item.Type == ImpactEffectType.SiphonHitPoints);
                if (effect?.Power > 0)
                {
                    yield return new KeyValuePair<string, string>("$DamageType", localization.GetString(effect.DamageType.Name()));
                    var damage = effect.Power * statModifier.DamageMultiplier.Value;
                    yield return new KeyValuePair<string, string>(ammunition.ImpactType == BulletImpactType.DamageOverTime ? "$WeaponDPS" : "$WeaponDamage", damage.ToString(_floatFormat));
                    yield break;
                }

                var trigger = ammunition.Triggers.OfType<BulletTrigger_SpawnBullet>().FirstOrDefault();
                if (trigger?.Ammunition != null)
                {
                    ammunition = trigger.Ammunition;
                    continue;
                }

                break;
            }
        }

        private static IEnumerable<KeyValuePair<string, string>> GetDroneBayDescription(KeyValuePair<DroneBayStats, ShipBuild> droneBay, ILocalization localization)
        {
            yield return new KeyValuePair<string, string>("$DroneBayCapacity", droneBay.Key.Capacity.ToString());
            if (!Mathf.Approximately(droneBay.Key.DamageMultiplier, 1f))
                yield return new KeyValuePair<string, string>("$DroneDamageModifier", FormatPercent(droneBay.Key.DamageMultiplier - 1f));
            if (!Mathf.Approximately(droneBay.Key.DefenseMultiplier, 1f))
                yield return new KeyValuePair<string, string>("$DroneDefenseModifier", FormatPercent(droneBay.Key.DefenseMultiplier - 1f));
            if (!Mathf.Approximately(droneBay.Key.SpeedMultiplier, 1f))
                yield return new KeyValuePair<string, string>("$DroneSpeedModifier", FormatPercent(droneBay.Key.SpeedMultiplier - 1f));

            yield return new KeyValuePair<string, string>("$DroneRangeModifier", droneBay.Key.Range.ToString("N"));

            var weapon = droneBay.Value.Components.Select(Constructor.ComponentExtensions.FromDatabase).FirstOrDefault(item => item.Info.Data.Weapon != null);
            if (weapon != null)
                yield return new KeyValuePair<string, string>("$WeaponType", localization.GetString(weapon.Info.Data.Name));
        }

        private static string FormatInt(int value)
        {
            return (value >= 0 ? "+" : "") + value;
        }

        private static string FormatFloat(float value)
        {
            return (value >= 0 ? "+" : "") + value.ToString(_floatFormat);
        }

        private static string FormatPercent(float value)
        {
            return (value >= 0 ? "+" : "") + Mathf.RoundToInt(100 * value) + "%";
        }

        private const string _floatFormat = "0.##";

        private class DummyPlatform : IWeaponPlatformStats
        {
            public void ChangeAutoAimingArc(float angle)
            {
                AutoAimingArc = angle;
            }

            public void ChangeTurnRate(float deltaAngle)
            {
                TurnRate += deltaAngle;
            }

            public float AutoAimingArc { get; private set; }
            public float TurnRate { get; private set; }
        }
    }
}