namespace Massive
{
	public class NoCloning : SetCloner
	{
		public override void CopyTo(Sets sets)
		{
		}

		public override void DiffTo(Sets shadow, FrameDiff diff, int setIndex)
		{
		}

		public override void ApplyDiff(Sets target, FrameDiff diff, int setIndex)
		{
		}
	}
}
