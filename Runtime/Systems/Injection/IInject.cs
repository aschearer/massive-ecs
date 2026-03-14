namespace Massive
{
    public interface IInject<TArg>
    {
        public void Inject(TArg arg);
    }
}