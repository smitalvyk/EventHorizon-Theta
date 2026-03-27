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

namespace GameDatabase.DataModel
{
	public partial class CellSettings 
	{
		partial void OnDataDeserialized(CellSettingsSerializable serializable, Database.Loader loader);

		public static CellSettings Create(CellSettingsSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new CellSettings(serializable, loader);
		}

		private CellSettings(CellSettingsSerializable serializable, Database.Loader loader)
		{
			Cells = new ImmutableCollection<CellData>(serializable.Cells?.Select(item => CellData.Create(item, loader)));

			OnDataDeserialized(serializable, loader);
		}

		public ImmutableCollection<CellData> Cells { get; private set; }

		public static CellSettings DefaultValue { get; private set; }
	}
}
