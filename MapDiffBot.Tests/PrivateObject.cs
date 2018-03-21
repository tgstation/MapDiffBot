using System;
using System.Reflection;

namespace MapDiffBot.Tests
{
	static class PrivateObject
	{
		public static void SetField<T>(object instance, Type type, string name, T newValue)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			var field = type.GetField(name ?? throw new ArgumentNullException(nameof(name)), (instance == null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic);
			if (field == null)
				throw new ArgumentOutOfRangeException(nameof(name), name, "Field does not exist!");
			field.SetValue(instance, newValue);
		}
	}

	static class PrivateObject<InternalObject> where InternalObject : class
	{
		public static void SetField<T>(InternalObject instance, string name, T newValue) => PrivateObject.SetField(instance, typeof(InternalObject), name, newValue);
	}
}
