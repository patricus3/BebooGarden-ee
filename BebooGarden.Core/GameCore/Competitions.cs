using BebooGarden.Content;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using BebooGarden.MiniGames;
using BebooGarden.Minigame;
using System;
using System.Collections.Generic;

namespace BebooGarden.GameCore;

/// <summary>
/// Entering a beboo into a race or a jumping contest: which event, which beboo, which track.
///
/// This was Windows-only, built out of Myra choice menus, which is why walking into the race gate
/// on a phone did nothing at all. None of the deciding is about widgets though - it is which
/// events are open today, how many tries are left, and which of your beboos is going in - so it
/// lives here and asks through <see cref="IGameUi.Choose"/>, which the desktop draws and the phone
/// speaks.
/// </summary>
public static class Competitions
{
  /// <summary>
  /// Offers whatever is open today. Called when the player uses the race gate.
  /// </summary>
  public static void ShowMenu(IGame game)
  {
    if (!Competition.IsAnyOpen())
    {
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      Voice.Current.Say(BebooText.competition_closed);
      return;
    }

    if (game.Map is null || game.Map.Beboos.Count == 0)
    {
      Voice.Current.Say(BebooText.nobeboo);
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      return;
    }

    Dictionary<string, CompetitionType> options = [];
    foreach ((string name, CompetitionType type) in new[]
             {
               (BebooText.competition_race, CompetitionType.Race),
               (BebooText.jump_name, CompetitionType.Jump),
             })
    {
      // The label carries how many goes are left, so the player knows before choosing.
      options.Add(
          string.Format(BebooText.competition_entry, name, Competition.GetRemainingTriesToday(type)),
          type);
    }

    game.Ui.Choose<CompetitionType>(BebooText.competition_choose, options, chosen =>
    {
      if (chosen != CompetitionType.None) ChooseBeboo(game, chosen);
    });
  }

  /// <summary>Asks which beboo is going in, unless there is only one, in which case it is obvious.</summary>
  private static void ChooseBeboo(IGame game, CompetitionType type)
  {
    if (game.Map is null || game.Map.Beboos.Count == 0)
    {
      Voice.Current.Say(BebooText.nobeboo);
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      return;
    }

    if (game.Map.Beboos.Count == 1)
    {
      Start(game, type, game.Map.Beboos[0]);
      return;
    }

    Dictionary<string, Beboo> options = [];
    for (int i = 0; i < game.Map.Beboos.Count; i++)
    {
      Beboo beboo = game.Map.Beboos[i];
      // Two beboos may share a name; the number keeps them apart in a spoken list.
      string label = options.ContainsKey(beboo.Name) ? $"{beboo.Name} {i + 1}" : beboo.Name;
      options[label] = beboo;
    }

    game.Ui.Choose<Beboo>(BebooText.choosebeboo, options, chosen =>
    {
      if (chosen != null) Start(game, type, chosen);
    });
  }

  private static void Start(IGame game, CompetitionType type, Beboo contester)
  {
    // Checked again here rather than only in the menu: choosing is not instant, and the last try
    // of the day can be used up by something else in between.
    if (!Competition.IsOpen(type))
    {
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      Voice.Current.Say(BebooText.competition_closed);
      return;
    }

    if (type == CompetitionType.Jump)
    {
      StartMiniGame(game, new JumpContest(contester));
      return;
    }

    Dictionary<string, RaceType> tracks = new() { { BebooText.race_simple, RaceType.Base } };
    if (game.Save.Flags.UnlockSnowyMap) tracks.Add(BebooText.race_snow, RaceType.Snowy);

    if (tracks.Count == 1)
    {
      StartMiniGame(game, new Race(RaceType.Base, contester));
      return;
    }

    game.Ui.Choose<RaceType>(BebooText.race_chooserace, tracks, track =>
    {
      if (track != RaceType.None) StartMiniGame(game, new Race(track, contester));
    });
  }

  private static void StartMiniGame(IGame game, IMiniGame miniGame)
  {
    if (Competition.IsRunning || game.CurrentPlayingMiniGame != null) return;
    game.CurrentPlayingMiniGame = miniGame;
    miniGame.Start();
  }
}
