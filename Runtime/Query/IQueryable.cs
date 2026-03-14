namespace Massive
{
    public interface IQueryable
    {
        public Query Query { get; }

        public World World => Query.World;

        public Filter Filter => Query.Filter;

        public EntityEnumerable Entities { get; }

        public IdsEnumerator GetEnumerator();
    }
}