using Constructor;
using Constructor.Component;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using Services.Localization;
using ShipEditor.Model;
using System.Collections.Generic;
using System.Linq;

namespace ShipEditor.UI
{
    public static class ComponentRestrictionsFormatter
    {
        public static IEnumerable<KeyValuePair<string, string>> GetStats(
            ComponentInfo info,
            IComponent component,
            IShipEditorModel shipEditor,
            ILocalization localization)
        {
            var restrictions = info.Data?.Restrictions;
            if (restrictions == null)
                yield break;

            var installed = shipEditor.InstalledComponents;
            var currentShip = shipEditor.Ship?.Model;

            // Individual component amount limit
            if (restrictions.MaxComponentAmount > 0)
            {
                int count = installed?.Count(c => c.Info.Data == info.Data) ?? 0;
                int max = restrictions.MaxComponentAmount;

                yield return new KeyValuePair<string, string>(
                    localization.GetString("$ComponentLimit"),
                    FormatCount(count, max));
            }

            // Group tag limit
            var tag = restrictions.ComponentGroupTag;
            if (tag != null)
            {
                int count = installed?.Count(c =>
                    c.Info.Data?.Restrictions?.ComponentGroupTag != null &&
                    c.Info.Data.Restrictions.ComponentGroupTag.Id == tag.Id) ?? 0;

                int max = tag.MaxInstallableComponents;
                string tagId = tag.Id.ToString();
                string cleanId = tagId.Replace("ComponentGroupTag_", "");

                string tagKey = $"$Tag_{tagId}";
                string tagName = localization.GetString(tagKey);

                if (tagName.StartsWith("$"))
                    tagName = $"{localization.GetString("$DefaultTagName")}{cleanId}";

                yield return new KeyValuePair<string, string>(tagName, FormatCount(count, max));
            }

            // Combined requirements
            var requirements = new List<string>();

            // Ship sizes mapped directly to xml keys: $ClassFrigate, $ClassCruiser, etc.
            var sizes = restrictions.ShipSizes
                .Where(s => s != SizeClass.Undefined)
                .Select(s => localization.GetString($"$Class{s}"));

            requirements.AddRange(sizes);

            if (restrictions.NotForOrganicShips)
                requirements.Add(localization.GetString("$ShipTypeMechanical"));
            else if (restrictions.NotForMechanicShips)
                requirements.Add(localization.GetString("$ShipTypeOrganic"));

            if (requirements.Count > 0)
            {
                string text = string.Join(", ", requirements);
                bool suitable = currentShip == null || (component != null && component.IsSuitable(currentShip));

                yield return new KeyValuePair<string, string>(
                    localization.GetString("$Requirements"),
                    suitable ? text : $"<color=#FF6666>{text}</color>");
            }
        }

        private static string FormatCount(int current, int max)
        {
            return current >= max ? $"<color=#FF6666>{current} / {max}</color>" : $"{current} / {max}";
        }
    }
}