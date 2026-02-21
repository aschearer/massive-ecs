using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Massive
{
	/// <summary>
	/// Rollback extension for <see cref="World"/>.
	/// Uses a growable frame buffer so the entire history since the last
	/// <see cref="ForgetHistory"/> call is available for rollback.
	/// </summary>
	public class MassiveWorld : World, IMassive
	{
		private readonly List<World> _frames = new();
		private readonly WorldConfig _config;

		/// <summary>
		/// Index of the most recently saved frame, or -1 when no history exists.
		/// </summary>
		private int _head = -1;

		public MassiveWorld()
			: this(new MassiveWorldConfig())
		{
		}

		public MassiveWorld(MassiveWorldConfig worldConfig)
			: base(worldConfig)
		{
			_config = worldConfig;
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
			// Keep _frames list intact — slots are reused on the next SaveFrame calls.
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void SaveFrame()
		{
			_head++;

			if (_head >= _frames.Count)
			{
				_frames.Add(new World(_config));
			}

			this.CopyTo(_frames[_head]);

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

			_head -= frames;

			_frames[_head].CopyTo(this);

			Rollbacked?.Invoke(frames);
		}

		/// <summary>
		/// Returns the saved world from a previous frame without modifying the current state.<br/>
		/// A value of 0 returns the world from the last <see cref="IMassive.SaveFrame"/> call.
		/// </summary>
		/// <param name="frames">
		/// The number of frames to peek back. Must be non-negative and not exceed <see cref="IMassive.CanRollbackFrames"/>.
		/// </param>
		/// <returns>
		/// The saved world from the specified frames ago.
		/// </returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public World Peekback(int frames)
		{
			NegativeArgumentException.ThrowIfNegative(frames);

			if (frames > CanRollbackFrames)
			{
				throw new ArgumentOutOfRangeException(nameof(frames), frames,
					$"Can't peekback this far. CanRollbackFrames: {CanRollbackFrames}.");
			}

			return _frames[_head - frames];
		}
	}
}
