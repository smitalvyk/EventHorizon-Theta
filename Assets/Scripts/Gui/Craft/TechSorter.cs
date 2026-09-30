using System;
using System.Collections.Generic;
using System.Linq;
using DataModel.Technology;
using GameDatabase.DataModel;
using Services.Localization;

namespace Gui.Craft
{
    public enum SortMode
    {
        Default = 0,
        NameAsc = 1,
        NameDesc = 2,
        IdAsc = 3,
        IdDesc = 4
    }

    public static class TechSorter
    {
        public static List<ITechnology> Sort(List<ITechnology> source, SortMode mode, ILocalization localization)
        {
            if (source == null || source.Count <= 1)
                return source;

            // Keep base faction items first (0), neutral/free stars last (1)
            int FactionOrder(ITechnology t) => (t.Faction == null || t.Faction == Faction.Empty) ? 1 : 0;

            switch (mode)
            {
                case SortMode.NameAsc:
                    return source.OrderBy(FactionOrder)
                                 .ThenBy(t => t.GetName(localization))
                                 .ToList();

                case SortMode.NameDesc:
                    return source.OrderBy(FactionOrder)
                                 .ThenByDescending(t => t.GetName(localization))
                                 .ToList();

                case SortMode.IdAsc:
                    return source.OrderBy(FactionOrder)
                                 .ThenBy(GetTechSortId)
                                 .ToList();

                case SortMode.IdDesc:
                    return source.OrderBy(FactionOrder)
                                 .ThenByDescending(GetTechSortId)
                                 .ToList();

                default:
                    return source.OrderBy(FactionOrder).ToList();
            }
        }

        public static string GetModeTitle(SortMode mode, ILocalization localization)
        {
            switch (mode)
            {
                case SortMode.NameAsc:
                    return localization.GetString("$SortNameAsc");
                case SortMode.NameDesc:
                    return localization.GetString("$SortNameDesc");
                case SortMode.IdAsc:
                    return localization.GetString("$SortIdAsc");
                case SortMode.IdDesc:
                    return localization.GetString("$SortIdDesc");
                default:
                    return localization.GetString("$SortDefault");
            }
        }

        public static int GetTechSortId(ITechnology tech)
        {
            if (tech == null) return 0;
            if (tech is ComponentTechnology compTech && compTech.Component != null)
                return compTech.Component.Id.Value;
            if (tech is ShipTechnology shipTech && shipTech.Ship != null)
                return shipTech.Ship.Id.Value;
            if (tech is TechnologyBase techBase && techBase.Data != null)
                return techBase.Data.Id.Value;
            return 0;
        }
    }
}