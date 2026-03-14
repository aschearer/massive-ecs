namespace Massive
{
    public abstract class SetCloner
    {
        public abstract void CopyTo(Sets sets);

        public abstract void DiffTo(Sets shadow, FrameDiff diff, int setIndex);

        public abstract void ApplyDiff(Sets target, FrameDiff diff, int setIndex);
    }
}