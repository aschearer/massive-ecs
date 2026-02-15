#if !MASSIVE_DISABLE_ASSERT
#define MASSIVE_ASSERT
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public partial class Sets
	{
		private Dictionary<string, BitSet> SetsByNames { get; } = new Dictionary<string, BitSet>();

		private FastList<string> Names { get; } = new FastList<string>();

		private FastList<SetCloner> Cloners { get; } = new FastList<SetCloner>();

		public BitSetList Sorted { get; } = new BitSetList();

		public BitSet[] LookupByTypeId { get; private set; } = Array.Empty<BitSet>();

		public BitSet[] LookupByComponentId { get; private set; } = Array.Empty<BitSet>();

		public int LookupCapacity { get; private set; }

		public int ComponentCount { get; private set; }

		private SetFactory SetFactory { get; }

		private Components Components { get; }

		public Sets(SetFactory setFactory, Components components)
		{
			SetFactory = setFactory;
			Components = components;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BitSet GetExisting(string setId)
		{
			if (SetsByNames.TryGetValue(setId, out var set))
			{
				return set;
			}

			return null;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BitSet Get<T>()
		{
			var info = TypeId<SetKind, T>.Info;

			EnsureLookupByTypeAt(info.Index);
			var candidate = LookupByTypeId[info.Index];

			if (candidate != null)
			{
				return candidate;
			}

			// When the same name is registered under a different Type (e.g., after
			// hot-reloading an assembly via collectible AssemblyLoadContext), replace
			// the old set so the generic DataSet<T> cast matches the new Type.
			var existing = GetExisting(info.FullName);
			if (existing != null)
			{
				var (newSet, newCloner) = SetFactory.CreateAppropriateSet<T>();
				ReplaceSet(info.FullName, existing, newSet, newCloner);
				LookupByTypeId[info.Index] = newSet;
				newSet.SetupComponent(this, Components, info.Index);
				if (existing.IsComponentBound)
				{
					var componentId = existing.ComponentId;
					existing.UnbindComponentId();
					newSet.BindComponentId(componentId);
					LookupByComponentId[componentId] = newSet;
				}

				// Carry entity membership and data forward from the old set.
				// existing is unbound so CopyBitSetTo skips EnsureBinded.
				existing.CopyBitSetTo(newSet);
				MigrateData(existing, newSet);

				return newSet;
			}

			var (set, cloner) = SetFactory.CreateAppropriateSet<T>();

			InsertSet(info.FullName, set, cloner);
			LookupByTypeId[info.Index] = set;

			set.SetupComponent(this, Components, info.Index);

			return set;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BitSet GetReflected(Type setType)
		{
			if (TypeId<SetKind>.TryGetInfo(setType, out var info))
			{
				EnsureLookupByTypeAt(info.Index);
				var candidate = LookupByTypeId[info.Index];

				if (candidate != null)
				{
					return candidate;
				}
			}

			var createMethod = typeof(Sets).GetMethod(nameof(Get));
			var genericMethod = createMethod?.MakeGenericMethod(setType);
			return (BitSet)genericMethod?.Invoke(this, new object[] { });
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureLookupByTypeAt(int index)
		{
			if (index >= LookupCapacity)
			{
				LookupByTypeId = LookupByTypeId.ResizeToNextPowOf2(index + 1);
				LookupCapacity = LookupByTypeId.Length;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureLookupByComponentAt(int index)
		{
			if (index >= LookupByComponentId.Length)
			{
				LookupByComponentId = LookupByComponentId.ResizeToNextPowOf2(index + 1);
				Components.EnsureComponentsCapacity(LookupByComponentId.Length);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void EnsureBinded(BitSet set)
		{
			if (set.IsComponentBound)
			{
				return;
			}

			var componentId = ComponentCount++;
			EnsureLookupByComponentAt(componentId);
			set.BindComponentId(componentId);
			LookupByComponentId[componentId] = set;
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void ReplaceSet(string setName, BitSet oldSet, BitSet newSet, SetCloner cloner)
		{
			var itemIndex = Names.BinarySearch(setName);
			Sorted[itemIndex] = newSet;
			Cloners[itemIndex] = cloner;
			SetsByNames[setName] = newSet;
		}

		/// <summary>
		/// Migrates entity data from <paramref name="oldSet"/> into <paramref name="newSet"/>
		/// after a hot-reload type replacement. Entity membership (bitset) must already be
		/// copied before calling this method.
		/// </summary>
		[MethodImpl(MethodImplOptions.NoInlining)]
		private static void MigrateData(BitSet oldSet, BitSet newSet)
		{
			if (oldSet is not IDataSet oldData || newSet is not IDataSet newData)
			{
				// Tag-only sets (no data) — membership copy is sufficient.
				return;
			}

			// Ensure pages exist in the new set for all non-empty pages.
			var deBruijn = MathUtils.DeBruijn;

			for (var blockIndex = 0; blockIndex < newSet.BlocksCapacity; blockIndex++)
			{
				var block = newSet.NonEmptyBlocks[blockIndex];
				var pageOffset = blockIndex << Constants.PagesInBlockPower;
				while (block != 0UL)
				{
					var blockBit = (int)deBruijn[(int)(((block & (ulong)-(long)block) * 0x37E84A99DAE458FUL) >> 58)];
					var pageIndexMod = blockBit >> Constants.PageMaskShift;
					var pageIndex = pageOffset + pageIndexMod;

					newData.EnsurePage(pageIndex);

					block &= Constants.PageMasksNegative[pageIndexMod];
				}
			}

			// Field-by-field migration via reflection.
			// Matches fields by name and type. New fields default to zero, removed fields dropped.
			var oldType = oldData.ElementType;
			var newType = newData.ElementType;
			var oldFields = oldType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var newFields = newType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

			// Build a map of matching fields: newField -> oldField
			var fieldMap = new List<(FieldInfo OldField, FieldInfo NewField)>();
			foreach (var newField in newFields)
			{
				foreach (var oldField in oldFields)
				{
					if (oldField.Name == newField.Name && oldField.FieldType == newField.FieldType)
					{
						fieldMap.Add((oldField, newField));
						break;
					}
				}
			}

			// If no fields match, pages are already default-initialized — nothing to do.
			if (fieldMap.Count == 0)
			{
				return;
			}

			// Iterate all entities and migrate matching fields.
			for (var bitsIndex = 0; bitsIndex < newSet.Bits.Length; bitsIndex++)
			{
				var bits = newSet.Bits[bitsIndex];
				while (bits != 0UL)
				{
					var bit = (int)deBruijn[(int)(((bits & (ulong)-(long)bits) * 0x37E84A99DAE458FUL) >> 58)];
					var id = (bitsIndex << 6) + bit;

					var oldValue = oldData.GetRaw(id);
					var newValue = newData.GetRaw(id);

					foreach (var (oldField, newField) in fieldMap)
					{
						newField.SetValue(newValue, oldField.GetValue(oldValue));
					}

					newData.SetRaw(id, newValue);

					bits &= bits - 1UL;
				}
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void InsertSet(string setName, BitSet set, SetCloner cloner)
		{
			// Maintain items sorted.
			var itemIndex = Names.BinarySearch(setName);
			if (itemIndex >= 0)
			{
				MassiveException.Throw($"You are trying to insert already existing item:{setName}.");
			}
			else
			{
				var insertionIndex = ~itemIndex;
				Names.Insert(insertionIndex, setName);
				Sorted.Insert(insertionIndex, set);
				Cloners.Insert(insertionIndex, cloner);
				SetsByNames.Add(setName, set);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(BitSet bitSet)
		{
			return bitSet.TypeId;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Type TypeOf(BitSet bitSet)
		{
			return TypeId<SetKind>.GetTypeByIndex(bitSet.TypeId);
		}

		public void Reset()
		{
			for (var i = 0; i < ComponentCount; i++)
			{
				ref var set = ref LookupByComponentId[i];
				set.UnbindComponentId();
				set.Reset();
				set = null;
			}

			ComponentCount = 0;
		}

		/// <summary>
		/// Copies all sets from this registry into the specified one.
		/// Clears sets in the target registry that are not present in the source.
		/// </summary>
		/// <remarks>
		/// Throws if the set factories are incompatible.
		/// </remarks>
		public void CopyTo(Sets other)
		{
			IncompatibleConfigsException.ThrowIfIncompatible(SetFactory, other.SetFactory);

			// Copy present sets.
			foreach (var cloner in Cloners)
			{
				cloner.CopyTo(other);
			}

			other.EnsureLookupByComponentAt(ComponentCount - 1);

			// Reorder the target world's component IDs to match the current world's layout.
			// This ensures that both worlds use identical component indices for their masks.
			for (var i = 0; i < ComponentCount; i++)
			{
				var set = LookupByComponentId[i];
				ref var otherSet = ref other.LookupByComponentId[i];

				if (otherSet == null || otherSet.TypeId != set.TypeId)
				{
					var otherMatchedComponentId = other.LookupByTypeId[set.TypeId].ComponentId;
					ref var otherMatchedSet = ref other.LookupByComponentId[otherMatchedComponentId];

					// Rebind the outgoing DataSet to its new position BEFORE the swap
					// to prevent stale ComponentId lookups in subsequent iterations.
					if (otherSet != null)
					{
						otherSet.BindComponentId(otherMatchedComponentId);
					}

					(otherSet, otherMatchedSet) = (otherMatchedSet, otherSet);
				}

				otherSet.BindComponentId(i);
			}

			// Ubind other sets and reset them.
			for (var i = ComponentCount; i < other.ComponentCount; i++)
			{
				ref var otherSet = ref other.LookupByComponentId[i];
				otherSet.UnbindComponentId();
				otherSet.Reset();
				otherSet = null;
			}

			other.ComponentCount = ComponentCount;
		}
	}

	internal struct SetKind
	{
	}
}
