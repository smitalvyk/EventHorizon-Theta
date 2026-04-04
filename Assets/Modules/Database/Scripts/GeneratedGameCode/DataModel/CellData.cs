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
			Color2 = new ColorData(serializable.Color2);
			Color3 = new ColorData(serializable.Color3);
			Color4 = new ColorData(serializable.Color4);
			Image = serializable.Image;
			EnableCustomShapeHighlight = serializable.EnableCustomShapeHighlight;
			MergeCells = serializable.MergeCells;
			AllowedCustomCells = serializable.AllowedCustomCells;
			ShowInShipyard = serializable.ShowInShipyard;
			ShipyardPlacementRule = serializable.ShipyardPlacementRule;

			OnDataDeserialized(serializable, loader);
		}

		public string Symbol { get; private set; }
		public ColorData Color { get; private set; }
		public ColorData Color2 { get; private set; }
		public ColorData Color3 { get; private set; }
		public ColorData Color4 { get; private set; }
		public string Image { get; private set; }
		public bool EnableCustomShapeHighlight { get; private set; }
		public bool MergeCells { get; private set; }
		public string AllowedCustomCells { get; private set; }
		public bool ShowInShipyard { get; private set; }
		public string ShipyardPlacementRule { get; private set; }

		public static CellData DefaultValue { get; private set; }= new(new(), null);
	}
}
