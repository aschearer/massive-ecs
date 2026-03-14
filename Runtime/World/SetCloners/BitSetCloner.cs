using System;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class BitSetCloner<T> : SetCloner
    {
        private readonly BitSet _bitSet;

        public BitSetCloner(BitSet bitSet)
        {
            _bitSet = bitSet;
        }

        public override void CopyTo(Sets sets)
        {
            _bitSet.CopyBitSetTo(sets.Get<T>());
        }

        public override void DiffTo(Sets shadow, FrameDiff diff, int setIndex)
        {
            var other = shadow.Get<T>();

            other.GrowToFit(_bitSet);

            var blocksLength = Math.Max(_bitSet.BlocksCapacity, other.BlocksCapacity);
            if (blocksLength > 0)
            {
                var bitsLength = Math.Max(_bitSet.Bits.Length, other.Bits.Length);
                WorldDiffUtils.DiffUlongArrays(_bitSet.Bits, other.Bits, bitsLength,
                    DiffSection.SetBits, setIndex, diff);
                WorldDiffUtils.DiffUlongArrays(_bitSet.NonEmptyBlocks, other.NonEmptyBlocks, blocksLength,
                    DiffSection.SetNonEmpty, setIndex, diff);
                WorldDiffUtils.DiffUlongArrays(_bitSet.SaturatedBlocks, other.SaturatedBlocks, blocksLength,
                    DiffSection.SetSaturated, setIndex, diff);
            }
        }

        public override void ApplyDiff(Sets target, FrameDiff diff, int setIndex)
        {
            var other = target.Get<T>();
            var entries = diff.Entries;
            var count = diff.Count;

            other.GrowToFit(_bitSet);

            WorldDiffUtils.ApplyUlongArrayDiff(other.Bits, entries, count,
                DiffSection.SetBits, setIndex);
            WorldDiffUtils.ApplyUlongArrayDiff(other.NonEmptyBlocks, entries, count,
                DiffSection.SetNonEmpty, setIndex);
            WorldDiffUtils.ApplyUlongArrayDiff(other.SaturatedBlocks, entries, count,
                DiffSection.SetSaturated, setIndex);
        }
    }
}