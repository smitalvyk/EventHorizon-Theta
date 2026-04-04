using System;
using System.Collections.Generic;
using System.Linq;
using Constructor.Model;
using GameDatabase.DataModel;
using GameDatabase.Enums;
using GameDatabase.Model;

namespace Constructor.Ships.Modification
{
    public class LayoutModifications
    {
        public event Action DataChangedEvent;
        private readonly IShipLayout _shipLayout;
        private readonly CustomLayout _customLayout;

        public static bool IsDisabledForShip(Ship ship, ShipSettings shipSettings) => ship.CellsExpansions switch
        {
            ToggleState.Enabled => false,
            ToggleState.Disabled => true,
            _ => shipSettings?.DisableCellsExpansions ?? false,
        };

        public LayoutModifications(Ship ship, bool disableModifications = false)
        {
            if (disableModifications)
                _shipLayout = new ShipLayoutAdapter(ship.Layout);
            else
                _shipLayout = _customLayout = new CustomLayout(ship.Layout);
        }

        public IShipLayout BuildLayout() => _shipLayout;

        public bool TryAddCell(int x, int y, CellType cellType, GameDatabase.IDatabase database = null)
        {
            if (_customLayout == null || !_customLayout.TryModifyCell(x, y, cellType, database))
                return false;

            DataChangedEvent?.Invoke();
            return true;
        }

        public void FullyUpgrade() => _customLayout?.FullyUpgrade();

        public int TotalExtraCells() => _customLayout?.CustomizableCellCount ?? 0;

        public int ExtraCells() => _customLayout?.AddedCellCount ?? 0;

        public void Reset()
        {
            _customLayout?.Reset();
            DataChangedEvent?.Invoke();
        }

        public void Deserialize(byte[] data)
        {
            if (_customLayout == null) return;
            if (data == null || data.Length == 0) { Reset(); return; }

            if (!_customLayout.TryDeserialize(data))
                _customLayout.DeserializeObsolete(data);

            DataChangedEvent?.Invoke();
        }

        public IEnumerable<byte> Serialize() => _customLayout?.Serialize() ?? Enumerable.Empty<byte>();

        public bool IsCellValid(int x, int y, CellType type, GameDatabase.IDatabase database = null)
            => _customLayout != null && _customLayout.IsValidModification(x, y, type, database);

        private class CustomLayout : IShipLayout
        {
            private readonly Layout _stockLayout;
            private readonly char[] _layout;
            private readonly LayoutRect _rect;
            private int _customizableCellCount;
            private int _addedCellCount;

            public CustomLayout(Layout stockLayout)
            {
                _stockLayout = stockLayout;
                var size = _stockLayout.Size;
                var data = _stockLayout.Data;

                bool top = false, bottom = false, left = false, right = false;
                for (int i = 0; i < size; ++i)
                {
                    if (data[i] != (char)CellType.Empty) top = true;
                    if (data[size * size - i - 1] != (char)CellType.Empty) bottom = true;
                    if (data[i * size] != (char)CellType.Empty) left = true;
                    if (data[(i + 1) * size - 1] != (char)CellType.Empty) right = true;
                }

                _rect = new LayoutRect(left ? -1 : 0, top ? -1 : 0, right ? size : size - 1, bottom ? size : size - 1);
                _layout = new char[_rect.Square];
                Reset();
            }

            public void FullyUpgrade()
            {
                for (int y = _rect.yMin; y <= _rect.yMax; ++y)
                {
                    for (int x = _rect.xMin; x <= _rect.xMax; ++x)
                    {
                        int index = _rect.ToArrayIndex(x, y);
                        if (_layout[index] != (char)Layout.CustomizableCell) continue;

                        var l = (CellType)_stockLayout[x - 1, y];
                        var r = (CellType)_stockLayout[x + 1, y];
                        var t = (CellType)_stockLayout[x, y - 1];
                        var b = (CellType)_stockLayout[x, y + 1];

                        if (l == CellType.Weapon || r == CellType.Weapon || t == CellType.Weapon || b == CellType.Weapon)
                            _layout[index] = (char)Layout.CustomWeaponCell;
                        else if (l == CellType.Inner || r == CellType.Inner || t == CellType.Inner || b == CellType.Inner)
                            _layout[index] = (char)CellType.Inner;
                        else if (l == CellType.Engine || r == CellType.Engine || t == CellType.Engine || b == CellType.Engine)
                            _layout[index] = (char)CellType.Engine;
                        else
                            _layout[index] = (char)CellType.Outer;
                    }
                }
            }

            public bool IsValidModification(int x, int y, CellType value, GameDatabase.IDatabase database = null)
            {
                if (!_rect.IsInsideRect(x, y) || !IsCustomizable(x, y)) return false;
                if (value == CellType.Outer) return true;

                var l = (CellType)_stockLayout[x - 1, y];
                var r = (CellType)_stockLayout[x + 1, y];
                var t = (CellType)_stockLayout[x, y - 1];
                var b = (CellType)_stockLayout[x, y + 1];

                if (value == l || value == r || value == t || value == b) return true;

                if (database?.CellSettings != null)
                {
                    CellType[] neighbors = { l, r, t, b };
                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor == CellType.Empty) continue;
                        foreach (var cellData in database.CellSettings.Cells)
                        {
                            if (!string.IsNullOrEmpty(cellData.Symbol) && cellData.Symbol[0] == (char)neighbor)
                            {
                                if (!string.IsNullOrEmpty(cellData.ShipyardPlacementRule) && cellData.ShipyardPlacementRule.Contains((char)value))
                                    return true;
                                break;
                            }
                        }
                    }
                }
                return false;
            }

            public bool TryModifyCell(int x, int y, CellType value, GameDatabase.IDatabase database = null)
            {
                if (!IsValidModification(x, y, value, database)) return false;

                if (value == CellType.Weapon) value = (CellType)Layout.CustomWeaponCell;

                _layout[_rect.ToArrayIndex(x, y)] = (char)value;
                _addedCellCount++;
                return true;
            }

            public IEnumerable<byte> Serialize()
            {
                if (_addedCellCount == 0) yield break;

                for (int i = _rect.yMin; i <= _rect.yMax; ++i)
                    for (int j = _rect.xMin; j <= _rect.xMax; ++j)
                        if (IsCustomizable(j, i)) yield return CellTypeConverter.ToByte((CellType)_layout[_rect.ToArrayIndex(j, i)]);
            }

            public bool TryDeserialize(byte[] data)
            {
                Reset();
                if (data.Length == 0) return true;

                int index = 0;
                for (int i = _rect.yMin; i <= _rect.yMax; ++i)
                {
                    for (int j = _rect.xMin; j <= _rect.xMax; ++j)
                    {
                        if (index >= data.Length) break;
                        if (_layout[_rect.ToArrayIndex(j, i)] != (char)Layout.CustomizableCell) continue;

                        if (!CellTypeConverter.TryConvert(data[index++], out var cellType)) return false;
                        if (cellType == CellType.Empty) continue;

                        if (cellType == CellType.Weapon) cellType = (CellType)Layout.CustomWeaponCell;
                        _layout[_rect.ToArrayIndex(j, i)] = (char)cellType;
                        _addedCellCount++;
                    }
                }
                return true;
            }

            public void DeserializeObsolete(byte[] data)
            {
                Reset();
                int index = 0, dataIndex = 0, size = _stockLayout.Size;

                while (dataIndex < data.Length && index < _layout.Length)
                {
                    if (data[dataIndex] == (byte)CellType.Empty) { dataIndex++; index += data[dataIndex++]; continue; }

                    int x = index % size, y = index / size;
                    CellType value = (CellType)data[dataIndex];
                    if (value == CellType.Weapon) value = (CellType)Layout.CustomWeaponCell;

                    _layout[_rect.ToArrayIndex(x, y)] = (char)value;
                    _addedCellCount++;
                    index++; dataIndex++;
                }
            }

            public void Reset()
            {
                _addedCellCount = 0; _customizableCellCount = 0;
                for (int i = _rect.yMin; i <= _rect.yMax; ++i)
                {
                    for (int j = _rect.xMin; j <= _rect.xMax; ++j)
                    {
                        int index = _rect.ToArrayIndex(j, i);
                        char cell = _stockLayout[j, i];
                        if (cell != (char)CellType.Empty) _layout[index] = cell;
                        else if (_stockLayout[j, i - 1] != (char)CellType.Empty || _stockLayout[j - 1, i] != (char)CellType.Empty ||
                                 _stockLayout[j + 1, i] != (char)CellType.Empty || _stockLayout[j, i + 1] != (char)CellType.Empty)
                        {
                            _layout[index] = (char)Layout.CustomizableCell;
                            _customizableCellCount++;
                        }
                        else _layout[index] = (char)CellType.Empty;
                    }
                }
            }

            public int AddedCellCount => _addedCellCount;
            public int CustomizableCellCount => _customizableCellCount;
            private bool IsCustomizable(int x, int y) => _stockLayout[x, y] == (char)CellType.Empty && _layout[_rect.ToArrayIndex(x, y)] != (char)CellType.Empty;
            public ref readonly LayoutRect Rect => ref _rect;
            public int CellCount => _stockLayout.CellCount + _addedCellCount;
            public int Size => _stockLayout.Size;
            public CellType this[int x, int y] => (CellType)_layout[_rect.ToArrayIndex(x, y)];

            private static class CellTypeConverter
            {
                public const byte EmptyCell = 0, OuterCell = 1, InnerCell = 2, EngineCell = 3, WeaponCell = 4;

                public static byte ToByte(CellType cellType) => cellType switch
                {
                    CellType.Outer => OuterCell,
                    CellType.Inner => InnerCell,
                    CellType.Engine => EngineCell,
                    CellType.Weapon or (CellType)Layout.CustomWeaponCell => WeaponCell,
                    CellType.Empty or (CellType)Layout.CustomizableCell => EmptyCell,
                    _ => (byte)cellType >= 32 ? (byte)cellType : throw new InvalidOperationException()
                };

                public static bool TryConvert(byte value, out CellType cellType)
                {
                    cellType = value switch
                    {
                        OuterCell => CellType.Outer,
                        InnerCell => CellType.Inner,
                        EngineCell => CellType.Engine,
                        WeaponCell => CellType.Weapon,
                        EmptyCell => CellType.Empty,
                        _ when value >= 32 => (CellType)value,
                        _ => CellType.Empty
                    };
                    return value == EmptyCell || cellType != CellType.Empty;
                }
            }
        }
    }
}