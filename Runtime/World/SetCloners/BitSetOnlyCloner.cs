using System;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
	/// <summary>
	/// Cloner that copies only the BitSet membership, not the data.
	/// Used for <see cref="INonRollback"/> components so their values
	/// survive SaveFrame/Rollback while entity membership stays consistent.
	/// </summary>
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class BitSetOnlyCloner<T> : SetCloner
	{
		private readonly DataSet<T> _dataSet;

		public BitSetOnlyCloner(DataSet<T> dataSet)
		{
			_dataSet = dataSet;
		}

		public override void CopyTo(Sets sets)
		{
			var target = (DataSet<T>)sets.Get<T>();
			_dataSet.CopyBitSetTo(target);
			if (target.RetainPages)
				target.EnsurePagesForActiveBits();
		}

		public override void DiffTo(Sets shadow, FrameDiff diff, int setIndex)
		{
			var other = shadow.Get<T>();

			other.GrowToFit(_dataSet);

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
		}

		public override void ApplyDiff(Sets target, FrameDiff diff, int setIndex)
		{
			var other = (DataSet<T>)target.Get<T>();
			var entries = diff.Entries;
			var count = diff.Count;

			other.GrowToFit(_dataSet);

			WorldDiffUtils.ApplyUlongArrayDiff(other.Bits, entries, count,
				DiffSection.SetBits, setIndex);
			WorldDiffUtils.ApplyUlongArrayDiff(other.NonEmptyBlocks, entries, count,
				DiffSection.SetNonEmpty, setIndex);
			WorldDiffUtils.ApplyUlongArrayDiff(other.SaturatedBlocks, entries, count,
				DiffSection.SetSaturated, setIndex);

			if (other.RetainPages)
				other.EnsurePagesForActiveBits();
		}
	}
}
