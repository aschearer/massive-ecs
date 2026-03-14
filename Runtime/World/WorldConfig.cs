using System;
using System.Runtime.CompilerServices;

namespace Massive
{
    public class WorldConfig
    {
        public readonly bool StoreEmptyTypesAsDataSets = false;

        /// <summary>
        /// Optional predicate that identifies component types whose data should not
        /// be copied during <see cref="MassiveWorld.SaveFrame"/>/<see cref="MassiveWorld.Rollback"/>.
        /// Entity membership (BitSet) is still copied; only the data values are preserved.
        /// </summary>
        public readonly Func<Type, bool> IsNonRollback;

        public WorldConfig(bool? storeEmptyTypesAsDataSets = default, Func<Type, bool> isNonRollback = null)
        {
            StoreEmptyTypesAsDataSets = storeEmptyTypesAsDataSets ?? StoreEmptyTypesAsDataSets;
            IsNonRollback = isNonRollback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompatibleWith(WorldConfig other)
        {
            return StoreEmptyTypesAsDataSets == other.StoreEmptyTypesAsDataSets;
        }
    }
}