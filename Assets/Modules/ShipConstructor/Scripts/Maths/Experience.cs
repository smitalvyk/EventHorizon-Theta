using System;
using Constructor.Ships;
using GameDatabase.DataModel;
using CodeWriter.ExpressionParser;

namespace Maths
{
    public struct Experience
    {
        private static ShipSettings _settings;
        private static float[] _powerMultiplierCache;

        // Compiled expression from CodeWriter.ExpressionParser
        private static Expression<float> _compiledExp;
        private static float _evalLevel;
        private static float _evalCells;
        private static float _evalThreat;

        public static ShipSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                RebuildFormulas();
            }
        }

        static Experience()
        {
            Settings = ShipSettings.Create(new GameDatabase.Serializable.ShipSettingsSerializable(), null);
        }

        private static void RebuildFormulas()
        {
            // Pre-calculate multipliers (0..1000) for instant lookups during combat
            _powerMultiplierCache = new float[1001];
            for (int lvl = 0; lvl <= 1000; lvl++)
            {
                _powerMultiplierCache[lvl] = _settings?.LevelToPowerMultiplier != null
                    ? _settings.LevelToPowerMultiplier(lvl)
                    : (float)Math.Pow(11, 0.01 * lvl);
            }

            // Compile TotalExp expression
            try
            {
                var context = new ExpressionContext<float>();
                context.RegisterVariable("level", () => _evalLevel);
                context.RegisterVariable("cells", () => _evalCells);
                context.RegisterVariable("threat", () => _evalThreat);

                var formula = !string.IsNullOrEmpty(_settings?.TotalExpFormula)
                    ? _settings.TotalExpFormula
                    : "(1 + (level + 1) ^ 1.2) * (cells ^ 1.2) * (1 + threat)";

                _compiledExp = FloatExpressionParser.Instance.Compile(formula, context, true);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[Experience] Failed to compile TotalExpFormula: {e.Message}");
                _compiledExp = null;
            }
        }

        public Experience(long value)
        {
            if (value < 0)
                value = 0;

            _value = value;
        }

        public int Level
        {
            get
            {
                var exp = Value;
                var level = (int)Math.Pow(exp / 100, 1.0 / 3.0);

                if (LevelToExp(level + 1) <= exp)
                    return level + 1;

                return level;
            }
        }

        public float PowerMultiplier => LevelToPowerMultiplier(Level);

        public long ExpFromLastLevel => Value - LevelToExp(Level);

        public long NextLevelCost
        {
            get
            {
                var level = Level;
                return LevelToExp(level + 1) - LevelToExp(level);
            }
        }

        public static Experience FromLevel(int level)
        {
            return new Experience(LevelToExp(level));
        }

        public static float LevelToPowerMultiplier(int level)
        {
            if (level < 0) level = 0;

            if (_powerMultiplierCache != null && level < _powerMultiplierCache.Length)
                return _powerMultiplierCache[level];

            return _settings?.LevelToPowerMultiplier != null
                ? _settings.LevelToPowerMultiplier(level)
                : (float)Math.Pow(11, 0.01 * level);
        }

        public static long TotalExpForShip(IShip ship)
        {
            if (_compiledExp == null)
            {
                // Fallback default formula
                return (1L + (long)Math.Pow(ship.Experience.Level + 1, 1.2)) *
                       (long)Math.Pow(ship.Model.Layout.CellCount, 1.2) *
                       (1 + (int)ship.ExtraThreatLevel);
            }

            _evalLevel = ship.Experience.Level;
            _evalCells = ship.Model.Layout.CellCount;
            _evalThreat = (int)ship.ExtraThreatLevel;

            return (long)_compiledExp.Invoke();
        }

        public static implicit operator Experience(long value)
        {
            return new Experience(value);
        }

        public static implicit operator long(Experience data)
        {
            return data.Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        private long Value => _value;

        public static long LevelToExp(int level) => 100L * level * level * level;

        private readonly ObscuredLong _value;
    }
}