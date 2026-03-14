using System;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public partial class BitSetBase
    {
        protected ulong[] PageMasks { get; } = Constants.PageMasks;

        public ulong[] Bits { get; private set; } = Array.Empty<ulong>();
        public ulong[] NonEmptyBlocks { get; private set; } = Array.Empty<ulong>();
        public ulong[] SaturatedBlocks { get; private set; } = Array.Empty<ulong>();

        public int BlocksCapacity { get; private set; }

        /// <summary>
        /// Recalculates <see cref="NonEmptyBlocks"/> and <see cref="SaturatedBlocks"/>
        /// from <see cref="Bits"/>. Call after XOR-based forward diff application where
        /// the derived block arrays may be out of sync with the primary bit data.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public void RebuildDerivedBlocks()
        {
            for (var blockIndex = 0; blockIndex < BlocksCapacity; blockIndex++)
            {
                var nonEmpty = 0UL;
                var saturated = 0UL;
                var bitsStart = blockIndex << 6;

                for (var i = 0; i < 64; i++)
                {
                    var bits = Bits[bitsStart + i];
                    var mask = 1UL << i;
                    if (bits != 0UL)
                    {
                        nonEmpty |= mask;
                    }
                    if (bits == ulong.MaxValue)
                    {
                        saturated |= mask;
                    }
                }

                NonEmptyBlocks[blockIndex] = nonEmpty;
                SaturatedBlocks[blockIndex] = saturated;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void GrowToFit(BitSetBase other)
        {
            EnsureBlocksCapacity(other.BlocksCapacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureBlocksCapacity(int blocksCapacity)
        {
            if (blocksCapacity > BlocksCapacity)
            {
                BlocksCapacity = MathUtils.RoundUpToPowerOfTwo(blocksCapacity);
                NonEmptyBlocks = NonEmptyBlocks.Resize(BlocksCapacity);
                SaturatedBlocks = SaturatedBlocks.Resize(BlocksCapacity);
                Bits = Bits.Resize(BlocksCapacity << 6);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void EnsureBlocksCapacityAt(int blockIndex)
        {
            if (blockIndex >= BlocksCapacity)
            {
                BlocksCapacity = MathUtils.RoundUpToPowerOfTwo(blockIndex + 1);
                NonEmptyBlocks = NonEmptyBlocks.Resize(BlocksCapacity);
                SaturatedBlocks = SaturatedBlocks.Resize(BlocksCapacity);
                Bits = Bits.Resize(BlocksCapacity << 6);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase[] bitSet)
        {
            var minimal = bitSet[0];
            for (var i = 1; i < bitSet.Length; i++)
            {
                if (minimal.BlocksCapacity > bitSet[i].BlocksCapacity)
                {
                    minimal = bitSet[i];
                }
            }
            return minimal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase[] bitSets, int count)
        {
            var minimal = bitSets[0];
            for (var i = 1; i < count; i++)
            {
                if (minimal.BlocksCapacity > bitSets[i].BlocksCapacity)
                {
                    minimal = bitSets[i];
                }
            }
            return minimal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase first, BitSetBase[] bitSets)
        {
            var minimal = first;
            for (var i = 0; i < bitSets.Length; i++)
            {
                if (minimal.BlocksCapacity > bitSets[i].BlocksCapacity)
                {
                    minimal = bitSets[i];
                }
            }
            return minimal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase first, BitSet[] bitSets, int count)
        {
            var minimal = first;
            for (var i = 0; i < count; i++)
            {
                if (minimal.BlocksCapacity > bitSets[i].BlocksCapacity)
                {
                    minimal = bitSets[i];
                }
            }
            return minimal;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase bitSet1, BitSetBase bitSet2)
        {
            if (bitSet1.BlocksCapacity <= bitSet2.BlocksCapacity)
            {
                return bitSet1;
            }
            return bitSet2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase bitSet1, BitSetBase bitSet2, BitSetBase bitSet3)
        {
            if (bitSet1.BlocksCapacity <= bitSet2.BlocksCapacity && bitSet1.BlocksCapacity <= bitSet3.BlocksCapacity)
            {
                return bitSet1;
            }
            if (bitSet2.BlocksCapacity <= bitSet1.BlocksCapacity && bitSet2.BlocksCapacity <= bitSet3.BlocksCapacity)
            {
                return bitSet2;
            }
            return bitSet3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BitSetBase GetMinBitSet(BitSetBase bitSet1, BitSetBase bitSet2, BitSetBase bitSet3, BitSetBase bitSet4)
        {
            if (bitSet1.BlocksCapacity <= bitSet2.BlocksCapacity && bitSet1.BlocksCapacity <= bitSet3.BlocksCapacity && bitSet1.BlocksCapacity <= bitSet4.BlocksCapacity)
            {
                return bitSet1;
            }
            if (bitSet2.BlocksCapacity <= bitSet1.BlocksCapacity && bitSet2.BlocksCapacity <= bitSet3.BlocksCapacity && bitSet2.BlocksCapacity <= bitSet4.BlocksCapacity)
            {
                return bitSet2;
            }
            if (bitSet3.BlocksCapacity <= bitSet1.BlocksCapacity && bitSet3.BlocksCapacity <= bitSet2.BlocksCapacity && bitSet3.BlocksCapacity <= bitSet4.BlocksCapacity)
            {
                return bitSet3;
            }
            return bitSet4;
        }
    }
}