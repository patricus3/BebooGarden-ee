using BebooGarden.GameCore;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BebooGarden.Save;

/// <summary>
/// Puts a loaded save back into the world: the beboos where they were, the things on the ground,
/// the fruit that grew while you were gone.
///
/// Reading the file is only half of loading a game, and this was the other half - written into the
/// Windows Game1 and nowhere else. The Android head read the save perfectly well and then never
/// applied it, so every launch built an empty garden and every beboo appeared to have been lost.
/// The save was never the problem; nothing was reading it back.
///
/// <see cref="SaveCapture"/> is the other side of this, and the two have to agree about what is
/// worth keeping, which is a good reason for them to sit next to each other.
/// </summary>
public static class SaveRestore
{
  /// <summary>
  /// Where a brand new player is put down: a couple of steps from the egg, so the first thing they
  /// ever do is walk to it.
  /// </summary>
  private static readonly Vector3 NewGameStart = new(-2, 0, 0);

  private static readonly Vector3 FirstEggAt = new(2, 0, 0);

  public static void Apply(IGame game)
  {
    if (game.Map is null) return;

    if (game.Save.Flags.NewGame) StartFreshGarden(game);
    else RestoreGarden(game);

    game.SoundSystem.LoadMap(game.Map);
    game.ChangeMapMusic();
    game.SoundSystem.MusicVolume = game.Save.MusicLevel ?? 1f;
    game.SoundSystem.MusicMuted = game.Save.MusicMuted;
  }

  private static void StartFreshGarden(IGame game)
  {
    game.PlayerPosition = NewGameStart;
    game.Map!.AddItem(new Egg(game.Save.FavoredColor), FirstEggAt);
  }

  private static void RestoreGarden(IGame game)
  {
    game.PlayerPosition = new Vector3(0, 0, 0);

    foreach (Map map in Map.Maps.Values)
    {
      if (map.TreeLines.Count > 0)
      {
        // Fruit keeps growing while the game is closed, up to what the trees hold.
        map.TreeLines[0].SetFruitsAfterAWhile(
            game.Save.LastPlayed,
            game.Save.MapInfos.GetValueOrDefault(map.Preset, game.Save.MapInfos[0]).RemainingFruits);
      }

      map.Items = game.Save.MapInfos.TryGetValue(map.Preset, out MapInfo? info) && info is not null
          ? info.Items
          : [];

      if (info is not null) RestoreBeboos(game, map, info);

      if (map != game.Map)
      {
        foreach (Beboo asleep in map.Beboos) asleep.Pause();
        game.SoundSystem.Pause(map);
      }
    }
  }

  private static void RestoreBeboos(IGame game, Map map, MapInfo info)
  {
    foreach (BebooInfo saved in info.BebooInfos)
    {
      // "bob" and "boby" are the placeholder names an unnamed beboo carries; they are not real
      // beboos and bringing them back would fill the garden with them.
      if (saved.Name is "bob" or "boby") continue;

      BebooType type = saved.BebooType != BebooType.Base
          ? saved.BebooType
          : game.Random.Next(2) == 1 && game.Save.FavoredColor != "none"
              ? Util.GetBebooTypeByColor(game.Save.FavoredColor)
              : Util.GetRandomBebooType();

      // LastPlayed, not now: how long it has been alone is what decides its mood, and
      // OfflineProgress slows that right down on a platform that is opened and closed all day.
      Beboo beboo = new(
          saved.Name, type, saved.Age, game.Save.LastPlayed,
          saved.Happiness, saved.Energy, saved.SwimLevel,
          racer: false, saved.Voice, saved.Trait)
      {
        KnowItsName = saved.KnowItsName || saved.Age >= 2,
        ModCreature = saved.ModCreature,
      };

      map.Beboos.Add(beboo);
    }
  }
}
