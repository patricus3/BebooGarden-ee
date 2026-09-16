using BebooGarden.Save;
using CrossSpeak;
using Microsoft.Xna.Framework.Input;
using SharpHook;
using System.Diagnostics;
using System;
using System.Threading.Tasks;
using BebooGarden.GameCore.Item.MusicBox;
using BebooGarden.GameCore.World;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using BebooGarden.Content;

namespace BebooGarden;

public partial class Game1
{
  public Map? Map { get; private set; }
  public System.Numerics.Vector3 PlayerPosition { get; set; }
  public DateTime LastPressedKeyTime { get; private set; }

  protected override void Initialize()
  {
    base.Initialize();
    Window.Title = GAMENAME;
    this.Exiting += OnExit;
    CrossSpeakManager.Instance.Initialize();
    // create hook to get keyboard and simulated keyboard (e.g. screen readers inputs) 
    TaskPoolGlobalHook hook = new();
    hook.KeyPressed += OnKeyPressed;
    hook.KeyReleased += OnKeyReleased;
    Task.Run(() => hook.Run());
    _previousKeyboardState = Keyboard.GetState();
    _previousMouseState = Mouse.GetState();
    // Before LoadMainScreen, which reads every voice folder including the mods'.
    Modding.ModManager.Discover();
    Save=SaveManager.LoadSave();
    Modding.ModManager.SetEnabled(Save.EnabledMods ?? []);
    Save.Flags.UnlockEggInShop = Save.Flags.UnlockUnderwaterMap || Save.Flags.UnlockSnowyMap || Save.Flags.UnlockEggInShop;
    Save.Flags.UnlockBeachMap = true;
    try
    {
      if (Save.CurrentMap != MapPreset.basicrace && Save.CurrentMap != MapPreset.snowyrace)
        Map = Map.Maps[Save.CurrentMap];
      else
        Map = Map.Garden;
    }
    catch (Exception) { Map = Map.Garden; }
    MusicBox.AvailableRolls = Save.UnlockedRolls ?? [];
    SoundSystem.LoadMainScreen();
    // Putting a loaded save back into the world is shared with the Android head; see
    // Save.SaveRestore. Language stays here: the desktop asks, a phone already knows.
    if (!Save.Flags.NewGame) ApplyLanguage(Save.Language);
    BebooGarden.Save.SaveRestore.Apply(this);
    LastPressedKeyTime = DateTime.Now;
    if (Save.FruitsBasket == null || Save.FruitsBasket.Count == 0)
    {
      Save.FruitsBasket = [];
      foreach (FruitSpecies fruitSpecies in Enum.GetValues(typeof(FruitSpecies))) Save.FruitsBasket[fruitSpecies] = 0;
    }
  }
}
