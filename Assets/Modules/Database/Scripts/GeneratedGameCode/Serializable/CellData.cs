//-------------------------------------------------------------------------------
//                                                                               
//    This code was automatically generated.                                     
//    Changes to this file may cause incorrect behavior and will be lost if      
//    the code is regenerated.                                                   
//                                                                               
//-------------------------------------------------------------------------------

using System;
using GameDatabase.Enums;
using GameDatabase.Model;

namespace GameDatabase.Serializable
{
	[Serializable]
	public class CellDataSerializable
	{
		public string Symbol;
		public string Color = "#00000000";
		public string Color2 = "#00000000";
		public string Color3 = "#00000000";
		public string Color4 = "#00000000";
		public string Image;
		public bool EnableCustomShapeHighlight = false;
		public bool MergeCells = false;
		public string AllowedCustomCells;
		public bool ShowInShipyard = true;
		public string ShipyardPlacementRule;
	}
}
