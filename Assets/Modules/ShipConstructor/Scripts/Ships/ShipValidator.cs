using System.Linq;
using Constructor.Ships.Modification;
using Database.Legacy;
using GameDatabase.DataModel;
using GameDatabase.Enums;

namespace Constructor.Ships
{
    public class ShipValidator
    {
        public static bool IsAvailableForPlayer(IShip ship)
        {
            if (ship.ExtraThreatLevel != DifficultyClass.Default)
                return false;

            if (ship.Components.Any(item => item.Info.Data.Availability == Availability.None))
                return false;

            if (ship.FirstSatellite != null)
                if (ship.FirstSatellite.Components.Any(item => item.Info.Data.Availability == Availability.None))
                    return false;

            if (ship.SecondSatellite != null)
                if (ship.SecondSatellite.Components.Any(item => item.Info.Data.Availability == Availability.None))
                    return false;

            return true;
        }

        public static bool IsShipViable(IShip ship, ShipSettings settings, out string errorReason)
        {
            errorReason = string.Empty;
            var spec = ship.CreateBuilder().Build(settings);
            var stats = spec.Stats;

            if (stats.EnergyRechargeRate <= 0)
            {
                errorReason = "No energy generation.";
                return false;
            }

            var weaponCount = 0;
            foreach (var platform in spec.Platforms)
            {
                foreach (var item in platform.WeaponsObsolete)
                {
                    if (item.Ammunition.EnergyCost > stats.EnergyPoints)
                    {
                        errorReason = $"Weapon energy cost ({item.Ammunition.EnergyCost}) > max ship capacity ({stats.EnergyPoints}).";
                        return false;
                    }
                    weaponCount++;
                }

                foreach (var item in platform.Weapons)
                {
                    if (item.Ammunition.Body.EnergyCost > stats.EnergyPoints)
                    {
                        errorReason = $"Weapon energy cost ({item.Ammunition.Body.EnergyCost}) > max ship capacity ({stats.EnergyPoints}).";
                        return false;
                    }
                    weaponCount++;
                }
            }

            if (weaponCount == 0 && !spec.DroneBays.Any())
            {
                errorReason = "No Weapons & DroneBays.";
                return false;
            }

            return true;
        }

        public static bool IsAllowedOnArena(IShip ship, ShipSettings settings)
        {
            if (!IsAvailableForPlayer(ship))
                return false;

            if (ship.Model.Modifications.Any(item => item.Type == ModificationType.ExtraBlueCells))
                return false;
            if (ship.Components.Any(item => IsForbiddenOnArena(ship, item)))
                return false;
            if (ship.FirstSatellite != null)
                if (ship.FirstSatellite.Components.Any(item => IsForbiddenOnArena(ship, item)))
                    return false;
            if (ship.SecondSatellite != null)
                if (ship.SecondSatellite.Components.Any(item => IsForbiddenOnArena(ship, item)))
                    return false;
            
            return IsShipViable(ship, settings, out _);
        }

        private static bool IsForbiddenOnArena(IShip ship, IntegratedComponent component)
        {
            return false;
        }
    }
}