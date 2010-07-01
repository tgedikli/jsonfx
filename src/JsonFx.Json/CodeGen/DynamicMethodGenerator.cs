﻿#region License
/*---------------------------------------------------------------------------------*\

	Distributed under the terms of an MIT-style license:

	The MIT License

	Copyright (c) 2006-2010 Stephen M. McKamey

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in
	all copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
	THE SOFTWARE.

\*---------------------------------------------------------------------------------*/
#endregion License

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace JsonFx.CodeGen
{
	/// <summary>
	/// Generalized delegate for invoking a constructor
	/// </summary>
	/// <param name="args"></param>
	/// <returns></returns>
	public delegate object FactoryDelegate(params object[] args);

	/// <summary>
	/// Generalized delegate for getting a field or property value
	/// </summary>
	/// <param name="target"></param>
	/// <returns></returns>
	public delegate object GetterDelegate(object target);

	/// <summary>
	/// Generalized delegate for setting a field or property value
	/// </summary>
	/// <param name="target"></param>
	/// <param name="value"></param>
	public delegate void SetterDelegate(object target, object value);

	/// <summary>
	/// Generates delegates for getting/setting properties and field and invoking constructors
	/// </summary>
	internal static class DynamicMethodGenerator
	{
		#region Getter / Setter Generators

		/// <summary>
		/// Creates a field getter delegate for the specified property or field
		/// </summary>
		/// <param name="memberInfo">PropertyInfo or FieldInfo</param>
		/// <returns>GetterDelegate for property or field, null otherwise</returns>
		public static GetterDelegate GetGetter(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
			{
				return DynamicMethodGenerator.GetPropertyGetter((PropertyInfo)memberInfo);
			}

			if (memberInfo is FieldInfo)
			{
				return DynamicMethodGenerator.GetFieldGetter((FieldInfo)memberInfo);
			}

			return null;
		}

		/// <summary>
		/// Creates a field setter delegate for the specified property or field
		/// </summary>
		/// <param name="memberInfo">PropertyInfo or FieldInfo</param>
		/// <returns>SetterDelegate for property or field, null otherwise</returns>
		public static SetterDelegate GetSetter(MemberInfo memberInfo)
		{
			if (memberInfo is PropertyInfo)
			{
				return DynamicMethodGenerator.GetPropertySetter((PropertyInfo)memberInfo);
			}

			if (memberInfo is FieldInfo)
			{
				return DynamicMethodGenerator.GetFieldSetter((FieldInfo)memberInfo);
			}

			return null;
		}

		/// <summary>
		/// Creates a property getter delegate for the specified property
		/// </summary>
		/// <param name="propertyInfo"></param>
		/// <returns>GetterDelegate if property CanRead, otherwise null</returns>
		public static GetterDelegate GetPropertyGetter(PropertyInfo propertyInfo)
		{
			if (propertyInfo == null)
			{
				throw new ArgumentNullException("propertyInfo");
			}

			if (!propertyInfo.CanRead)
			{
				return null;
			}

			MethodInfo methodInfo = propertyInfo.GetGetMethod(true);
			if (methodInfo == null ||
				methodInfo.IsAbstract)
			{
				return null;
			}

			// Create a dynamic method with a return type of object, and one parameter taking the instance.
			// Create the method in the module that owns the instance type
			DynamicMethod dynamicMethod = new DynamicMethod(
				"",//propertyInfo.DeclaringType.FullName+".get_"+propertyInfo.Name,
				typeof(object),
				new Type[] { typeof(object) },
				propertyInfo.DeclaringType.Module,
				true);

			// Get an ILGenerator and emit a body for the dynamic method,
			// using a stream size larger than the IL that will be emitted.
			ILGenerator il = dynamicMethod.GetILGenerator(64 * 5);
			if (methodInfo.IsStatic)
			{
				// TODO: what goes here?
			}
			else
			{
				// Load the target instance onto the evaluation stack
				il.Emit(OpCodes.Ldarg_0);
			}
			// Call the method that returns void
			il.Emit(methodInfo.IsVirtual ? OpCodes.Callvirt :  OpCodes.Call, methodInfo);
			if (propertyInfo.PropertyType.IsValueType)
			{
				// Load the return value as a reference type
				il.Emit(OpCodes.Box, propertyInfo.PropertyType);
			}
			// return property value from the method
			il.Emit(OpCodes.Ret);

			// produce a delegate that we can then call
			return (GetterDelegate)dynamicMethod.CreateDelegate(typeof(GetterDelegate));
		}

		/// <summary>
		/// Creates a property setter delegate for the specified property
		/// </summary>
		/// <param name="propertyInfo"></param>
		/// <returns>GetterDelegate if property CanWrite, otherwise null</returns>
		public static SetterDelegate GetPropertySetter(PropertyInfo propertyInfo)
		{
			if (propertyInfo == null)
			{
				throw new ArgumentNullException("propertyInfo");
			}

			if (!propertyInfo.CanWrite)
			{
				return null;
			}

			MethodInfo methodInfo = propertyInfo.GetSetMethod(true);
			if (methodInfo == null ||
				methodInfo.IsAbstract)
			{
				return null;
			}

			// Create a dynamic method with a return type of void, one parameter taking the instance and the other taking the new value.
			// Create the method in the module that owns the instance type
			DynamicMethod dynamicMethod = new DynamicMethod(
				"",//propertyInfo.DeclaringType.FullName+".set_"+propertyInfo.Name,
				null,
				new Type[] { typeof(object), typeof(object) },
				propertyInfo.DeclaringType.Module,
				true);

			// Get an ILGenerator and emit a body for the dynamic method,
			// using a stream size larger than the IL that will be emitted.
			ILGenerator il = dynamicMethod.GetILGenerator(64 * 5);

			if (methodInfo.IsStatic)
			{
				// TODO: what goes here?
			}
			else
			{
				// Load the target instance onto the evaluation stack
				il.Emit(OpCodes.Ldarg_0);
			}
			// Load the argument onto the evaluation stack
			il.Emit(OpCodes.Ldarg_1);
			if (propertyInfo.PropertyType.IsValueType)
			{
				// unbox the argument as a value type
				il.Emit(OpCodes.Unbox_Any, propertyInfo.PropertyType);
			}
			// Call the method that returns void
			il.Emit(methodInfo.IsVirtual ? OpCodes.Callvirt :  OpCodes.Call, methodInfo);
			// return (void) from the method
			il.Emit(OpCodes.Ret);

			// produce a delegate that we can then call
			return (SetterDelegate)dynamicMethod.CreateDelegate(typeof(SetterDelegate));
		}

		/// <summary>
		/// Creates a field getter delegate for the specified field
		/// </summary>
		/// <param name="fieldInfo"></param>
		/// <returns></returns>
		public static GetterDelegate GetFieldGetter(FieldInfo fieldInfo)
		{
			if (fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}

			// Create a dynamic method with a return type of object, one parameter taking the instance.
			// Create the method in the module that owns the instance type
			DynamicMethod dynamicMethod = new DynamicMethod(
				"",//fieldInfo.DeclaringType.FullName+".get_"+fieldInfo.Name,
				typeof(object),
				new Type[] { typeof(object) },
				fieldInfo.DeclaringType.Module,
				true);

			// Get an ILGenerator and emit a body for the dynamic method,
			// using a stream size larger than the IL that will be emitted.
			ILGenerator il = dynamicMethod.GetILGenerator(64 * 5);

			if (fieldInfo.IsStatic && fieldInfo.DeclaringType.IsEnum)
			{
				object value = fieldInfo.GetValue(null);
				switch (Type.GetTypeCode(fieldInfo.FieldType))
				{
					case TypeCode.Byte:
					{
						il.Emit(OpCodes.Ldc_I4_S, (byte)value);
						break;
					}
					case TypeCode.SByte:
					{
						il.Emit(OpCodes.Ldc_I4_S, (sbyte)value);
						break;
					}
					case TypeCode.Int16:
					{
						il.Emit(OpCodes.Ldc_I4_S, (short)value);
						break;
					}
					case TypeCode.UInt16:
					{
						il.Emit(OpCodes.Ldc_I4_S, (ushort)value);
						break;
					}
					case TypeCode.Int32:
					{
						il.Emit(OpCodes.Ldc_I4, (int)value);
						break;
					}
					case TypeCode.UInt32:
					{
						il.Emit(OpCodes.Ldc_I4, (uint)value);
						break;
					}
					case TypeCode.Int64:
					{
						il.Emit(OpCodes.Ldc_I8, (long)value);
						break;
					}
					case TypeCode.UInt64:
					{
						il.Emit(OpCodes.Ldc_I8, (ulong)value);
						break;
					}
					default:
					{
						return null;
					}
				}

				// Load the field value as a reference type
				il.Emit(OpCodes.Box, fieldInfo.DeclaringType);
			}
			else
			{
				// Load the target instance onto the evaluation stack
				il.Emit(OpCodes.Ldarg_0);
				// Load the field
				il.Emit(OpCodes.Ldfld, fieldInfo);
				if (fieldInfo.FieldType.IsValueType)
				{
					// box the field value as a reference type
					il.Emit(OpCodes.Box, fieldInfo.FieldType);
				}
			}
			// return field value from the method
			il.Emit(OpCodes.Ret);

			// produce a delegate that we can then call
			return (GetterDelegate)dynamicMethod.CreateDelegate(typeof(GetterDelegate));
		}

		/// <summary>
		/// Creates a field setter delegate for the specified field
		/// </summary>
		/// <param name="fieldInfo"></param>
		/// <returns>SetterDelegate unless field IsInitOnly then returns null</returns>
		public static SetterDelegate GetFieldSetter(FieldInfo fieldInfo)
		{
			if (fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}

			if (fieldInfo.IsInitOnly ||
				fieldInfo.IsLiteral)
			{
				return null;
			}

			// Create a dynamic method with a return type of void, one parameter taking the instance and the other taking the new value.
			// Create the method in the module that owns the instance type
			DynamicMethod dynamicMethod = new DynamicMethod(
				"",//fieldInfo.DeclaringType.FullName+".set_"+fieldInfo.Name,
				null,
				new Type[] { typeof(object), typeof(object) },
				fieldInfo.DeclaringType.Module,
				true);

			// Get an ILGenerator and emit a body for the dynamic method,
			// using a stream size larger than the IL that will be emitted.
			ILGenerator il = dynamicMethod.GetILGenerator(64 * 5);

			// Load the target instance onto the evaluation stack
			il.Emit(OpCodes.Ldarg_0);
			// Load the argument onto the evaluation stack
			il.Emit(OpCodes.Ldarg_1);
			if (fieldInfo.FieldType.IsValueType)
			{
				// unbox the argument as a value type
				il.Emit(OpCodes.Unbox_Any, fieldInfo.FieldType);
			}
			// Set the field
			il.Emit(OpCodes.Stfld, fieldInfo);
			// return (void) from the method
			il.Emit(OpCodes.Ret);

			// produce a delegate that we can then call
			return (SetterDelegate)dynamicMethod.CreateDelegate(typeof(SetterDelegate));
		}

		#endregion Getter / Setter Generators

		#region Type Factory Generators

		/// <summary>
		/// Creates a constructor delegate accepting specified arguments
		/// </summary>
		/// <param name="type"></param>
		/// <param name="argsCount"></param>
		/// <returns>FactoryDelegate or null if constructor not found</returns>
		public static FactoryDelegate GetTypeFactory(Type type, params Type[] args)
		{
			if (type == null)
			{
				throw new ArgumentNullException("type");
			}

			ConstructorInfo ctor = type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic, null, args, null);
			if (ctor == null)
			{
				return null;
			}

			// Create a dynamic method with a return type of object and one parameter for each argument.
			// Create the method in the module that owns the instance type
			DynamicMethod dynamicMethod = new DynamicMethod(
				"",//type.FullName+".ctor_"+args.Length,
				typeof(object),
				new Type[] { typeof(object[]) },
				type.Module,
				true);

			// Get an ILGenerator and emit a body for the dynamic method,
			// using a stream size larger than the IL that will be emitted.
			ILGenerator il = dynamicMethod.GetILGenerator(64 * (args.Length+5));

			for (int i=0; i<args.Length; i++)
			{
				// Load the argument from params array onto the evaluation stack
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldelem_Ref);
				if (args[i].IsValueType)
				{
					il.Emit(OpCodes.Unbox_Any, args[i]);
				}
				else
				{
					il.Emit(OpCodes.Castclass, args[i]);
				}
			}

			// Call the ctor, passing in the stack of args, result is put back on stack
			il.Emit(OpCodes.Newobj, ctor);
			if (type.IsValueType)
			{
				// box the return value as a reference type
				il.Emit(OpCodes.Box, type);
			}
			// return result from the method
			il.Emit(OpCodes.Ret);

			// produce a delegate that we can then call
			return (FactoryDelegate)dynamicMethod.CreateDelegate(typeof(FactoryDelegate));
		}

		#endregion Type Factory Generators
	}
}
