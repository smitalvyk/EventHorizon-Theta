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
	public partial class CustomCategorySettings 
	{
		partial void OnDataDeserialized(CustomCategorySettingsSerializable serializable, Database.Loader loader);

		public static CustomCategorySettings Create(CustomCategorySettingsSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new CustomCategorySettings(serializable, loader);
		}

		private CustomCategorySettings(CustomCategorySettingsSerializable serializable, Database.Loader loader)
		{
			Categories = new ImmutableCollection<CustomCategoryData>(serializable.Categories?.Select(item => CustomCategoryData.Create(item, loader)));

			OnDataDeserialized(serializable, loader);
		}

		public ImmutableCollection<CustomCategoryData> Categories { get; private set; }

		public static CustomCategorySettings DefaultValue { get; private set; }
	}
}
