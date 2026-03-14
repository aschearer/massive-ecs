using System;

namespace Massive
{
    public interface IDataSet
    {
        public BitSet BitSet { get; }

        public Type ElementType { get; }

        public Type ArrayType { get; }

        public Array GetPage(int page);

        public void EnsurePage(int page);

        public object GetRaw(int id);

        public void SetRaw(int id, object value);

        public DataPageEnumerable GetDataPages();

        public int ElementSize { get; }

        public bool IsUnmanaged { get; }
    }
}