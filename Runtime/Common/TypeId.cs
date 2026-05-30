using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Unity.IL2CPP.CompilerServices;

// ReSharper disable all StaticMemberInGenericType
namespace Massive
{
	[Il2CppEagerStaticClassConstruction]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class TypeId<TKind>
	{
		// Guards the non-concurrent s_typeInfo / s_types registry below. Distinct
		// TypeId<TKind, T> static constructors first-touched on different threads
		// all funnel into Register, so the registry must be serialized.
		private static readonly object s_lock = new object();
		private static readonly Dictionary<Type, TypeIdInfo> s_typeInfo = new Dictionary<Type, TypeIdInfo>();
		private static Type[] s_types = Array.Empty<Type>();
		private static int s_typeCounter = -1;

		[MethodImpl(MethodImplOptions.NoInlining)]
		public static TypeIdInfo GetInfo(Type type)
		{
			if (TryGetInfo(type, out var typeIdInfo))
			{
				return typeIdInfo;
			}

			// Run the TypeId<TKind, T> static constructor WITHOUT holding s_lock:
			// it calls Register, which takes s_lock itself. Holding s_lock across
			// the cctor would invert the lock order against the CLR's per-type
			// initialization lock and can deadlock under concurrent warmup.
			WarmupTypeId(type);

			lock (s_lock)
			{
				return s_typeInfo[type];
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool TryGetInfo(Type type, out TypeIdInfo info)
		{
			lock (s_lock)
			{
				return s_typeInfo.TryGetValue(type, out info);
			}
		}

		public static Type GetTypeByIndex(int index)
		{
			lock (s_lock)
			{
				if (index >= s_types.Length)
				{
					return null;
				}

				return s_types[index];
			}
		}

		public static void WarmupTypeId(Type type)
		{
			try
			{
				var typeId = typeof(TypeId<>).MakeGenericType(type);
				RuntimeHelpers.RunClassConstructor(typeId.TypeHandle);
			}
			catch
			{
				MassiveException.Throw(
					$"The type identifier for {type} has been stripped from the build.\n" +
					"Ensure that the type is used in your codebase as a generic argument with the library API.");
			}
		}

		internal static int IncrementTypeCounter()
		{
			return Interlocked.Increment(ref s_typeCounter);
		}

		internal static void Register(Type type, TypeIdInfo info)
		{
			lock (s_lock)
			{
				s_typeInfo.Add(type, info);

				if (info.Index >= s_types.Length)
				{
					s_types = s_types.ResizeToNextPowOf2(info.Index + 1);
				}
				s_types[info.Index] = type;
			}
		}
	}

	[Il2CppEagerStaticClassConstruction]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public static class TypeId<TKind, T>
	{
		public static readonly TypeIdInfo Info;

		static TypeId()
		{
			var type = typeof(T);
			var index = TypeId<TKind>.IncrementTypeCounter();
			var typeName = type.GetFullGenericName();

			var info = new TypeIdInfo(index, typeName, type);

			Info = info;

			TypeId<TKind>.Register(type, info);
		}
	}
}
