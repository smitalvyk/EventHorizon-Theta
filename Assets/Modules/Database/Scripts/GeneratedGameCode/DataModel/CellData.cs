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
	public partial class CellData 
	{
		partial void OnDataDeserialized(CellDataSerializable serializable, Database.Loader loader);

		public static CellData Create(CellDataSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new CellData(serializable, loader);
		}

		private CellData(CellDataSerializable serializable, Database.Loader loader)
		{
			Symbol = serializable.Symbol;
			Color = new ColorData(serializable.Color);
			Image = serializable.Image;
			EnableCustomShapeHighlight = serializable.EnableCustomShapeHighlight;
			MergeCells = serializable.MergeCells;

			OnDataDeserialized(serializable, loader);
		}

		public string Symbol { get; private set; }
		public ColorData Color { get; private set; }
		public string Image { get; private set; }
		public bool EnableCustomShapeHighlight { get; private set; }
		public bool MergeCells { get; private set; }

		public static CellData DefaultValue { get; private set; }= new(new(), null);
	}
}
