using BebooGarden.GameCore.Pet;
using BebooGarden.MiniGames;
using System;
using System.Collections.Generic;

namespace BebooGarden.GameCore;

/// <summary>
/// The three things the shared code needs to put in front of the player.
///
/// Game logic hatches an egg, finishes a race or opens a music box and wants a scene or a list
/// shown; it has no business knowing that the Windows build draws those with Myra widgets. A phone
/// shows the same three things with native views and the same logic drives both.
///
/// Every parameter here is a shared type, so nothing about this signature is desktop-shaped.
/// </summary>
public interface IGameUi
{
  /// <summary>
  /// The opening sequence for a brand new game: asks the player their name, favourite colour and
  /// the rest, then calls GameStart.FinishWelcome with what it collected.
  ///
  /// The head owns the asking because the desktop draws it and a phone speaks it, but neither
  /// decides what the answers mean - that is FinishWelcome, shared.
  /// </summary>
  void ShowWelcome();

  /// <summary>The scene that introduces a beboo that has just hatched.</summary>
  void ShowNewBeboo(Beboo beboo);

  /// <summary>
  /// The shop. Still only built on the desktop; the phone says so rather than going quiet, which
  /// on a game played by ear is the difference between "not yet" and "this is broken".
  /// </summary>
  void ShowShop();

  /// <summary>The podium at the end of a race. Each tuple is (contester index, score).</summary>
  void ShowRaceResult(
      (int, double) third,
      (int, double) second,
      (int, double) first,
      Beboo mainBeboo,
      RaceType raceType);

  /// <summary>
  /// Asks the player to pick one of <paramref name="choices"/>, by its key, and calls
  /// <paramref name="onChosen"/> with what they picked. Cancelling calls it with null.
  /// </summary>
  void Choose<T>(
      string title,
      Dictionary<string, T> choices,
      Action<T?> onChosen,
      bool allowCancel = true);
}
