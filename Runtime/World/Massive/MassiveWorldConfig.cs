using System;

namespace Massive
{
	public class MassiveWorldConfig : WorldConfig
	{
		public readonly int FramesCapacity = Constants.DefaultFramesCapacity;

		public MassiveWorldConfig(int? framesCapacity = default, bool? storeEmptyTypesAsDataSets = default, Func<Type, bool> isNonRollback = null)
			: base(storeEmptyTypesAsDataSets, isNonRollback)
		{
			FramesCapacity = framesCapacity ?? FramesCapacity;
		}
	}
}
