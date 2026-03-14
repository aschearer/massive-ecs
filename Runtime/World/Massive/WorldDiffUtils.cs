using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Massive
{
    public static class WorldDiffUtils
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DiffUlongArrays(ulong[] a, ulong[] b, int length,
            DiffSection section, int setIndex, FrameDiff diff)
        {
            for (var i = 0; i < length; i++)
            {
                var xor = a[i] ^ b[i];
                if (xor != 0UL)
                {
                    diff.Add(section, setIndex, 0, i, xor);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyUlongArrayDiff(ulong[] target, DiffEntry[] entries, int count,
            DiffSection section, int setIndex)
        {
            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Section == section && entry.SetIndex == setIndex)
                {
                    target[entry.WordOffset] ^= entry.XorValue;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DiffIntArrayAsUlongs(int[] a, int[] b, int length,
            DiffSection section, int setIndex, FrameDiff diff)
        {
            var ulongCount = length / 2;
            var spanA = MemoryMarshal.Cast<int, ulong>(a.AsSpan(0, ulongCount * 2));
            var spanB = MemoryMarshal.Cast<int, ulong>(b.AsSpan(0, ulongCount * 2));

            for (var i = 0; i < ulongCount; i++)
            {
                var xor = spanA[i] ^ spanB[i];
                if (xor != 0UL)
                {
                    diff.Add(section, setIndex, 0, i, xor);
                }
            }

            // Handle odd tail element
            if ((length & 1) != 0)
            {
                var lastIndex = length - 1;
                if (a[lastIndex] != b[lastIndex])
                {
                    var xor = (uint)a[lastIndex] ^ (ulong)(uint)b[lastIndex];
                    diff.Add(section, setIndex, 0, ulongCount, xor);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyIntArrayDiff(int[] target, DiffEntry[] entries, int count,
            DiffSection section, int setIndex)
        {
            var span = MemoryMarshal.Cast<int, ulong>(target);
            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Section == section && entry.SetIndex == setIndex)
                {
                    if (entry.WordOffset < span.Length)
                    {
                        span[entry.WordOffset] ^= entry.XorValue;
                    }
                    else
                    {
                        // Odd tail element
                        var idx = entry.WordOffset * 2;
                        target[idx] ^= (int)(uint)entry.XorValue;
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DiffUintArrayAsUlongs(uint[] a, uint[] b, int length,
            DiffSection section, int setIndex, FrameDiff diff)
        {
            var ulongCount = length / 2;
            var spanA = MemoryMarshal.Cast<uint, ulong>(a.AsSpan(0, ulongCount * 2));
            var spanB = MemoryMarshal.Cast<uint, ulong>(b.AsSpan(0, ulongCount * 2));

            for (var i = 0; i < ulongCount; i++)
            {
                var xor = spanA[i] ^ spanB[i];
                if (xor != 0UL)
                {
                    diff.Add(section, setIndex, 0, i, xor);
                }
            }

            if ((length & 1) != 0)
            {
                var lastIndex = length - 1;
                if (a[lastIndex] != b[lastIndex])
                {
                    var xor = a[lastIndex] ^ (ulong)b[lastIndex];
                    diff.Add(section, setIndex, 0, ulongCount, xor);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApplyUintArrayDiff(uint[] target, DiffEntry[] entries, int count,
            DiffSection section, int setIndex)
        {
            var span = MemoryMarshal.Cast<uint, ulong>(target);
            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Section == section && entry.SetIndex == setIndex)
                {
                    if (entry.WordOffset < span.Length)
                    {
                        span[entry.WordOffset] ^= entry.XorValue;
                    }
                    else
                    {
                        var idx = entry.WordOffset * 2;
                        target[idx] ^= (uint)entry.XorValue;
                    }
                }
            }
        }

        /// <summary>
        /// Diffs a page of struct data by reinterpreting as ulong words.
        /// Uses MemoryMarshal.Cast for correct managed memory layout.
        /// </summary>
        public static void DiffPage<T>(T[] current, T[] shadow,
            DiffSection section, int setIndex, int pageIndex, FrameDiff diff) where T : struct
        {
            var currentWords = MemoryMarshal.Cast<T, ulong>(current.AsSpan());
            var shadowWords = MemoryMarshal.Cast<T, ulong>(shadow.AsSpan());
            var wordCount = currentWords.Length;

            for (var w = 0; w < wordCount; w++)
            {
                var xor = currentWords[w] ^ shadowWords[w];
                if (xor != 0UL)
                {
                    diff.Add(section, setIndex, pageIndex, w, xor);
                }
            }

            // Handle remaining bytes (when sizeof(T) * Length is not a multiple of 8)
            var currentBytes = MemoryMarshal.AsBytes(current.AsSpan());
            var totalBytes = currentBytes.Length;
            var coveredBytes = wordCount * 8;
            if (coveredBytes < totalBytes)
            {
                var shadowBytes = MemoryMarshal.AsBytes(shadow.AsSpan());
                ulong aVal = 0;
                ulong bVal = 0;
                for (var r = 0; r < totalBytes - coveredBytes; r++)
                {
                    aVal |= (ulong)currentBytes[coveredBytes + r] << (r * 8);
                    bVal |= (ulong)shadowBytes[coveredBytes + r] << (r * 8);
                }
                var xor = aVal ^ bVal;
                if (xor != 0UL)
                {
                    diff.Add(section, setIndex, pageIndex, wordCount, xor);
                }
            }
        }

        /// <summary>
        /// Applies XOR diff entries to a page of struct data.
        /// </summary>
        public static void ApplyPageDiff<T>(T[] page,
            DiffEntry[] entries, int count, int setIndex, int pageIndex) where T : struct
        {
            var words = MemoryMarshal.Cast<T, ulong>(page.AsSpan());
            var wordCount = words.Length;
            var totalBytes = MemoryMarshal.AsBytes(page.AsSpan()).Length;

            for (var i = 0; i < count; i++)
            {
                ref var entry = ref entries[i];
                if (entry.Section != DiffSection.DataPage ||
                    entry.SetIndex != setIndex ||
                    entry.PageIndex != pageIndex)
                {
                    continue;
                }

                if (entry.WordOffset < wordCount)
                {
                    words[entry.WordOffset] ^= entry.XorValue;
                }
                else
                {
                    // Remainder bytes
                    var bytes = MemoryMarshal.AsBytes(page.AsSpan());
                    var offset = entry.WordOffset * 8;
                    var remainder = totalBytes - offset;
                    var xor = entry.XorValue;
                    for (var r = 0; r < remainder; r++)
                    {
                        bytes[offset + r] ^= (byte)(xor >> (r * 8));
                    }
                }
            }
        }
    }
}