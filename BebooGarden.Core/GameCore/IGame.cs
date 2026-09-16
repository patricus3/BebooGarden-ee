using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.World;
using BebooGarden.MiniGames;
using BebooGarden.Save;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BebooGarden.GameCore;

/// <summary>
/// What the running game offers the shared code.
///
/// This replaces reaching for Game1.Instance from inside beboo behaviour, items and minigames. The
/// singleton was a Windows class, so every one of those reaches was a line of game logic that could
/// only ever compile against the desktop build; the surface it was actually used for is this, and
/// this has nothing platform-specific in it.
///
/// It is deliberately only what the shared code asks for today. Two members that showed up in the
/// old call sites, ChooseBeboo and UpdateMapMusic, appear exclusively in commented-out code and are
/// left off until something wants them.
/// </summary>
public interface IGame
{
  SoundSystem SoundSystem { get; }
  Map? Map { get; }
  Random Random { get; }
  SaveParameters Save { get; }
  /// <summary>Where the player stands. Settable because walking is shared code now.</summary>
  Vector3 PlayerPosition { get; set; }

  /// <summary>The beboo currently being carried, if any.</summary>
  Pet.Beboo? BebooInArms { get; set; }

  /// <summary>The item taken out of the bag and not yet put down, if any.</summary>
  Item.Item? ItemInHand { get; set; }
  IMiniGame? CurrentPlayingMiniGame { get; set; }
  List<Item.Item> Inventory { get; set; }

  void GainTicket(int amount);
  void ChangeMapMusic();
  void ChangeMap(Map map, bool backup = true);
  void LoadBackedMap();
  void Pause();
  void Unpause();
  void SwitchToScreen(GameScreen screen);

  /// <summary>Scenes and menus, drawn by whatever the platform draws with.</summary>
  IGameUi Ui { get; }
}

/// <summary>
/// Where the shared code finds the running game. The head installs itself here at startup, the same
/// way it installs a voice on <see cref="Speech.Voice"/>.
/// </summary>
public static class GameHost
{
  private static IGame? _current;

  public static IGame Current => _current
      ?? throw new InvalidOperationException(
          "No IGame installed. The platform head must call GameHost.Use before the game starts.");

  public static void Use(IGame game) => _current = game;
}
