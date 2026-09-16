using BebooGarden.GameCore;
using System;

namespace BebooGarden.Save;

/// <summary>
/// Writes the garden down every so often while it is being played.
///
/// The game used to save only on the way out. That is fine when the way out is you closing a
/// window, and it is not fine anywhere else: Android kills a backgrounded app whenever it wants the
/// memory and does not ask first, and a desktop can still lose a session to a crash or a power cut.
/// Either way the answer is the same - do not let very much time pass between the garden changing
/// and the garden being written down.
///
/// Wall clock, not ticks, so it behaves the same on a phone ticking at 20 Hz and a desktop at 60.
/// </summary>
public static class AutoSave
{
  /// <summary>
  /// How long between saves. Long enough that writing is never what the game is busy doing, short
  /// enough that losing the gap would not upset anybody.
  /// </summary>
  public static TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(2);

  private static DateTime _lastSaved = DateTime.Now;

  /// <summary>
  /// Saves if it is time to. Call from the game loop; it decides for itself whether to do anything,
  /// so calling it every tick costs a comparison.
  /// </summary>
  public static void Tick(IGame game)
  {
    if (DateTime.Now - _lastSaved < Interval) return;
    Now(game);
  }

  /// <summary>
  /// Saves immediately. Never throws: a save that fails must not take the game down with it, and
  /// on a phone this runs while the app is being put away.
  /// </summary>
  public static void Now(IGame game)
  {
    _lastSaved = DateTime.Now;

    // Nothing to write before the world exists, and writing then would save an empty garden over
    // a real one.
    if (game.Map is null || game.Save is null || game.Save.Flags.NewGame) return;

    try
    {
      SaveCapture.Write(game);
    }
    catch (Exception)
    {
      // Deliberately swallowed. The next one is two minutes away, and a garden that keeps playing
      // is better than one that stops because a disk was busy.
    }
  }

  /// <summary>Pushes the next save back, after one has been written by other means.</summary>
  public static void Defer() => _lastSaved = DateTime.Now;
}
