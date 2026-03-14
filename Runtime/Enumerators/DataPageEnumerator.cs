using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace Massive
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public readonly struct DataPageEnumerable
    {
        private readonly BitSetBase _bitSet;

        public DataPageEnumerable(BitSetBase bitSet)
        {
            _bitSet = bitSet;
        }

        public readonly Enumerator GetEnumerator()
        {
            return new Enumerator(_bitSet);
        }

        public struct Enumerator
        {
            private readonly ulong[] _blocks;
            private readonly int _blocksLength;
            private int _blockIndex;

            private readonly byte[] _deBruijn;
            private readonly ulong[] _pageMasksNegative;

            private ulong _block;
            private int _pageOffset;

            public Enumerator(BitSetBase _bitSet)
            {
                _blocks = _bitSet.NonEmptyBlocks;
                _blocksLength = _blocks.Length;

                _deBruijn = MathUtils.DeBruijn;
                _pageMasksNegative = Constants.PageMasksNegative;

                _blockIndex = -1;
                _pageOffset = default;
                _block = default;
                Current = default;

                while (++_blockIndex < _blocksLength)
                {
                    if (_blocks[_blockIndex] != 0UL)
                    {
                        _block = _blocks[_blockIndex];
                        _pageOffset = _blockIndex << Constants.PagesInBlockPower;
                        return;
                    }
                }
            }

            public int Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get; private set;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                if (_block != 0UL)
                {
                    var blockBit = _deBruijn[(int)(((_block & (ulong)-(long)_block) * 0x37E84A99DAE458FUL) >> 58)];
                    var pageIndexMod = blockBit >> Constants.PageMaskShift;
                    Current = _pageOffset + pageIndexMod;
                    _block &= _pageMasksNegative[pageIndexMod];
                    return true;
                }

                while (++_blockIndex < _blocksLength)
                {
                    if (_blocks[_blockIndex] != 0UL)
                    {
                        _block = _blocks[_blockIndex];
                        _pageOffset = _blockIndex << Constants.PagesInBlockPower;

                        var blockBit = _deBruijn[(int)(((_block & (ulong)-(long)_block) * 0x37E84A99DAE458FUL) >> 58)];
                        var pageIndexMod = blockBit >> Constants.PageMaskShift;
                        Current = _pageOffset + pageIndexMod;
                        _block &= _pageMasksNegative[pageIndexMod];
                        return true;
                    }
                }

                return false;
            }
        }
    }
}