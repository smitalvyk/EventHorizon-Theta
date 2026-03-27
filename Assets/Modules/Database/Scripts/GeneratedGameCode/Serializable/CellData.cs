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
		public string Color;
		public string Image;
		public bool EnableCustomShapeHighlight = false;
		public bool MergeCells = false;
	}
}
