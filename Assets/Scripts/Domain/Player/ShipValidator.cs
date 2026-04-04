using System.Collections.Generic;
using System.Linq;
using Constructor;
using Constructor.Extensions;
using Constructor.Model;
using Constructor.Ships;
using GameDatabase.DataModel;
using ShipEditor.Model;

namespace Domain.Shipyard
{
    public interface IShipPartsStorage
    {
        public void AddComponent(ComponentInfo component);
        public void AddSatellite(Satellite satellite);
        public bool TryGetComponentReplacement(ComponentInfo original, out ComponentInfo replacement);
    }

    public static class ShipValidator
    {
        public static bool HasForbiddenComponents(IShip ship)
        {
            if (HasForbiddenComponents(ship.Components)) return true;
            if (HasForbiddenComponents(ship.FirstSatellite?.Components)) return true;
            if (HasForbiddenComponents(ship.SecondSatellite?.Components)) return true;
            return false;
        }

        public static bool IsLayoutValid(IShip ship, GameDatabase.IDatabase database = null, string contextName = "")
        {
            var componentTracker = new ComponentTracker(ship);

            if (HasInvalidComponents(CreateShipLayout(ship, componentTracker, database), ship.Components, componentTracker, contextName)) return false;
            if (IsSatelliteInvalid(ship, ship.FirstSatellite, componentTracker, database)) return false;
            if (IsSatelliteInvalid(ship, ship.SecondSatellite, componentTracker, database)) return false;
            return true;
        }

        public static void RemoveInvalidParts(IShip ship, IShipPartsStorage storage = null, GameDatabase.IDatabase database = null, string contextName = "")
        {
            var componentTracker = new ComponentTracker(ship);

            RemoveInvalidComponents(CreateShipLayout(ship, componentTracker, database), ship.Components, componentTracker, storage, contextName);
            ValidateComponentsConfiguration(ship.Components, storage);
            ship.FirstSatellite = ValidateSatellite(ship, ship.FirstSatellite, componentTracker, storage, database);
            ship.SecondSatellite = ValidateSatellite(ship, ship.SecondSatellite, componentTracker, storage, database);
        }

        private static void ReturnSatelliteToStorage(Constructor.Satellites.ISatellite satellite, IShipPartsStorage storage)
        {
            if (storage == null || satellite == null) return;
            storage.AddSatellite(satellite.Information);
            foreach (var item in satellite.Components)
                storage.AddComponent(item.Info);
        }

        private static bool IsSatelliteInvalid(IShip ship, Constructor.Satellites.ISatellite satellite, ComponentTracker componentTracker, GameDatabase.IDatabase database)
        {
            if (satellite == null) return false;
            if (!ship.IsSuitableSatelliteSize(satellite.Information))
            {
                GameDiagnostics.Trace.LogError($"Incompatible satellite: {satellite.Information.Name}");
                return true;
            }

            var layout = new ShipLayoutModel(ShipElementType.SatelliteL, new ShipLayoutAdapter(satellite.Information.Layout),
                satellite.Information.Barrels, componentTracker, database);

            return HasInvalidComponents(layout, satellite.Components, componentTracker, satellite.Information?.Name ?? "Satellite");
        }

        private static Constructor.Satellites.ISatellite ValidateSatellite(IShip ship, Constructor.Satellites.ISatellite satellite,
            ComponentTracker componentTracker, IShipPartsStorage storage, GameDatabase.IDatabase database)
        {
            if (satellite == null) return null;
            if (!ship.IsSuitableSatelliteSize(satellite.Information))
            {
                ReturnSatelliteToStorage(satellite, storage);
                return null;
            }

            var layout = new ShipLayoutModel(ShipElementType.SatelliteL, new ShipLayoutAdapter(satellite.Information.Layout),
                satellite.Information.Barrels, componentTracker, database);

            RemoveInvalidComponents(layout, satellite.Components, componentTracker, storage, satellite.Information?.Name ?? "Satellite");
            ValidateComponentsConfiguration(satellite.Components, storage);
            return satellite;
        }

        private static ShipLayoutModel CreateShipLayout(IShip ship, ComponentTracker componentTracker, GameDatabase.IDatabase database)
        {
            return new ShipLayoutModel(ShipElementType.Ship, ship.Model.Layout, ship.Model.Barrels, componentTracker, database);
        }

        private static void ValidateComponentsConfiguration(IList<IntegratedComponent> components, IShipPartsStorage storage)
        {
            if (components == null) return;

            int index = 0;
            while (index < components.Count)
            {
                var component = components[index];

                if (!component.Info.IsValidModification)
                {
                    if (storage == null || !storage.TryGetComponentReplacement(component.Info, out var replacement))
                    {
                        components.QuickRemove(index);
                        continue;
                    }

                    components[index] = new IntegratedComponent(replacement, component.X, component.Y,
                        component.BarrelId, component.KeyBinding, component.Behaviour, component.Locked);

                    GameDiagnostics.Trace.LogError($"Component replaced: {component.Info.Data.Name}");
                }
                index++;
            }
        }

        private static bool HasInvalidComponents(ShipLayoutModel layout, IList<IntegratedComponent> components, ComponentTracker componentTracker, string contextName = "")
        {
            bool hasInvalid = false;
            var groupedErrors = new Dictionary<string, List<string>>();

            foreach (var comp in components)
            {
                if (!TryInstallComponent(comp, layout, componentTracker, null))
                {
                    string name = comp.Info.Data.Name;
                    if (!groupedErrors.ContainsKey(name)) groupedErrors[name] = new List<string>();
                    groupedErrors[name].Add($"[{comp.X},{comp.Y}]");
                    hasInvalid = true;
                }
            }

            string prefix = string.IsNullOrEmpty(contextName) ? "" : $"[{contextName}] ";
            foreach (var kvp in groupedErrors)
                GameDiagnostics.Trace.LogError($"{prefix}Invalid component '{kvp.Key}' at {string.Join(", ", kvp.Value)}");

            return hasInvalid;
        }

        private static void RemoveInvalidComponents(ShipLayoutModel layout, IList<IntegratedComponent> components,
            ComponentTracker componentTracker, IShipPartsStorage storage, string contextName = "")
        {
            if (components == null) return;

            int index = 0;
            while (index < components.Count)
            {
                if (!TryInstallComponent(components[index], layout, componentTracker, storage))
                {
                    components.QuickRemove(index);
                    continue;
                }
                index++;
            }
        }

        private static bool TryInstallComponent(IntegratedComponent component, ShipLayoutModel layout, ComponentTracker tracker, IShipPartsStorage storage = null)
        {
            if (!layout.IsSuitableLocation(component.X, component.Y, component.Info.Data) || !tracker.IsCompatible(component.Info.Data))
            {
                storage?.AddComponent(component.Info);
                return false;
            }

            layout.InstallComponent(component.X, component.Y, component.Info,
                new ComponentSettings(component.KeyBinding, component.Behaviour, component.Locked));
            return true;
        }

        private static bool HasForbiddenComponents(IList<IntegratedComponent> components)
        {
            if (components == null) return false;
            foreach (var item in components)
            {
                if (item.Info.Data.Availability == GameDatabase.Enums.Availability.None || item.Info.Level > 0 ||
                   (item.Info.Data.Availability == GameDatabase.Enums.Availability.Special && !item.Locked))
                {
                    GameDiagnostics.Trace.LogError($"Forbidden module: {item.Info.Data.Name}");
                    return true;
                }
            }
            return false;
        }
    }

    public struct FleetPartsStorage : IShipPartsStorage
    {
        private readonly GameServices.Player.PlayerInventory _inventory;
        public FleetPartsStorage(GameServices.Player.PlayerInventory inventory) => _inventory = inventory;

        public void AddComponent(ComponentInfo component) => _inventory.Components.Add(component);
        public void AddSatellite(Satellite satellite) => _inventory.Satellites.Add(satellite);
        public bool TryGetComponentReplacement(ComponentInfo original, out ComponentInfo replacement)
        {
            replacement = ComponentInfo.Empty;
            return false;
        }
    }
}