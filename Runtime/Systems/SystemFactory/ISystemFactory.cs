namespace Massive
{
    public interface ISystemFactory
    {
        public ISystem Create();

        public int Order => 0;
    }
}