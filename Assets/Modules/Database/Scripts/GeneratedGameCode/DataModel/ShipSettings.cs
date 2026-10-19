//-------------------------------------------------------------------------------
//                                                                               
//    This code was automatically generated.                                     
//    Changes to this file may cause incorrect behavior and will be lost if      
//    the code is regenerated.                                                   
//                                                                               
//-------------------------------------------------------------------------------

using System.Linq;
using GameDatabase.Enums;
using GameDatabase.Serializable;
using GameDatabase.Model;
using CodeWriter.ExpressionParser;

namespace GameDatabase.DataModel
{
	public partial class ShipSettings : IDefaultVariablesResolver
	{
		partial void OnDataDeserialized(ShipSettingsSerializable serializable, Database.Loader loader);

		public static ShipSettings Create(ShipSettingsSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new ShipSettings(serializable, loader);
		}

		private ShipSettings(ShipSettingsSerializable serializable, Database.Loader loader)
		{
			var variableResolver = new VariableResolver(this);
			DefaultWeightPerCell = UnityEngine.Mathf.Clamp(serializable.DefaultWeightPerCell, 1f, 1000000f);
			MinimumWeightPerCell = UnityEngine.Mathf.Clamp(serializable.MinimumWeightPerCell, 1f, 1000000f);
			BaseArmorPoints = UnityEngine.Mathf.Clamp(serializable.BaseArmorPoints, 0f, 1000000f);
			ArmorPointsPerCell = UnityEngine.Mathf.Clamp(serializable.ArmorPointsPerCell, 0f, 1000000f);
			ArmorRepairCooldown = UnityEngine.Mathf.Clamp(serializable.ArmorRepairCooldown, 0f, 60f);
			BaseEnergyPoints = UnityEngine.Mathf.Clamp(serializable.BaseEnergyPoints, 0f, 1000000f);
			BaseEnergyRechargeRate = UnityEngine.Mathf.Clamp(serializable.BaseEnergyRechargeRate, 0f, 1000000f);
			EnergyRechargeCooldown = UnityEngine.Mathf.Clamp(serializable.EnergyRechargeCooldown, 0f, 60f);
			BaseShieldRechargeRate = UnityEngine.Mathf.Clamp(serializable.BaseShieldRechargeRate, 0f, 1000000f);
			ShieldRechargeCooldown = UnityEngine.Mathf.Clamp(serializable.ShieldRechargeCooldown, 0f, 60f);
			BaseDroneReconstructionSpeed = UnityEngine.Mathf.Clamp(serializable.BaseDroneReconstructionSpeed, 0f, 100f);
			ShieldCorrosiveResistance = UnityEngine.Mathf.Clamp(serializable.ShieldCorrosiveResistance, 0f, 1f);
			MaxVelocity = UnityEngine.Mathf.Clamp(serializable.MaxVelocity, 5f, 100f);
			MaxAngularVelocity = UnityEngine.Mathf.Clamp(serializable.MaxAngularVelocity, 5f, 100f);
			MaxAcceleration = UnityEngine.Mathf.Clamp(serializable.MaxAcceleration, 5f, 1000f);
			MaxAngularAcceleration = UnityEngine.Mathf.Clamp(serializable.MaxAngularAcceleration, 5f, 1000f);
			DisableCellsExpansions = serializable.DisableCellsExpansions;
			ShipExplosionEffect = loader?.GetVisualEffect(new ItemId<VisualEffect>(serializable.ShipExplosionEffect)) ?? VisualEffect.DefaultValue;
			ShipExplosionSound = new AudioClipId(serializable.ShipExplosionSound);
			DroneExplosionEffect = loader?.GetVisualEffect(new ItemId<VisualEffect>(serializable.DroneExplosionEffect)) ?? VisualEffect.DefaultValue;
			DroneExplosionSound = new AudioClipId(serializable.DroneExplosionSound);
			SatelliteModelScaleMultiplier = UnityEngine.Mathf.Clamp(serializable.SatelliteModelScaleMultiplier, 1f, 100f);
			SatelliteRequireModelScaleWithSizeClass = serializable.SatelliteRequireModelScaleWithSizeClass;
			_levelToPowerMultiplier = new Expressions.IntToFloat(serializable.LevelToPowerMultiplier, 1, 2147483647, variableResolver) { ParamName1 = "level" };
			LevelToPowerMultiplier = _levelToPowerMultiplier.Evaluate;
			TotalExpFormula = serializable.TotalExpFormula;

			OnDataDeserialized(serializable, loader);
		}

		public float DefaultWeightPerCell { get; private set; }
		public float MinimumWeightPerCell { get; private set; }
		public float BaseArmorPoints { get; private set; }
		public float ArmorPointsPerCell { get; private set; }
		public float ArmorRepairCooldown { get; private set; }
		public float BaseEnergyPoints { get; private set; }
		public float BaseEnergyRechargeRate { get; private set; }
		public float EnergyRechargeCooldown { get; private set; }
		public float BaseShieldRechargeRate { get; private set; }
		public float ShieldRechargeCooldown { get; private set; }
		public float BaseDroneReconstructionSpeed { get; private set; }
		public float ShieldCorrosiveResistance { get; private set; }
		public float MaxVelocity { get; private set; }
		public float MaxAngularVelocity { get; private set; }
		public float MaxAcceleration { get; private set; }
		public float MaxAngularAcceleration { get; private set; }
		public bool DisableCellsExpansions { get; private set; }
		public VisualEffect ShipExplosionEffect { get; private set; }
		public AudioClipId ShipExplosionSound { get; private set; }
		public VisualEffect DroneExplosionEffect { get; private set; }
		public AudioClipId DroneExplosionSound { get; private set; }
		public float SatelliteModelScaleMultiplier { get; private set; }
		public bool SatelliteRequireModelScaleWithSizeClass { get; private set; }
		private readonly Expressions.IntToFloat _levelToPowerMultiplier;
		public delegate float LevelToPowerMultiplierDelegate(int level);
		public LevelToPowerMultiplierDelegate LevelToPowerMultiplier { get; private set; }
		public string TotalExpFormula { get; private set; }

		public static ShipSettings DefaultValue { get; private set; }

		private class VariableResolver : IVariableResolver
		{
			private ShipSettings _context;

			public VariableResolver(ShipSettings context)
			{
				_context = context;
			}

			public IFunction<Variant> ResolveFunction(string name)
            {
				if (name == "LevelToPowerMultiplier") return _context._levelToPowerMultiplier;
				return ((IVariableResolver)_context).ResolveFunction(name);
			}

			public Expression<Variant> ResolveVariable(string name)
			{
				if (name == "DefaultWeightPerCell") return GetDefaultWeightPerCell;
				if (name == "MinimumWeightPerCell") return GetMinimumWeightPerCell;
				if (name == "BaseArmorPoints") return GetBaseArmorPoints;
				if (name == "ArmorPointsPerCell") return GetArmorPointsPerCell;
				if (name == "ArmorRepairCooldown") return GetArmorRepairCooldown;
				if (name == "BaseEnergyPoints") return GetBaseEnergyPoints;
				if (name == "BaseEnergyRechargeRate") return GetBaseEnergyRechargeRate;
				if (name == "EnergyRechargeCooldown") return GetEnergyRechargeCooldown;
				if (name == "BaseShieldRechargeRate") return GetBaseShieldRechargeRate;
				if (name == "ShieldRechargeCooldown") return GetShieldRechargeCooldown;
				if (name == "BaseDroneReconstructionSpeed") return GetBaseDroneReconstructionSpeed;
				if (name == "ShieldCorrosiveResistance") return GetShieldCorrosiveResistance;
				if (name == "MaxVelocity") return GetMaxVelocity;
				if (name == "MaxAngularVelocity") return GetMaxAngularVelocity;
				if (name == "MaxAcceleration") return GetMaxAcceleration;
				if (name == "MaxAngularAcceleration") return GetMaxAngularAcceleration;
				if (name == "DisableCellsExpansions") return GetDisableCellsExpansions;
				if (name == "SatelliteModelScaleMultiplier") return GetSatelliteModelScaleMultiplier;
				if (name == "SatelliteRequireModelScaleWithSizeClass") return GetSatelliteRequireModelScaleWithSizeClass;
				return ((IVariableResolver)_context).ResolveVariable(name);
			}

			private Variant GetDefaultWeightPerCell() => _context.DefaultWeightPerCell;
			private Variant GetMinimumWeightPerCell() => _context.MinimumWeightPerCell;
			private Variant GetBaseArmorPoints() => _context.BaseArmorPoints;
			private Variant GetArmorPointsPerCell() => _context.ArmorPointsPerCell;
			private Variant GetArmorRepairCooldown() => _context.ArmorRepairCooldown;
			private Variant GetBaseEnergyPoints() => _context.BaseEnergyPoints;
			private Variant GetBaseEnergyRechargeRate() => _context.BaseEnergyRechargeRate;
			private Variant GetEnergyRechargeCooldown() => _context.EnergyRechargeCooldown;
			private Variant GetBaseShieldRechargeRate() => _context.BaseShieldRechargeRate;
			private Variant GetShieldRechargeCooldown() => _context.ShieldRechargeCooldown;
			private Variant GetBaseDroneReconstructionSpeed() => _context.BaseDroneReconstructionSpeed;
			private Variant GetShieldCorrosiveResistance() => _context.ShieldCorrosiveResistance;
			private Variant GetMaxVelocity() => _context.MaxVelocity;
			private Variant GetMaxAngularVelocity() => _context.MaxAngularVelocity;
			private Variant GetMaxAcceleration() => _context.MaxAcceleration;
			private Variant GetMaxAngularAcceleration() => _context.MaxAngularAcceleration;
			private Variant GetDisableCellsExpansions() => _context.DisableCellsExpansions;
			private Variant GetSatelliteModelScaleMultiplier() => _context.SatelliteModelScaleMultiplier;
			private Variant GetSatelliteRequireModelScaleWithSizeClass() => _context.SatelliteRequireModelScaleWithSizeClass;
		}
	}
}
