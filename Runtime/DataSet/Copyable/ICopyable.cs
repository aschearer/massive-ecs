namespace Massive
{
    public interface ICopyable<T> where T : ICopyable<T>
    {
        public void CopyTo(ref T other);
    }
}