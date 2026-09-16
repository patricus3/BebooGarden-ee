using System;
using System.Numerics;

namespace BebooGarden.Audio;

/// <summary>
/// A thin layer over FMOD's own C# bindings, in the shape the game already speaks.
///
/// The game used to talk to FmodAudio, a third-party binding whose newest release anywhere still
/// targets FMOD 2.02. Moving to 2.03 meant leaving it. The obvious way to do that would have been
/// to rewrite all 220 call sites against FMOD's raw bindings - every call returns a RESULT and
/// hands its answer back through an out parameter - but the sound vocabulary runs right through
/// the game logic: nineteen files under GameCore name Sound or Channel, and they care about
/// "is it playing" and "put it here", not about error codes.
///
/// So this keeps that vocabulary and does the translating in one place. Sound, Channel and
/// FmodSystem behave as they did: properties rather than getters, System.Numerics vectors rather
/// than FMOD.VECTOR, and a thrown <see cref="FmodException"/> rather than a RESULT nobody checks.
/// GameCore did not change at all.
///
/// Errors throw on purpose. A channel whose sound has finished is no longer a channel and FMOD
/// answers ERR_INVALID_HANDLE for anything you ask it; the game already catches FmodException in
/// those places, and that behaviour is preserved exactly.
/// </summary>
public class FmodException : Exception
{
  public FMOD.RESULT Result { get; }

  public FmodException(FMOD.RESULT result)
      : base($"FMOD error {result}: {FMOD.Error.String(result)}")
      => Result = result;
}

internal static class Check
{
  /// <summary>Throws unless FMOD was happy. The one place a RESULT is looked at.</summary>
  internal static void Ok(FMOD.RESULT result)
  {
    if (result != FMOD.RESULT.OK) throw new FmodException(result);
  }

  internal static FMOD.VECTOR Vec(Vector3 v) => new() { x = v.X, y = v.Y, z = v.Z };
}

/// <summary>How a sound is loaded and played. Maps onto FMOD's MODE.</summary>
[Flags]
public enum Mode : uint
{
  Default = FMOD.MODE.DEFAULT,
  Loop_Normal = FMOD.MODE.LOOP_NORMAL,
  Loop_Off = FMOD.MODE.LOOP_OFF,
  _2D = FMOD.MODE._2D,
  _3D = FMOD.MODE._3D,
  _3D_LinearSquareRolloff = FMOD.MODE._3D_LINEARSQUAREROLLOFF,
  _3D_InverseTaperedRolloff = FMOD.MODE._3D_INVERSETAPEREDROLLOFF,
  Unique = FMOD.MODE.UNIQUE,
  CreateStream = FMOD.MODE.CREATESTREAM,
}

[Flags]
public enum InitFlags : uint
{
  Normal = FMOD.INITFLAGS.NORMAL,
  _3D_RightHanded = FMOD.INITFLAGS._3D_RIGHTHANDED,
  Vol0_Becomes_Virtual = FMOD.INITFLAGS.VOL0_BECOMES_VIRTUAL,
}

public enum TimeUnit : uint
{
  MS = FMOD.TIMEUNIT.MS,
  PCM = FMOD.TIMEUNIT.PCM,
}

/// <summary>Knobs FMOD only exposes through a struct. Only what the game sets.</summary>
public sealed class AdvancedSettings
{
  public float Vol0VirtualVol { get; set; }
}

/// <summary>A loaded sound. Cheap to copy: it is a handle, not the audio.</summary>
public readonly struct Sound
{
  internal readonly FMOD.Sound Handle;
  internal Sound(FMOD.Sound handle) => Handle = handle;

  public bool HasHandle => Handle.hasHandle();

  /// <summary>Where this sound starts falling off with distance, and where it stops being heard.</summary>
  public void Set3DMinMaxDistance(float min, float max)
      => Check.Ok(Handle.set3DMinMaxDistance(min, max));

  public void SetLoopPoints(TimeUnit startUnit, uint start, TimeUnit endUnit, uint end)
      => Check.Ok(Handle.setLoopPoints(start, (FMOD.TIMEUNIT)startUnit, end, (FMOD.TIMEUNIT)endUnit));

  public uint GetLength(TimeUnit unit = TimeUnit.MS)
  {
    Check.Ok(Handle.getLength(out uint length, (FMOD.TIMEUNIT)unit));
    return length;
  }

  public void Release() => Check.Ok(Handle.release());
}

/// <summary>What FmodAudio called a raw sound handle. The same thing here.</summary>
public readonly struct SoundHandle
{
  internal readonly FMOD.Sound Handle;
  internal SoundHandle(FMOD.Sound handle) => Handle = handle;

  public static implicit operator Sound(SoundHandle h) => new(h.Handle);
  public static implicit operator SoundHandle(Sound s) => new(s.Handle);
}

/// <summary>
/// One playing instance of a sound. Every member throws once the sound has finished, because at
/// that point FMOD has taken the channel back; callers that expect that catch FmodException.
/// </summary>
public sealed class Channel
{
  internal readonly FMOD.Channel Handle;
  internal Channel(FMOD.Channel handle) => Handle = handle;

  public bool IsPlaying
  {
    get
    {
      // The one place a bad handle is an answer rather than a fault: "has it finished" is a
      // question the game asks constantly, and "yes, and the channel is gone" is a normal reply.
      FMOD.RESULT result = Handle.isPlaying(out bool playing);
      if (result is FMOD.RESULT.ERR_INVALID_HANDLE or FMOD.RESULT.ERR_CHANNEL_STOLEN) return false;
      Check.Ok(result);
      return playing;
    }
  }

  public bool Paused
  {
    get { Check.Ok(Handle.getPaused(out bool paused)); return paused; }
    set => Check.Ok(Handle.setPaused(value));
  }

  public float Volume
  {
    get { Check.Ok(Handle.getVolume(out float volume)); return volume; }
    set => Check.Ok(Handle.setVolume(value));
  }

  public bool Mute
  {
    get { Check.Ok(Handle.getMute(out bool mute)); return mute; }
    set => Check.Ok(Handle.setMute(value));
  }

  /// <summary>Playback speed, and with it pitch. The beboo voices ride on this.</summary>
  public float Pitch
  {
    get { Check.Ok(Handle.getPitch(out float pitch)); return pitch; }
    set => Check.Ok(Handle.setPitch(value));
  }

  public void Set3DAttributes(Vector3 position, Vector3 velocity, Vector3 alternatePanPosition = default)
  {
    FMOD.VECTOR pos = Check.Vec(position);
    FMOD.VECTOR vel = Check.Vec(velocity);
    Check.Ok(Handle.set3DAttributes(ref pos, ref vel));
  }

  public void Set3DMinMaxDistance(float min, float max)
      => Check.Ok(Handle.set3DMinMaxDistance(min, max));

  public void Set3DConeSettings(float insideAngle, float outsideAngle, float outsideVolume)
      => Check.Ok(Handle.set3DConeSettings(insideAngle, outsideAngle, outsideVolume));

  public void Set3DConeOrientation(Vector3 orientation)
  {
    FMOD.VECTOR o = Check.Vec(orientation);
    Check.Ok(Handle.set3DConeOrientation(ref o));
  }

  public void SetLoopPoints(TimeUnit startUnit, uint start, TimeUnit endUnit, uint end)
      => Check.Ok(Handle.setLoopPoints(start, (FMOD.TIMEUNIT)startUnit, end, (FMOD.TIMEUNIT)endUnit));

  public void Stop() => Check.Ok(Handle.stop());
}

/// <summary>A group of channels. The game only ever touches the master group's volume.</summary>
public readonly struct SoundGroup
{
  internal readonly FMOD.ChannelGroup Handle;
  internal SoundGroup(FMOD.ChannelGroup handle) => Handle = handle;

  /// <summary>
  /// Silences or resumes every channel in the group at once. On the master group that is the whole
  /// game, which is what stopping for a locked screen actually needs: FMOD mixes on its own thread,
  /// so a game that has stopped ticking still plays.
  /// </summary>
  public bool Paused
  {
    get
    {
      if (!Handle.hasHandle()) return false;
      Check.Ok(Handle.getPaused(out bool paused));
      return paused;
    }
    set
    {
      if (!Handle.hasHandle()) return;
      Check.Ok(Handle.setPaused(value));
    }
  }

  public float Volume
  {
    get
    {
      if (!Handle.hasHandle()) return 1f;
      Check.Ok(Handle.getVolume(out float volume));
      return volume;
    }
    set
    {
      if (!Handle.hasHandle()) return;
      Check.Ok(Handle.setVolume(value));
    }
  }
}

/// <summary>The FMOD system. Created by <see cref="Fmod.CreateSystem"/>.</summary>
public sealed class FmodSystem
{
  internal FMOD.System Handle;
  internal FmodSystem(FMOD.System handle) => Handle = handle;

  public void Init(int maxChannels, InitFlags flags)
      => Check.Ok(Handle.init(maxChannels, (FMOD.INITFLAGS)flags, IntPtr.Zero));

  public void SetAdvancedSettings(AdvancedSettings settings)
  {
    var native = new FMOD.ADVANCEDSETTINGS { vol0virtualvol = settings.Vol0VirtualVol };
    Check.Ok(Handle.setAdvancedSettings(ref native));
  }

  public void Set3DSettings(float dopplerScale, float distanceFactor, float rolloffScale)
      => Check.Ok(Handle.set3DSettings(dopplerScale, distanceFactor, rolloffScale));

  // 'in' to match how the game already calls this - Set3DListenerAttributes(0, in Forward, ...).
  public void Set3DListenerAttributes(
      int listener, in Vector3 position, in Vector3 velocity, in Vector3 forward, in Vector3 up)
  {
    FMOD.VECTOR pos = Check.Vec(position);
    FMOD.VECTOR vel = Check.Vec(velocity);
    FMOD.VECTOR fwd = Check.Vec(forward);
    FMOD.VECTOR upv = Check.Vec(up);
    Check.Ok(Handle.set3DListenerAttributes(listener, ref pos, ref vel, ref fwd, ref upv));
  }

  /// <summary>Where the listener is now. The whistle uses this to know where the player stands.</summary>
  public void Get3DListenerAttributes(
      int listener, out Vector3 position, out Vector3 velocity, out Vector3 forward, out Vector3 up)
  {
    Check.Ok(Handle.get3DListenerAttributes(
        listener, out FMOD.VECTOR pos, out FMOD.VECTOR vel, out FMOD.VECTOR fwd, out FMOD.VECTOR upv));
    position = new Vector3(pos.x, pos.y, pos.z);
    velocity = new Vector3(vel.x, vel.y, vel.z);
    forward = new Vector3(fwd.x, fwd.y, fwd.z);
    up = new Vector3(upv.x, upv.y, upv.z);
  }

  /// <summary>The master group, whose volume is the game's overall volume.</summary>
  public SoundGroup? MasterSoundGroup
  {
    get
    {
      if (Handle.getMasterChannelGroup(out FMOD.ChannelGroup group) != FMOD.RESULT.OK) return null;
      return new SoundGroup(group);
    }
  }

  public Sound CreateSound(string path, Mode mode = Mode.Default)
  {
    Check.Ok(Handle.createSound(path, (FMOD.MODE)mode, out FMOD.Sound sound));
    return new Sound(sound);
  }

  /// <summary>
  /// Opens a sound without reading it all into memory. Music and the long ambiences use this;
  /// the game has several hundred megabytes of audio and loading it all would be absurd.
  /// </summary>
  public Sound CreateStream(string path, Mode mode = Mode.Default)
  {
    Check.Ok(Handle.createStream(path, (FMOD.MODE)mode, out FMOD.Sound sound));
    return new Sound(sound);
  }

  public Channel PlaySound(Sound sound, bool paused = false)
  {
    Check.Ok(Handle.playSound(sound.Handle, default, paused, out FMOD.Channel channel));
    return new Channel(channel);
  }

  public int GetChannelsPlaying()
  {
    Check.Ok(Handle.getChannelsPlaying(out int channels));
    return channels;
  }

  /// <summary>Voices playing, and how many of those are real rather than virtual.</summary>
  public void GetChannelsPlaying(out int channels, out int realChannels)
      => Check.Ok(Handle.getChannelsPlaying(out channels, out realChannels));

  public void Release()
  {
    Handle.release();
  }

  public void SetReverbProperties(int instance, ReverbProperties properties)
  {
    FMOD.REVERB_PROPERTIES native = properties.Native;
    Check.Ok(Handle.setReverbProperties(instance, ref native));
  }

  public Reverb3D CreateReverb3D()
  {
    Check.Ok(Handle.createReverb3D(out FMOD.Reverb3D reverb));
    return new Reverb3D(reverb);
  }

  public Dsp CreateDSPByType(DspType type)
  {
    Check.Ok(Handle.createDSPByType((FMOD.DSP_TYPE)type, out FMOD.DSP dsp));
    return new Dsp(dsp);
  }

  /// <summary>Must be called regularly or streams never advance and finished channels never free.</summary>
  public void Update() => Check.Ok(Handle.update());

  public void Close()
  {
    Handle.close();
    Handle.release();
  }
}

/// <summary>What a space sounds like. Each map carries one.</summary>
public readonly struct ReverbProperties
{
  internal readonly FMOD.REVERB_PROPERTIES Native;
  internal ReverbProperties(FMOD.REVERB_PROPERTIES native) => Native = native;
}

/// <summary>FMOD's built-in reverbs, by the names the maps already use.</summary>
public static class Preset
{
  public static ReverbProperties Off => new(FMOD.PRESET.OFF());
  public static ReverbProperties Generic => new(FMOD.PRESET.GENERIC());
  public static ReverbProperties Plain => new(FMOD.PRESET.PLAIN());
  public static ReverbProperties Room => new(FMOD.PRESET.ROOM());
  public static ReverbProperties StoneCorridor => new(FMOD.PRESET.STONECORRIDOR());
  public static ReverbProperties UnderWater => new(FMOD.PRESET.UNDERWATER());
  public static ReverbProperties Cave => new(FMOD.PRESET.CAVE());
  public static ReverbProperties Forest => new(FMOD.PRESET.FOREST());
  public static ReverbProperties Mountains => new(FMOD.PRESET.MOUNTAINS());
}

/// <summary>A reverb positioned in the world, audible within its own radius.</summary>
public readonly struct Reverb3D
{
  internal readonly FMOD.Reverb3D Handle;
  internal Reverb3D(FMOD.Reverb3D handle) => Handle = handle;

  public void Set3DAttributes(Vector3 position, float minDistance, float maxDistance)
  {
    FMOD.VECTOR pos = Check.Vec(position);
    Check.Ok(Handle.set3DAttributes(ref pos, minDistance, maxDistance));
  }

  public void Release() => Check.Ok(Handle.release());
}

public enum DspType
{
  PitchShift = FMOD.DSP_TYPE.PITCHSHIFT,
}

/// <summary>
/// A signal processor on a channel. The game uses exactly one: a pitch shift, which is what gives
/// each beboo a voice of its own rather than every one of them sounding identical.
/// </summary>
public readonly struct Dsp
{
  internal readonly FMOD.DSP Handle;
  internal Dsp(FMOD.DSP handle) => Handle = handle;

  public void SetParameterFloat(int index, float value)
      => Check.Ok(Handle.setParameterFloat(index, value));

  public void Release() => Check.Ok(Handle.release());
}

/// <summary>Entry point, and where the native library is found.</summary>
public static class Fmod
{
  public static FmodSystem CreateSystem()
  {
    Check.Ok(FMOD.Factory.System_Create(out FMOD.System system));
    return new FmodSystem(system);
  }

  /// <summary>
  /// The version of FMOD these bindings were generated for. Each platform's native library has to
  /// match: FMOD compares the two in init and refuses to start otherwise.
  /// </summary>
  public static uint BindingVersion => FMOD.VERSION.number;
}
