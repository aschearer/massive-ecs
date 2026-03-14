using System;

namespace Massive
{
    public class Empty : IAndSelector, IOrSelector
    {
        public BitSet[] Select(Sets sets)
        {
            return Array.Empty<BitSet>();
        }
    }
}