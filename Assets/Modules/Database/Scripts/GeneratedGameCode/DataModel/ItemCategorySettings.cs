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
	public partial class ItemCategorySettings 
	{
		partial void OnDataDeserialized(ItemCategorySettingsSerializable serializable, Database.Loader loader);

		public static ItemCategorySettings Create(ItemCategorySettingsSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new ItemCategorySettings(serializable, loader);
		}

		private ItemCategorySettings(ItemCategorySettingsSerializable serializable, Database.Loader loader)
		{
			Categories = new ImmutableCollection<ItemCategoryData>(serializable.Categories?.Select(item => ItemCategoryData.Create(item, loader)));

			OnDataDeserialized(serializable, loader);
		}

		public ImmutableCollection<ItemCategoryData> Categories { get; private set; }

		public static ItemCategorySettings DefaultValue { get; private set; }
	}
}
