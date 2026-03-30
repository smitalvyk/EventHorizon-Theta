using UnityEngine;
using UnityEngine.UI;
using Gui.ComponentList;
using Zenject;
using Services.Localization;
using Services.Resources;
using GameDatabase.DataModel;
using ShipEditor.Model;
using Constructor.Satellites;
using System.Linq;

namespace ShipEditor.UI
{
    public class SatellitesPanel : MonoBehaviour
    {
        [Inject] private readonly ILocalization _localization;
        [Inject] private readonly IResourceLocator _resourceLocator;
        [Inject] private readonly IShipEditorModel _shipEditor;

        [SerializeField] private GroupListItem _backButton;
        [SerializeField] private LayoutGroup _itemsLayoutGroup;
        [SerializeField] private GameObject _removeSatelliteItem;

        [Header("Search UI")]
        [SerializeField] private InputField _searchInput;

        private SatelliteLocation _location;
        private string _searchQuery = "";

        private void OnEnable()
        {
            _shipEditor.Events.SatelliteChanged += OnSatelliteChanged;
            _shipEditor.Events.ShipChanged += OnShipChanged;

            // Reset search query on enable
            _searchQuery = "";
            if (_searchInput != null) _searchInput.text = "";

            UpdateContent();
        }

        private void OnDisable()
        {
            _shipEditor.Events.SatelliteChanged -= OnSatelliteChanged;
            _shipEditor.Events.ShipChanged -= OnShipChanged;
        }

        public SatelliteLocation Location
        {
            get => _location;
            set
            {
                if (_location == value) return;
                _location = value;
            }
        }

        public bool Visible
        {
            get => gameObject.activeSelf;
            set => gameObject.SetActive(value);
        }

        public void Start()
        {
            var node = new RootNode(null);
            _backButton.Initialize(node, node);
        }

        public void RemoveSatellite()
        {
            _shipEditor.RemoveSatellite(_location);
        }

        public void InstallSatellite(SatelliteItem item)
        {
            if (item.SatelliteBuild != null)
                _shipEditor.InstallSatellite(_location, item.SatelliteBuild);
            else
                _shipEditor.TryInstallSatellite(_location, item.Satellite);
        }

        private void OnShipChanged(Constructor.Ships.IShip ship)
        {
            UpdateContent();
        }

        private void OnSatelliteChanged(SatelliteLocation location)
        {
            if (location == _location)
                UpdateContent();
        }

        // Called by the Search button UI event
        public void ExecuteSearch()
        {
            if (_searchInput != null)
            {
                _searchQuery = _searchInput.text != null ? _searchInput.text.ToLower() : "";
                UpdateContent();
            }
        }

        private void UpdateContent()
        {
            var isInstalled = _shipEditor.HasSatellite(_location);
            _removeSatelliteItem.SetActive(isInstalled);

            bool isSearching = !string.IsNullOrEmpty(_searchQuery);

            if (_shipEditor.Inventory.SatelliteBuilds.Count > 0)
            {
                var builds = _shipEditor.Inventory.SatelliteBuilds.ToList();

                // Filter satellite builds by search query
                if (isSearching)
                {
                    builds = builds.Where(s =>
                        GetSatelliteName(s).Contains(_searchQuery)
                    ).ToList();
                }

                _itemsLayoutGroup.transform.InitializeElements<SatelliteItem, ISatellite>(
                    builds, UpdateSatellite);
            }
            else
            {
                var sats = _shipEditor.Inventory.Satellites.ToList();

                // Filter regular satellites by search query
                if (isSearching)
                {
                    sats = sats.Where(s =>
                        GetSatelliteName(s).Contains(_searchQuery)
                    ).ToList();
                }

                _itemsLayoutGroup.transform.InitializeElements<SatelliteItem, Satellite>(
                    sats, UpdateSatellite);
            }
        }

        // Helper methods for search filtering
        private string GetSatelliteName(ISatellite satellite)
        {
            return _localization.Localize(satellite.Name).ToLower();
        }

        private string GetSatelliteName(Satellite satellite)
        {
            return _localization.Localize(satellite.Name).ToLower();
        }

        private void UpdateSatellite(SatelliteItem item, ISatellite satellite)
        {
            var canBeInstalled = _shipEditor.CompatibilityChecker.IsCompatible(satellite.Information);
            item.Initialize(satellite, canBeInstalled, _resourceLocator, _localization);
        }

        private void UpdateSatellite(SatelliteItem item, Satellite satellite)
        {
            var quantity = _shipEditor.Inventory.GetQuantity(satellite);
            var canBeInstalled = _shipEditor.CompatibilityChecker.IsCompatible(satellite);
            item.Initialize(satellite, quantity, canBeInstalled, _resourceLocator, _localization);
        }
    }
}