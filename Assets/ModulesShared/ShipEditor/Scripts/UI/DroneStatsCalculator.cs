using System.Collections.Generic;
using System.Linq;
using Constructor.Component;
using GameDatabase.DataModel;
using GameDatabase.Model;
using Services.Localization;
using UnityEngine;

namespace ShipEditor.UI
{
    public static class DroneStatsCalculator
    {
        public struct Result
        {
            public bool IsValid;
            public SpriteId ShipModelImage;
            public float Durability;
            public float Energy;
            public float Speed;
            public List<string> Weapons;
            public List<string> Devices;
        }

        public static Result Calculate(IComponent component, ILocalization localization)
        {
            var result = new Result
            {
                Weapons = new List<string>(),
                Devices = new List<string>()
            };

            if (!component.DroneBays.Any())
                return result;

            var droneBay = component.DroneBays.First();
            var bayStats = droneBay.Key;
            var shipBuild = droneBay.Value;
            var ship = shipBuild?.Ship;

            if (ship == null || shipBuild == null)
                return result;

            result.IsValid = true;
            result.ShipModelImage = ship.ModelImage;

            float totalArmor = 0f;
            float totalShield = 0f;
            float totalEnergy = 0f;
            float totalEnginePower = 0f;

            var weaponCounts = new Dictionary<string, int>();
            int totalWeaponCount = 0;

            var deviceCounts = new Dictionary<string, int>();
            int totalDeviceCount = 0;

            int droneCellCount = ship.Layout.CellCount;

            foreach (var installedComp in shipBuild.Components)
            {
                var integrated = Constructor.ComponentExtensions.FromDatabase(installedComp);
                if (integrated.Info.Data == null)
                    continue;

                var compInstance = integrated.Info.CreateComponent(droneCellCount);
                string compName = localization.GetString(integrated.Info.Data.Name);

                if (compInstance != null)
                {
                    var stats = compInstance.GetStats();
                    totalArmor += stats.ArmorPoints;
                    totalShield += stats.ShieldPoints;
                    totalEnergy += stats.EnergyPoints;
                    totalEnginePower += stats.EnginePower;
                }

                // Weapons
                if (integrated.Info.Data.Weapon != null)
                {
                    if (!weaponCounts.ContainsKey(compName))
                        weaponCounts[compName] = 0;
                    weaponCounts[compName]++;
                    totalWeaponCount++;
                }

                // Special devices and active modules
                bool isSpecial = integrated.Info.Data.Device != null ||
                                 (compInstance != null && compInstance.Devices.Any()) ||
                                 (compInstance != null && compInstance.GetStats().ArmorRepairRate > 0 && integrated.Info.Data.Weapon == null);

                if (isSpecial)
                {
                    if (!deviceCounts.ContainsKey(compName))
                        deviceCounts[compName] = 0;
                    deviceCounts[compName]++;
                    totalDeviceCount++;
                }
            }

            // Apply bay multipliers (armor minimum is clamped to 1)
            float finalArmor = Mathf.Max(1f, totalArmor * bayStats.DefenseMultiplier);
            float finalShield = Mathf.Max(0f, totalShield * bayStats.DefenseMultiplier);

            result.Durability = finalArmor + finalShield;
            result.Energy = totalEnergy;
            result.Speed = Mathf.Max(0f, totalEnginePower * bayStats.SpeedMultiplier);

            // Format weapons list
            if (weaponCounts.Count > 0)
            {
                result.Weapons = weaponCounts
                    .Select(kv => totalWeaponCount > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key)
                    .ToList();
            }

            // Format devices list
            if (deviceCounts.Count > 0)
            {
                result.Devices = deviceCounts
                    .Select(kv => totalDeviceCount > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key)
                    .ToList();
            }

            return result;
        }
    }
}