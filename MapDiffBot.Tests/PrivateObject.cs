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

		public static object InvokeMethod(object instance, Type type, string name, object[] arguments = null)
		{
			if (type == null)
				throw new ArgumentNullException(nameof(type));
			var method = type.GetMethod(name ?? throw new ArgumentNullException(nameof(name)), (instance == null ? BindingFlags.Static : BindingFlags.Instance) | BindingFlags.NonPublic);
			if (method == null)
				throw new ArgumentOutOfRangeException(nameof(name), name, "Method does not exist!");
			if (arguments == null)
				arguments = Array.Empty<object>();
			return method.Invoke(instance, arguments);
		}
	}

	static class PrivateObject<InternalObject> where InternalObject : class
	{
		public static void SetField<T>(InternalObject instance, string name, T newValue) => PrivateObject.SetField(instance, typeof(InternalObject), name, newValue);

		public static T InvokeMethod<T>(InternalObject instance, string name, object[] arguments = null) => (T)PrivateObject.InvokeMethod(instance, typeof(InternalObject), name, arguments);
	}
}
