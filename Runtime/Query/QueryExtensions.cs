using Unity.IL2CPP.CompilerServices;

namespace Massive
{
    [Il2CppEagerStaticClassConstruction]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public static class QueryExtensions
    {
        public static Query Include(this Query query, BitSet[] all)
        {
            query.Filter.SetIncluded(all);
            return query;
        }

        public static Query Include<T>(this Query query)
        {
            return query.Include(query.World.SelectSets<T>());
        }

        public static Query Include<T1, T2>(this Query query)
        {
            return query.Include(query.World.SelectSets<T1, T2>());
        }

        public static Query Include<T1, T2, T3>(this Query query)
        {
            return query.Include(query.World.SelectSets<T1, T2, T3>());
        }

        public static Query Include<T1, T2, T3, T4>(this Query query)
        {
            return query.Include(query.World.SelectSets<T1, T2, T3, T4>());
        }

        public static Query Include<T1, T2, T3, T4, T5>(this Query query)
        {
            return query.Include(query.World.SelectSets<T1, T2, T3, T4, T5>());
        }

        public static Query Include<T1, T2, T3, T4, T5, T6>(this Query query)
        {
            return query.Include(query.World.SelectSets<T1, T2, T3, T4, T5, T6>());
        }

        public static Query Include<T1, T2, T3, T4, T5, T6, TAnd>(this Query query)
            where TAnd : IAndSelector, new()
        {
            return query.Include(query.World.SelectSets<T1, T2, T3, T4, T5, T6, TAnd>());
        }

        public static Query Exclude(this Query query, BitSet[] none)
        {
            query.Filter.SetExcluded(none);
            return query;
        }

        public static Query Exclude<T>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T>());
        }

        public static Query Exclude<T1, T2>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T1, T2>());
        }

        public static Query Exclude<T1, T2, T3>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T1, T2, T3>());
        }

        public static Query Exclude<T1, T2, T3, T4>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T1, T2, T3, T4>());
        }

        public static Query Exclude<T1, T2, T3, T4, T5>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T1, T2, T3, T4, T5>());
        }

        public static Query Exclude<T1, T2, T3, T4, T5, T6>(this Query query)
        {
            return query.Exclude(query.World.SelectSets<T1, T2, T3, T4, T5, T6>());
        }

        public static Query Exclude<T1, T2, T3, T4, T5, T6, TAnd>(this Query query)
            where TAnd : IAndSelector, new()
        {
            return query.Exclude(query.World.SelectSets<T1, T2, T3, T4, T5, T6, TAnd>());
        }
    }
}