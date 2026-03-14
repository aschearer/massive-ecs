using System;
using System.Runtime.CompilerServices;

namespace Massive
{
    public class SetFactory
    {
        private readonly Allocator _allocator;
        private readonly bool _storeEmptyTypesAsDataSets;
        private readonly Func<Type, bool> _isNonRollback;

        public SetFactory(Allocator allocator, WorldConfig worldConfig)
            : this(allocator, worldConfig.StoreEmptyTypesAsDataSets, worldConfig.IsNonRollback)
        {
        }

        public SetFactory(Allocator allocator, bool storeEmptyTypesAsDataSets = false, Func<Type, bool> isNonRollback = null)
        {
            _allocator = allocator;
            _storeEmptyTypesAsDataSets = storeEmptyTypesAsDataSets;
            _isNonRollback = isNonRollback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CompatibleWith(SetFactory other)
        {
            return _storeEmptyTypesAsDataSets == other._storeEmptyTypesAsDataSets;
        }

        public Output CreateAppropriateSet<T>()
        {
            var type = typeof(T);

            if (TypeHasNoData(type))
            {
                return CreateBitSet<T>();
            }

            return CreateDataSet<T>();
        }

        private Output CreateBitSet<T>()
        {
            var bitSet = new BitSet();
            var cloner = new BitSetCloner<T>(bitSet);
            return new Output(bitSet, cloner);
        }

        private Output CreateDataSet<T>()
        {
            var type = typeof(T);
            if (_isNonRollback != null && _isNonRollback(type))
            {
                var dataSet = new DataSet<T>(Default<T>.Value) { RetainPages = true };
                var cloner = new BitSetOnlyCloner<T>(dataSet);
                return new Output(dataSet, cloner);
            }
            else if (CopyableUtils.IsImplementedFor(type))
            {
                var dataSet = CopyableUtils.CreateCopyingDataSet(Default<T>.Value);
                var cloner = CopyableUtils.CreateCopyingDataSetCloner(dataSet);
                return new Output(dataSet, cloner);
            }
            else if (AutoFreeUtils.IsImplementedFor(type) && AllocatorSchemaGenerator.HasPointers(type))
            {
                var dataSet = AutoFreeUtils.CreateAutoFreeDataSet(_allocator, Default<T>.Value);
                var cloner = new DataSetCloner<T>(dataSet);
                return new Output(dataSet, cloner);
            }
            else
            {
                var dataSet = new DataSet<T>(Default<T>.Value);
                var cloner = new DataSetCloner<T>(dataSet);
                return new Output(dataSet, cloner);
            }
        }

        public bool TypeHasData(Type type)
        {
            return !TypeHasNoData(type);
        }

        public bool TypeHasNoData(Type type)
        {
            return type.IsValueType && ReflectionUtils.HasNoFields(type) && !_storeEmptyTypesAsDataSets;
        }

        public readonly struct Output
        {
            public readonly BitSet Set;
            public readonly SetCloner Cloner;

            public Output(BitSet set, SetCloner cloner)
            {
                Set = set;
                Cloner = cloner;
            }

            public void Deconstruct(out BitSet set, out SetCloner cloner)
            {
                set = Set;
                cloner = Cloner;
            }
        }
    }
}