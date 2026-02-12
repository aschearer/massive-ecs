using Unity.IL2CPP.CompilerServices;

namespace Massive
{
	/// <summary>
	/// Cloner that copies only the BitSet membership, not the data.
	/// Used for <see cref="INonRollback"/> components so their values
	/// survive SaveFrame/Rollback while entity membership stays consistent.
	/// </summary>
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class BitSetOnlyCloner<T> : SetCloner
	{
		private readonly DataSet<T> _dataSet;

		public BitSetOnlyCloner(DataSet<T> dataSet)
		{
			_dataSet = dataSet;
		}

		public override void CopyTo(Sets sets)
		{
			_dataSet.CopyBitSetTo(sets.Get<T>());
		}
	}
}
