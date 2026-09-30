using System;
using DataModel.Technology;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using Services.Localization;

namespace Gui.Craft
{
    public enum TechCategory
    {
        Weapon = 0,
        Armor = 1,
        Energy = 2,
        Engine = 3,
        Drone = 4,
        Ship = 5,
        Satellite = 6,
        Other = 7
    }

    public static class TechCategoryHelper
    {
        public static TechCategory GetCategory(this ITechnology tech)
        {
            if (tech is ShipTechnology)
                return TechCategory.Ship;

            if (tech is SatelliteTechnology)
                return TechCategory.Satellite;

            if (tech is ComponentTechnology compTech && compTech.Component != null)
            {
                switch (compTech.Component.DisplayCategory)
                {
                    case ComponentCategory.Weapon:
                        return TechCategory.Weapon;
                    case ComponentCategory.Defense:
                        return TechCategory.Armor;
                    case ComponentCategory.Energy:
                        return TechCategory.Energy;
                    case ComponentCategory.Engine:
                        return TechCategory.Engine;
                    case ComponentCategory.Drones:
                        return TechCategory.Drone;
                }
            }

            return TechCategory.Other;
        }

        public static string GetTitle(this TechCategory category, ILocalization localization)
        {
            switch (category)
            {
                case TechCategory.Weapon:
                    return localization.GetString("$GroupWeapon");
                case TechCategory.Armor:
                    return localization.GetString("$GroupArmor");
                case TechCategory.Energy:
                    return localization.GetString("$GroupEnergy");
                case TechCategory.Engine:
                    return localization.GetString("$GroupEngines");
                case TechCategory.Drone:
                    return localization.GetString("$GroupDrones");
                case TechCategory.Ship:
                    return localization.GetString("$GroupShips");
                case TechCategory.Satellite:
                    return localization.GetString("$GroupSatellites");
                default:
                    return localization.GetString("$MenuSpecial");
            }
        }

        public static string GetLocalizedFactionName(Faction faction, ILocalization localization)
        {
            if (faction == null || faction == Faction.Empty || faction.Id.ToString() == "Faction_0" || faction.Id.ToString() == "0")
            {
                string neutralName = localization.GetString("$NeutralFaction");
                if (!string.IsNullOrEmpty(neutralName) && !neutralName.StartsWith("$"))
                    return neutralName;

                string freeName = localization.GetString("$FreeStars");
                if (!string.IsNullOrEmpty(freeName) && !freeName.StartsWith("$"))
                    return freeName;

                return "$NeutralFaction";
            }

            string name = localization.GetString(faction.Name);
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("$"))
                return name;

            return faction.Name;
        }

        public static string GetShipClassName(Ship ship, ILocalization localization)
        {
            if (ship == null)
                return string.Empty;

            try
            {
                string rawStr = ship.SizeClass.ToString();
                string classKey = rawStr.StartsWith("Class", StringComparison.OrdinalIgnoreCase)
                    ? "$" + rawStr
                    : "$Class" + rawStr;

                string loc = localization.GetString(classKey);
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("$"))
                    return loc;

                loc = localization.GetString("$" + rawStr);
                if (!string.IsNullOrEmpty(loc) && !loc.StartsWith("$"))
                    return loc;

                return rawStr;
            }
            catch { }

            return string.Empty;
        }
    }
}