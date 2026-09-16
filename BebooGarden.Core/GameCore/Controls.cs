using BebooGarden.Content;

namespace BebooGarden.GameCore;

/// <summary>
/// How the player is holding the game, so the game can explain itself in their terms.
///
/// The tips the game reads out are prose, and prose about arrow keys and the space bar is worse
/// than useless on a phone - it tells a blind player to press things that are not there. So the
/// explanations come in two wordings and this picks between them.
///
/// The touch wordings are English only for now. ResourceManager falls back to the neutral culture
/// when a translation has not got a key yet, so every other language still hears something correct
/// about the game and gets the keyboard wording until somebody translates the touch one.
/// </summary>
public static class Controls
{
  /// <summary>
  /// True when the player is using gestures rather than a keyboard. Set once by the head at
  /// startup; the Windows build leaves it false.
  /// </summary>
  public static bool IsTouch { get; set; }

  /// <summary>Where the garden is, and how to reach the egg, worded for this platform.</summary>
  public static string Welcome2 => Pick(BebooText.ui_welcome2_touch, BebooText.ui_welcome2);

  /// <summary>The things that matter, worded for this platform.</summary>
  public static string QuickTips => Pick(BebooText.ui_quicktips_touch, BebooText.ui_quicktips);

  private static string Pick(string? touch, string keyboard)
      => IsTouch && !string.IsNullOrWhiteSpace(touch) ? touch! : keyboard;
}
