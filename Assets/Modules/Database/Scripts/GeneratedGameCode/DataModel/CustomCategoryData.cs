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
	public partial class CustomCategoryData 
	{
		partial void OnDataDeserialized(CustomCategoryDataSerializable serializable, Database.Loader loader);

		public static CustomCategoryData Create(CustomCategoryDataSerializable serializable, Database.Loader loader)
		{
			return serializable == null ? DefaultValue : new CustomCategoryData(serializable, loader);
		}

		private CustomCategoryData(CustomCategoryDataSerializable serializable, Database.Loader loader)
		{
			Id = UnityEngine.Mathf.Clamp(serializable.Id, 1, 9999);
			Name = serializable.Name;
			Icon = serializable.Icon;
			ParentId = UnityEngine.Mathf.Clamp(serializable.ParentId, 0, 9999);
			AlwaysShow = serializable.AlwaysShow;
			Priority = UnityEngine.Mathf.Clamp(serializable.Priority, -2147483648, 2147483647);

			OnDataDeserialized(serializable, loader);
		}

		public int Id { get; private set; }
		public string Name { get; private set; }
		public string Icon { get; private set; }
		public int ParentId { get; private set; }
		public bool AlwaysShow { get; private set; }
		public int Priority { get; private set; }

		public static CustomCategoryData DefaultValue { get; private set; }= new(new(), null);
	}
}
