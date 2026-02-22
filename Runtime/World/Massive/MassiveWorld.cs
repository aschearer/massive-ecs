using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Massive
{
	/// <summary>
	/// Rollback extension for <see cref="World"/>.
	/// Uses byte-level XOR delta compression: stores a shadow world and
	/// per-frame diffs instead of full world copies.
	/// </summary>
	public class MassiveWorld : World, IMassive
	{
		private readonly World _shadow;
		private readonly List<FrameDiff> _diffs = new();
		private readonly WorldConfig _config;

		/// <summary>
		/// Index of the most recently saved frame, or -1 when no history exists.
		/// </summary>
		private int _head = -1;

		/// <summary>
		/// Entities.State at frame -1 (baseline before any diffs).
		/// Captured on first SaveFrame and on ForgetHistory.
		/// </summary>
		private Entities.State _baselineState;

		/// <summary>
		/// Whether the shadow world has been synced to the live world at least once
		/// (via ForgetHistory or the first SaveFrame). Without this, the shadow and
		/// baseline remain in their empty constructor state and a full rollback would
		/// restore an uninitialised world.
		/// </summary>
		private bool _shadowInitialized;

		public MassiveWorld()
			: this(new MassiveWorldConfig())
		{
		}

		public MassiveWorld(MassiveWorldConfig worldConfig)
			: base(worldConfig)
		{
			_config = worldConfig;
			_shadow = new World(worldConfig);
		}

		public event Action FrameSaved;
		public event Action<int> Rollbacked;

		public int CanRollbackFrames
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _head;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void ForgetHistory()
		{
			_head = -1;
			// Full copy live -> shadow to reset baseline
			this.CopyTo(_shadow);
			_baselineState = Entities.CurrentState;
			_shadowInitialized = true;
			// Keep _diffs list for reuse
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SaveFrame()
		{
			// First SaveFrame after construction: sync shadow to the live world.
			// The MassiveWorld constructor runs before any game entities are created,
			// so the shadow and baseline would otherwise represent an empty world.
			// ForgetHistory sets this flag when it explicitly syncs the shadow.
			if (!_shadowInitialized)
			{
				this.CopyTo(_shadow);
				_baselineState = Entities.CurrentState;
				_shadowInitialized = true;
			}

			_head++;

			if (_head >= _diffs.Count)
			{
				_diffs.Add(new FrameDiff());
			}

			var diff = _diffs[_head];
			diff.Clear();

			// Save Entities.State before diffing
			diff.EntitiesState = Entities.CurrentState;

			// Diff Entities arrays (live vs shadow)
			DiffEntities(diff);

			// Diff Components.BitMap (live vs shadow)
			DiffComponents(diff);

			// Diff all Sets via cloners
			Sets.DiffTo(_shadow.Sets, diff);

			// Allocator: full copy (no diff — not worth the complexity)
			Allocator.CopyTo(_shadow.Allocator);

			// Apply diff to shadow so shadow now matches live
			ApplyEntitiesDiff(diff, _shadow);
			ApplyComponentsDiff(diff, _shadow);
			Sets.ApplyDiff(_shadow.Sets, diff);

			// Sync shadow Entities.State
			_shadow.Entities.CurrentState = Entities.CurrentState;

			FrameSaved?.Invoke();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Rollback(int frames)
		{
			NegativeArgumentException.ThrowIfNegative(frames);

			if (frames > CanRollbackFrames)
			{
				throw new ArgumentOutOfRangeException(nameof(frames), frames,
					$"Can't rollback this far. CanRollbackFrames: {CanRollbackFrames}.");
			}

			// Restore live to last saved state (shadow always == live at last SaveFrame).
			// This is necessary because game logic (OnUpdate) can modify the live world
			// between SaveFrame and Rollback, and XOR diffs require the exact saved state.
			// Copy individual subsystems instead of World.CopyTo — Sets.CopyTo reorders
			// component bindings and clears entries the shadow doesn't know about, which
			// would null out LookupByComponentId for lazily-bound component types.
			_shadow.Entities.CopyTo(Entities);
			_shadow.Components.CopyTo(Components);
			_shadow.Sets.CopyDataTo(Sets);
			_shadow.Allocator.CopyTo(Allocator);

			// Apply diffs in reverse order to undo changes
			for (var i = _head; i > _head - frames; i--)
			{
				var diff = _diffs[i];

				// Apply XOR diff to live world (undoes changes)
				ApplyEntitiesDiff(diff, this);
				ApplyComponentsDiff(diff, this);
				Sets.ApplyDiff(Sets, diff);

				// Apply same diff to shadow (keep in sync)
				ApplyEntitiesDiff(diff, _shadow);
				ApplyComponentsDiff(diff, _shadow);
				Sets.ApplyDiff(_shadow.Sets, diff);

				// Restore Entities.State from previous frame
				var previousState = i > 0 ? _diffs[i - 1].EntitiesState : _baselineState;
				Entities.CurrentState = previousState;
				_shadow.Entities.CurrentState = previousState;
			}

			// Restore allocator from shadow
			_shadow.Allocator.CopyTo(Allocator);

			_head -= frames;

			Rollbacked?.Invoke(frames);
		}

		/// <summary>
		/// Returns the saved world from a previous frame without modifying the current state.<br/>
		/// A value of 0 returns the world from the last <see cref="IMassive.SaveFrame"/> call.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public World Peekback(int frames)
		{
			NegativeArgumentException.ThrowIfNegative(frames);

			if (frames > CanRollbackFrames)
			{
				throw new ArgumentOutOfRangeException(nameof(frames), frames,
					$"Can't peekback this far. CanRollbackFrames: {CanRollbackFrames}.");
			}

			// Peekback(0) means "last saved frame" which is the shadow
			if (frames == 0)
			{
				return _shadow;
			}

			// For frames > 0, we'd need to reconstruct — not supported in diff mode.
			// Create a temporary copy and apply diffs in reverse.
			var temp = new World(_config);
			_shadow.CopyTo(temp);

			for (var i = _head; i > _head - frames; i--)
			{
				var diff = _diffs[i];
				ApplyEntitiesDiff(diff, temp);
				ApplyComponentsDiff(diff, temp);
				Sets.ApplyDiff(temp.Sets, diff);

				var previousState = i > 0 ? _diffs[i - 1].EntitiesState : _baselineState;
				temp.Entities.CurrentState = previousState;
			}

			return temp;
		}

		private void DiffEntities(FrameDiff diff)
		{
			var live = Entities;
			var shadow = _shadow.Entities;

			// Grow shadow to match live
			shadow.GrowToFit(live);
			shadow.EnsureEntityAt(live.UsedIds > 0 ? live.UsedIds - 1 : 0);
			shadow.EnsurePoolAt(live.PooledIds > 0 ? live.PooledIds - 1 : 0);

			// Diff BitSet arrays
			var blocksLength = Math.Max(live.BlocksCapacity, shadow.BlocksCapacity);
			if (blocksLength > 0)
			{
				var bitsLength = Math.Max(live.Bits.Length, shadow.Bits.Length);
				WorldDiffUtils.DiffUlongArrays(live.Bits, shadow.Bits, bitsLength,
					DiffSection.EntitiesBits, 0, diff);
				WorldDiffUtils.DiffUlongArrays(live.NonEmptyBlocks, shadow.NonEmptyBlocks, blocksLength,
					DiffSection.EntitiesNonEmpty, 0, diff);
				WorldDiffUtils.DiffUlongArrays(live.SaturatedBlocks, shadow.SaturatedBlocks, blocksLength,
					DiffSection.EntitiesSaturated, 0, diff);
			}

			// Diff Pool array
			var poolLength = Math.Max(live.PooledIds, shadow.PooledIds);
			if (poolLength > 0)
			{
				WorldDiffUtils.DiffIntArrayAsUlongs(live.Pool, shadow.Pool, poolLength,
					DiffSection.EntitiesPool, 0, diff);
			}

			// Diff Versions array
			var versionsLength = Math.Max(live.UsedIds, shadow.UsedIds);
			if (versionsLength > 0)
			{
				WorldDiffUtils.DiffUintArrayAsUlongs(live.Versions, shadow.Versions, versionsLength,
					DiffSection.EntitiesVersions, 0, diff);
			}
		}

		private void DiffComponents(FrameDiff diff)
		{
			var live = Components;
			var shadow = _shadow.Components;

			// Grow shadow using data-preserving methods (NOT EnsureBitMapCapacity
			// which discards existing data).
			shadow.EnsureEntitiesCapacity(live.EntitiesCapacity);
			if (live.MaskLength > shadow.MaskLength)
			{
				shadow.EnsureComponentsCapacity(live.MaskLength << 6);
			}

			// Shadow is now >= live capacity. Diff only up to live's range
			// (shadow's extra region beyond live is guaranteed to be zero).
			var length = live.BitMapCapacity;
			if (length > 0)
			{
				WorldDiffUtils.DiffUlongArrays(live.BitMap, shadow.BitMap, length,
					DiffSection.ComponentsBitMap, 0, diff);
			}
		}

		private static void ApplyEntitiesDiff(FrameDiff diff, World target)
		{
			var entities = target.Entities;
			var entries = diff.Entries;
			var count = diff.Count;

			WorldDiffUtils.ApplyUlongArrayDiff(entities.Bits, entries, count,
				DiffSection.EntitiesBits, 0);
			WorldDiffUtils.ApplyUlongArrayDiff(entities.NonEmptyBlocks, entries, count,
				DiffSection.EntitiesNonEmpty, 0);
			WorldDiffUtils.ApplyUlongArrayDiff(entities.SaturatedBlocks, entries, count,
				DiffSection.EntitiesSaturated, 0);

			WorldDiffUtils.ApplyIntArrayDiff(entities.Pool, entries, count,
				DiffSection.EntitiesPool, 0);
			WorldDiffUtils.ApplyUintArrayDiff(entities.Versions, entries, count,
				DiffSection.EntitiesVersions, 0);
		}

		private static void ApplyComponentsDiff(FrameDiff diff, World target)
		{
			var components = target.Components;
			var entries = diff.Entries;
			var count = diff.Count;

			WorldDiffUtils.ApplyUlongArrayDiff(components.BitMap, entries, count,
				DiffSection.ComponentsBitMap, 0);
		}
	}
}
