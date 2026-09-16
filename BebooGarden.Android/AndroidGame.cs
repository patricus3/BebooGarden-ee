using BebooGarden;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Speech;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Item.MusicBox;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using BebooGarden.MiniGames;
using BebooGarden.Minigame;
using BebooGarden.Save;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BebooGarden.Droid;

/// <summary>
/// The phone's running game: what Game1 is on Windows, minus everything to do with a
/// window, a keyboard or a widget.
///
/// It holds no behaviour of its own. Beboos, items, maps and minigames all come from
/// BebooGarden.Core and behave here exactly as they do on the desktop, because they are the same
/// compiled code. What this supplies is the handful of things <see cref="IGame"/> promises them.
/// </summary>
public sealed class AndroidGame : IGame
{
  private Map? _backedMap;

  public SoundSystem SoundSystem { get; }
  public Map? Map { get; private set; }
  public Random Random { get; } = new();
  public SaveParameters Save { get; private set; } = null!;
  public Vector3 PlayerPosition { get; set; }

  /// <summary>The beboo being carried, if any.</summary>
  public Beboo? BebooInArms { get; set; }

  /// <summary>The item taken out of the bag and not yet put down, if any.</summary>
  public Item? ItemInHand { get; set; }
  public IMiniGame? CurrentPlayingMiniGame { get; set; }
  public List<Item> Inventory { get; set; } = [];
  public IGameUi Ui { get; }

  public bool Paused { get; private set; }

  /// <summary>
  /// Work handed over from the UI thread, run on the next tick.
  ///
  /// Nothing the player does may run on Android's main thread. Plenty of the game blocks it for as
  /// long as a sound lasts - PlayCinematic spins until the channel finishes, MusicFadeOut sleeps in
  /// quarter-second steps - which is entirely reasonable on a desktop game loop and is an
  /// Application Not Responding dialog on a phone. Hatching the first egg plays a cinematic, so
  /// this was not a corner case: it was the first thing a new player does.
  /// </summary>
  private readonly ConcurrentQueue<Action> _pending = new();

  /// <summary>Hands work to the game thread. Safe to call from anywhere.</summary>
  public void Post(Action work) => _pending.Enqueue(work);

  /// <summary>Runs whatever the UI thread handed over. Called once per tick, on the game thread.</summary>
  private void DrainPending()
  {
    while (_pending.TryDequeue(out Action? work))
    {
      try { work(); }
      catch (Exception error) { MainActivity.RecordFault("player action", error); }
    }
  }

  public AndroidGame(IGameUi ui)
  {
    Ui = ui;
    SoundSystem = new SoundSystem();
  }

  /// <summary>
  /// Boots a garden. Mirrors Game1.Initialize: find mods before loading voices, read the save, pick
  /// the map it was left on, then load the sounds.
  /// </summary>
  public void Start()
  {
    Modding.ModManager.Discover();
    Save = SaveManager.LoadSave();
    Modding.ModManager.SetEnabled(Save.EnabledMods ?? []);

    Save.Flags.UnlockEggInShop =
        Save.Flags.UnlockUnderwaterMap || Save.Flags.UnlockSnowyMap || Save.Flags.UnlockEggInShop;
    Save.Flags.UnlockBeachMap = true;

    try
    {
      Map = Save.CurrentMap is not MapPreset.basicrace and not MapPreset.snowyrace
          ? Map.Maps[Save.CurrentMap]
          : Map.Garden;
    }
    catch (Exception)
    {
      Map = Map.Garden;
    }

    MusicBox.AvailableRolls = Save.UnlockedRolls ?? [];
    SoundSystem.LoadMainScreen();

    // Reading the save is only half of loading a game. This is the half that puts the beboos and
    // the things on the ground back where they were; without it every launch built an empty
    // garden and the player's beboos looked lost.
    SaveRestore.Apply(this);
  }

  /// <summary>
  /// One tick of the world. Driven by the activity rather than a fixed 60 Hz loop: this game draws
  /// nothing, and spinning a phone's CPU to clear a screen nobody looks at is just battery.
  /// </summary>
  public void Update()
  {
    DrainPending();
    if (Paused || Map is null) return;

    foreach (Beboo beboo in Map.Beboos.ToList())
    {
      beboo.Update();
      if (Map.IsLullabyPlaying) beboo.GoAsleep();
      else if (Map.IsDansePlaying) beboo.WakeUp();
    }

    Map.Update();
    foreach (Item item in Map.Items.ToList()) item?.Update();

    SoundSystem.System.Update();
  }

  public void MovePlayerTo(Vector3 position)
  {
    PlayerPosition = position;
    SoundSystem.MovePlayerTo(position);
  }

  // --- IGame ---------------------------------------------------------------

  public void GainTicket(int amount)
  {
    if (amount <= 0) return;
    Save.Tickets += amount;
    Voice.Current.Say(string.Format(Content.BebooText.gainticket, amount));
    SoundSystem.System.PlaySound(SoundSystem.MenuOk2Sound);
    if (!Save.Flags.UnlockShop && Map != Map.SnowyRace && Map != Map.BasicRace)
      Save.Flags.UnlockShop = true;
  }

  public void ChangeMapMusic()
  {
    if (Map != null) SoundSystem.PlayMapMusic(Map);
  }

  public void ChangeMap(Map map, bool backup = true)
  {
    if (Map != null) SoundSystem.Pause(Map);
    if (backup) _backedMap = Map;
    Map = map;
    SoundSystem.LoadMap(map);
    foreach (Map otherMap in Map.Maps.Values)
      if (otherMap != map) SoundSystem.Pause(otherMap);
  }

  public void LoadBackedMap()
  {
    if (_backedMap is null || Map is null) return;
    SoundSystem.Pause(Map);
    Map = _backedMap;
    _backedMap = null;
    SoundSystem.Unpause(Map);
  }

  public void Pause()
  {
    Paused = true;
    if (Map is null) return;
    foreach (Beboo beboo in Map.Beboos) beboo.Pause();
    SoundSystem.DisableAmbiTimer();
    SoundSystem.Pause(Map);
  }

  public void Unpause()
  {
    Paused = false;
    if (Map is null) return;
    foreach (Beboo beboo in Map.Beboos) beboo.Unpause();
    SoundSystem.EnableAmbiTimer();
    SoundSystem.Unpause(Map);
  }

  /// <summary>
  /// Screens are a desktop idea - a Myra panel swapped into a Desktop. The phone has real views and
  /// the activity decides what is on top, so this only records where the game thinks it is.
  /// </summary>
  public GameScreen CurrentScreen { get; private set; } = GameScreen.game;

  public void SwitchToScreen(GameScreen screen) => CurrentScreen = screen;

  /// <summary>Writes the garden down. Called when the activity is going away.</summary>
  public void WriteSave()
  {
    if (Map is null) return;
    SaveCapture.Write(this);
  }
}
