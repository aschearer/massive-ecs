using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
	internal interface IPageDiffer
	{
		void DiffPage(Array current, Array shadow,
			DiffSection section, int setIndex, int pageIndex, FrameDiff diff);

		void ApplyPageDiff(Array page,
			DiffEntry[] entries, int count, int setIndex, int pageIndex);
	}

	internal sealed class StructPageDiffer<TStruct> : IPageDiffer where TStruct : struct
	{
		public void DiffPage(Array current, Array shadow,
			DiffSection section, int setIndex, int pageIndex, FrameDiff diff)
		{
			WorldDiffUtils.DiffPage((TStruct[])current, (TStruct[])shadow,
				section, setIndex, pageIndex, diff);
		}

		public void ApplyPageDiff(Array page,
			DiffEntry[] entries, int count, int setIndex, int pageIndex)
		{
			WorldDiffUtils.ApplyPageDiff((TStruct[])page,
				entries, count, setIndex, pageIndex);
		}
	}

	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class DataSetCloner<T> : SetCloner
	{
		private readonly DataSet<T> _dataSet;
		private readonly IPageDiffer _pageDiffer;

		public DataSetCloner(DataSet<T> dataSet)
		{
			_dataSet = dataSet;

			// Create the struct-constrained differ only for unmanaged types.
			// For reference-containing types, page diffing is skipped entirely.
			if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				_pageDiffer = (IPageDiffer)Activator.CreateInstance(
					typeof(StructPageDiffer<>).MakeGenericType(typeof(T)));
			}
		}

		public override void CopyTo(Sets sets)
		{
			_dataSet.CopyTo((DataSet<T>)sets.Get<T>());
		}

		public override void DiffTo(Sets shadow, FrameDiff diff, int setIndex)
		{
			var other = (DataSet<T>)shadow.Get<T>();

			// Grow shadow bitset arrays if needed
			other.GrowToFit(_dataSet);

			// Diff BitSet arrays
			var blocksLength = Math.Max(_dataSet.BlocksCapacity, other.BlocksCapacity);

			if (blocksLength > 0)
			{
				var bitsLength = Math.Max(_dataSet.Bits.Length, other.Bits.Length);
				WorldDiffUtils.DiffUlongArrays(_dataSet.Bits, other.Bits, bitsLength,
					DiffSection.SetBits, setIndex, diff);
				WorldDiffUtils.DiffUlongArrays(_dataSet.NonEmptyBlocks, other.NonEmptyBlocks, blocksLength,
					DiffSection.SetNonEmpty, setIndex, diff);
				WorldDiffUtils.DiffUlongArrays(_dataSet.SaturatedBlocks, other.SaturatedBlocks, blocksLength,
					DiffSection.SetSaturated, setIndex, diff);
			}

			// Skip data page diffing for reference types
			if (_pageDiffer == null)
			{
				return;
			}

			// Diff data pages — union of pages from both live and shadow
			var deBruijn = MathUtils.DeBruijn;
			var pageMasksNegative = Constants.PageMasksNegative;

			for (var blockIndex = 0; blockIndex < blocksLength; blockIndex++)
			{
				var liveBlock = blockIndex < _dataSet.BlocksCapacity ? _dataSet.NonEmptyBlocks[blockIndex] : 0UL;
				var shadowBlock = blockIndex < other.BlocksCapacity ? other.NonEmptyBlocks[blockIndex] : 0UL;
				var unionBlock = liveBlock | shadowBlock;

				var pageOffset = blockIndex << Constants.PagesInBlockPower;
				while (unionBlock != 0UL)
				{
					var blockBit = (int)deBruijn[(int)(((unionBlock & (ulong)-(long)unionBlock) * 0x37E84A99DAE458FUL) >> 58)];
					var pageIndexMod = blockBit >> Constants.PageMaskShift;
					var pageIndex = pageOffset + pageIndexMod;

					var livePage = pageIndex < _dataSet.PagedData.Length ? _dataSet.PagedData[pageIndex] : null;
					var shadowPage = pageIndex < other.PagedData.Length ? other.PagedData[pageIndex] : null;

					if (livePage != null && shadowPage != null)
					{
						_pageDiffer.DiffPage(livePage, shadowPage,
							DiffSection.DataPage, setIndex, pageIndex, diff);
					}
					else if (livePage != null)
					{
						other.EnsurePageInternal(pageIndex);
						shadowPage = other.PagedData[pageIndex];
						_pageDiffer.DiffPage(livePage, shadowPage,
							DiffSection.DataPage, setIndex, pageIndex, diff);
					}
					else if (shadowPage != null)
					{
						_dataSet.EnsurePageInternal(pageIndex);
						livePage = _dataSet.PagedData[pageIndex];
						_pageDiffer.DiffPage(livePage, shadowPage,
							DiffSection.DataPage, setIndex, pageIndex, diff);
					}

					unionBlock &= pageMasksNegative[pageIndexMod];
				}
			}
		}

		public override void ApplyDiff(Sets target, FrameDiff diff, int setIndex)
		{
			var other = (DataSet<T>)target.Get<T>();
			var entries = diff.Entries;
			var count = diff.Count;

			// Grow target bitset if needed
			other.GrowToFit(_dataSet);

			// Apply BitSet diffs
			WorldDiffUtils.ApplyUlongArrayDiff(other.Bits, entries, count,
				DiffSection.SetBits, setIndex);
			WorldDiffUtils.ApplyUlongArrayDiff(other.NonEmptyBlocks, entries, count,
				DiffSection.SetNonEmpty, setIndex);
			WorldDiffUtils.ApplyUlongArrayDiff(other.SaturatedBlocks, entries, count,
				DiffSection.SetSaturated, setIndex);

			// Skip data page patching for reference types
			if (_pageDiffer == null)
			{
				return;
			}

			// Apply data page diffs page by page
			var lastPageIndex = -1;
			for (var i = 0; i < count; i++)
			{
				ref var entry = ref entries[i];
				if (entry.Section != DiffSection.DataPage || entry.SetIndex != setIndex)
				{
					continue;
				}

				var pageIndex = (int)entry.PageIndex;
				if (pageIndex == lastPageIndex)
				{
					continue; // Already processed this page
				}

				other.EnsurePageInternal(pageIndex);
				_pageDiffer.ApplyPageDiff(other.PagedData[pageIndex],
					entries, count, setIndex, pageIndex);
				lastPageIndex = pageIndex;
			}
		}
	}
}
