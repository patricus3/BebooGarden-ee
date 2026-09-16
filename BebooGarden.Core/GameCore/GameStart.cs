using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.World;
using System;
using System.Linq;
using System.Numerics;

namespace BebooGarden.GameCore;

/// <summary>What the player told the game about themselves when they first opened it.</summary>
public sealed class WelcomeAnswers
{
  public string PlayerName { get; set; } = string.Empty;
  public string? FavoredColor { get; set; }
  public string FreeTime { get; set; } = string.Empty;
  public string? Dessert { get; set; }
}

/// <summary>
/// Opening the garden: what happens between the save being read and the player being in it.
///
/// This lived inside the Windows Game1, which meant the Android head did not do any of it - it
/// loaded a save, loaded the sounds and then simply sat there. No mods were started, no music was
/// chosen, and a brand new game never got its opening sequence at all, so the phone said "welcome"
/// and then nothing happened for ever.
///
/// The sequence is shared. The way it is presented is not: the desktop draws a scripted scene with
/// Myra widgets, a phone speaks and uses native dialogs. So the head owns the asking, through
/// <see cref="IGameUi.ShowWelcome"/>, and hands the answers back to <see cref="FinishWelcome"/> -
/// which is where every decision that actually changes the garden lives, once, for both.
/// </summary>
public static class GameStart
{
  /// <summary>
  /// Called once, when the game is ready to be played. Starts the player's mods, then either runs
  /// the opening sequence or drops them straight into the garden they already had.
  /// </summary>
  public static void Begin(IGame game)
  {
    Modding.ModHost.StartEnabledMods();

    if (game.Save.Flags.NewGame)
    {
      game.Ui.ShowWelcome();
      return;
    }

    game.ChangeMapMusic();
    game.SwitchToScreen(GameScreen.game);
  }

  /// <summary>
  /// Takes what the opening sequence collected and makes it true, then opens the garden. Both
  /// heads call this at the end of their own presentation.
  ///
  /// Clears NewGame, because from here there is a garden worth keeping. It used to stay set until
  /// the first beboo was named, which meant quitting in between threw the whole garden away and
  /// started the opening sequence over. Whether the tips have been seen is Flags.TipsShown now.
  /// </summary>
  public static void FinishWelcome(IGame game, WelcomeAnswers answers)
  {
    if (answers is null) throw new ArgumentNullException(nameof(answers));

    game.Save.PlayerName = answers.PlayerName;
    game.Save.FavoredColor = answers.FavoredColor ?? "none";
    game.Save.FreeTime = answers.FreeTime;
    game.Save.Dessert = answers.Dessert ?? string.Empty;

    PutEggOfFavoredColor(game);
    game.Save.Flags.NewGame = false;

    game.ChangeMapMusic();
    game.SwitchToScreen(GameScreen.game);
  }

  /// <summary>
  /// The starting egg is put down before the player is asked anything, so swap it for one of the
  /// colour they chose.
  /// </summary>
  private static void PutEggOfFavoredColor(IGame game)
  {
    Map? map = game.Map;
    if (map is null) return;

    foreach (Egg egg in map.Items.OfType<Egg>().ToList())
    {
      egg.SoundLoopBehaviour.Stop();
      if (egg.Channel != null && egg.Channel.IsPlaying) egg.Channel.Stop();
      map.Items.Remove(egg);
    }

    map.AddItem(new Egg(game.Save.FavoredColor), new Vector3(2, 0, 0));
  }
}
