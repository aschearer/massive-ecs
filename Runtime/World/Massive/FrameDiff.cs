using System;
using System.Runtime.CompilerServices;

namespace Massive
{
    public enum DiffSection : byte
    {
        EntitiesBits,
        EntitiesNonEmpty,
        EntitiesSaturated,
        EntitiesPool,
        EntitiesVersions,
        ComponentsBitMap,
        SetBits,
        SetNonEmpty,
        SetSaturated,
        DataPage,
    }

    public struct DiffEntry
    {
        public DiffSection Section;
        public ushort SetIndex;
        public ushort PageIndex;
        public int WordOffset;
        public ulong XorValue;
    }

    public class FrameDiff
    {
        private DiffEntry[] _entries = new DiffEntry[64];
        public Entities.State EntitiesState;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get; private set;
        }

        public DiffEntry[] Entries
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _entries;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Count == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(DiffSection section, int setIndex, int pageIndex, int wordOffset, ulong xorValue)
        {
            if (Count >= _entries.Length)
            {
                Array.Resize(ref _entries, _entries.Length * 2);
            }

            ref var entry = ref _entries[Count++];
            entry.Section = section;
            entry.SetIndex = (ushort)setIndex;
            entry.PageIndex = (ushort)pageIndex;
            entry.WordOffset = wordOffset;
            entry.XorValue = xorValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            Count = 0;
        }
    }
}