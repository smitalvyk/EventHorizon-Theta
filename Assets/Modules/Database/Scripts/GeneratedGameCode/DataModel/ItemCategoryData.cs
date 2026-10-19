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
	public partial class ItemCategoryData 
	{
		partial void OnDataDeserialized(ItemCategoryDataSerializable serializable, Database.Loader loader);

		public static ItemCategoryData Create(ItemCategoryDataSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new ItemCategoryData(serializable, loader);
		}

		private ItemCategoryData(ItemCategoryDataSerializable serializable, Database.Loader loader)
		{
			Id = UnityEngine.Mathf.Clamp(serializable.Id, 1, 9999);
			Name = serializable.Name;
			Icon = serializable.Icon;
			ParentId = UnityEngine.Mathf.Clamp(serializable.ParentId, 0, 9999);
			Fill = new ImmutableSet<CategoryTag>(serializable.Fill);
			Slots = serializable.Slots;

			OnDataDeserialized(serializable, loader);
		}

		public int Id { get; private set; }
		public string Name { get; private set; }
		public string Icon { get; private set; }
		public int ParentId { get; private set; }
		public ImmutableSet<CategoryTag> Fill { get; private set; }
		public string Slots { get; private set; }

		public static ItemCategoryData DefaultValue { get; private set; }= new(new(), null);
	}
}
