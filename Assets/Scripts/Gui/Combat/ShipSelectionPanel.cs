using System.Collections.Generic;
using Combat.Component.Ship;
using Combat.Domain;
using Combat.Manager;
using Combat.Scene;
using Combat.Unit;
using Services.Gui;
using UnityEngine;
using Zenject;

namespace Gui.Combat
{
    public class ShipSelectionPanel : MonoBehaviour
    {
        [Inject] private readonly CombatManager _manager;
        [Inject] private readonly IScene _scene;

        [SerializeField] private ShipList _enemyShips;
        [SerializeField] private ShipList _playerShips;

        public void Open(ICombatModel combatModel)
        {
            if (Window.IsVisible)
                return;

            IList<IShipInfo> enemyShips = combatModel.EnemyFleet.Ships;
            IList<IShipInfo> playerShips = combatModel.PlayerFleet.Ships;

            int enemyIndex = FindTargetShipIndex(enemyShips);
            int playerIndex = FindTargetShipIndex(playerShips);

            var rules = combatModel.Rules;
            int maxListCount = rules.EnemyShipMaxCount;
            int scrollAhead = rules.EnemyShipScrollAhead;
            int identifiedCount = rules.EnemyShipIdentifiedCount;

            // Compute dynamic limits for enemy fleet
            int enemyMaxScrollIndex = Mathf.Clamp(enemyIndex + scrollAhead, 0, Mathf.Max(0, enemyShips.Count - 1));
            int enemyMaxNormalIndex = Mathf.Clamp(enemyIndex + (identifiedCount - 1), 0, Mathf.Max(0, enemyShips.Count - 1));

            int playerMaxIndex = Mathf.Max(0, playerShips.Count - 1);

            _enemyShips.Initialize(combatModel.EnemyFleet, enemyIndex, enemyMaxScrollIndex, enemyMaxNormalIndex, maxListCount);
            _playerShips.Initialize(combatModel.PlayerFleet, playerIndex, playerMaxIndex, playerMaxIndex, 99);

            GetComponent<IWindow>().Open();
        }

        public void StartButtonClicked()
        {
            var ship = _playerShips.SelectedShip;
            if (ship == null || ship.Status != ShipStatus.Ready)
                return;

            _manager.CreateShip(ship);
            GetComponent<IWindow>().Close();
        }

        private int FindTargetShipIndex(IList<IShipInfo> ships)
        {
            if (ships == null || ships.Count == 0)
                return 0;

            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i].Status == ShipStatus.Active)
                    return i;
            }

            for (int i = 0; i < ships.Count; i++)
            {
                if (ships[i].Status == ShipStatus.Ready)
                    return i;
            }

            return 0;
        }

        private void Update()
        {
            if (_scene.PlayerShip.IsActive() && Window.IsVisible)
                Window.Close();
        }

        private IWindow Window => GetComponent<IWindow>();
    }
}