using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Item.MusicBox;
using BebooGarden.GameCore.World;
using BebooGarden.Minigame;
using BebooGarden.MiniGames;
using BebooGarden.Save;
using BebooGarden.UI.ScriptedScene;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using Myra;
using Myra.Graphics2D.UI;
using System;
using System.Linq;
using System.Numerics;

namespace BebooGarden;

public partial class Game1
{
  protected override void LoadContent()
  {
    _spriteBatch = new SpriteBatch(GraphicsDevice);
    Save = SaveManager.LoadSave();
    if (Save.RaceScores != null) Race.RaceScores = Save.RaceScores;
    Race.TotalWin = Save.RaceTotalWin;
    if (Save.LastPlayed.Day == DateTime.Now.Day)
    {
      Competition.TodayTries = Save.CompetitionTries ?? [];
      // Saves written before contests had separate allowances only carry the race's count.
      if (Competition.TodayTries.Count == 0 && Save.RaceTodayTries > 0)
        Competition.TodayTries[CompetitionType.Race] = Save.RaceTodayTries;
    }
    else Competition.ResetDay();
    Save.Flags.UnlockEggInShop = Save.Flags.UnlockUnderwaterMap || Save.Flags.UnlockSnowyMap || Save.Flags.UnlockEggInShop;
    try
    {
      if (Save.CurrentMap != MapPreset.basicrace && Save.CurrentMap != MapPreset.snowyrace)
        Map = Map.Maps[Save.CurrentMap];
      else
        Map = Map.Garden;
    }
    catch (Exception) { Map = Map.Garden; }
    MusicBox.AvailableRolls = Save.UnlockedRolls ?? [];
    SoundSystem.Volume = Save.Volume;
    SoundSystem.LoadMainScreen();
    if (!Save.Flags.NewGame)
    {
    }
    else
    {
      PlayerPosition = new Vector3(-2, 0, 0);
      Map.AddItem(new Egg(Save.FavoredColor), new(2, 0, 0));
    }
    SoundSystem.MusicVolume = Save.MusicLevel ?? 1f;
    SoundSystem.MusicMuted = Save.MusicMuted;
    LastPressedKeyTime = DateTime.Now;
    if (Save.FruitsBasket == null || Save.FruitsBasket.Count == 0)
    {
      Save.FruitsBasket = [];
      foreach (FruitSpecies fruitSpecies in Enum.GetValues(typeof(FruitSpecies))) Save.FruitsBasket[fruitSpecies] = 0;
    }
    Inventory = Save.Inventory;
    MediaPlayer.Volume = 0.3f;
    SoundEffect.MasterVolume = 1f;
    MyraEnvironment.Game = this;
    _desktop = new Desktop
    {
      HasExternalTextInput = true
    };
    // The garden itself is played by ear, but it still needs a root: a null one makes
    // Desktop.Root null on every switch back to the game, which the menu bookkeeping cannot take.
    _gamePanel = new Panel();
    Window.TextInput += (s, a) =>
    {
      _desktop.OnChar(a.Character);
    };
    // Not from here. MonoGame calls LoadContent from inside base.Initialize, which runs before the
    // rest of Initialize has discovered the mods and read which of them are switched on - so this
    // used to decide both questions against an empty list, and no mod ever started. The first
    // Update tick is after all of it, whichever order the graphics device happens to be created in.
    _gardenNotStarted = true;
  }

  /// <summary>Whether the garden still has to be opened. Cleared on the first update.</summary>
  private bool _gardenNotStarted;

  /// <summary>
  /// Opens the garden, asking about mods first when there is one the player has not seen before.
  /// Asking every launch would put a screen between them and their beboos forever, for an answer
  /// that almost never changes, so the list stays reachable from the main menu instead.
  /// </summary>
  internal void BeginGardenIfNeeded()
  {
    if (!_gardenNotStarted) return;
    _gardenNotStarted = false;
    if (Modding.ModManager.All.Any(mod => !(Save.KnownMods ?? []).Contains(mod.Id)))
      new UI.ModMenu(StartTheGarden).Show();
    else StartTheGarden();
  }

  /// <summary>
  /// Opening the garden is shared with the Android head; see GameCore.GameStart.
  /// </summary>
  private void StartTheGarden() => GameCore.GameStart.Begin(this);
}