using BebooGarden.Content;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using BebooGarden.GameCore.World;
using BebooGarden.MiniGames;
using BebooGarden.Modding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BebooGarden.GameCore;

/// <summary>
/// Everything the player can actually do in the garden: walking, looking, petting, picking things
/// up, whistling, carrying a beboo.
///
/// All of it used to live in the Windows Game1, wired straight to arrow keys - which is why the
/// phone could hear a garden but not move around in it. None of it is about keyboards, though;
/// walking west is walking west whether that came from an arrow key, a flick or a gamepad. So it
/// lives here, and each head only decides what gesture means what.
/// </summary>
public static class PlayerActions
{
  /// <summary>How close together two steps may be. Stops a held key or a fast flick sprinting.</summary>
  public static readonly TimeSpan StepInterval = TimeSpan.FromMilliseconds(150);

  public static readonly Vector3 West = new(-1, 0, 0);
  public static readonly Vector3 East = new(1, 0, 0);
  public static readonly Vector3 North = new(0, 1, 0);
  public static readonly Vector3 South = new(0, -1, 0);

  // --- Walking -------------------------------------------------------------

  public static void MoveOf(IGame game, Vector3 movement)
  {
    Map? map = game.Map;
    if (map is null) return;

    Vector3 newPos = map.Clamp(game.PlayerPosition + movement);
    if (newPos != game.PlayerPosition + movement)
      game.SoundSystem.System.PlaySound(game.SoundSystem.WallSound);
    else if (map.IsInWater(newPos))
      game.SoundSystem.PlayWaterCursorSound();
    else
      game.SoundSystem.PlayCursorSound();

    game.PlayerPosition = newPos;
    MapConnexion? connexion = map.GetConnexionArroundPosition(game.PlayerPosition);
    game.SoundSystem.MovePlayerTo(newPos);

    if (game.Save.Flags.UnlockShop && map.IsArroundShop(game.PlayerPosition))
      Voice.Current.Say(BebooText.shop);
    else if (map.IsArroundRaceGate(game.PlayerPosition))
      Voice.Current.Say(string.Format(BebooText.competition_gate, Competition.GetRemainingTriesToday()));
    else if (connexion?.Map.IsUnlocked() ?? false)
      Voice.Current.Say(connexion.Nme);

    SpeakObjectUnderCursor(game);
  }

  /// <summary>Says what is where the player is now standing. This is the whole game's map display.</summary>
  public static void SpeakObjectUnderCursor(IGame game)
  {
    TreeLine? treeLine = game.Map?.GetTreeLineAtPosition(game.PlayerPosition);
    Item.Item? item = game.Map?.GetItemArroundPosition(game.PlayerPosition);

    foreach (Beboo beboo in BeboosUnderCursor(game, 1)) Voice.Current.Say(beboo.Name);

    if (treeLine != null)
    {
      if (treeLine.Fruits == treeLine.FruitPerHour) Voice.Current.Say(BebooText.trees_full);
      else if (treeLine.Fruits == 0) Voice.Current.Say(BebooText.trees_empty);
      else if (treeLine.Fruits <= treeLine.FruitPerHour / 2) Voice.Current.Say(BebooText.trees_soonempty);
      else Voice.Current.Say(BebooText.trees_soonfull);
    }
    else if (item != null) Voice.Current.Say(item.Name);
  }

  // --- Who is here ---------------------------------------------------------

  public static Beboo? BebooUnderCursor(IGame game)
      => game.Map?.Beboos.FirstOrDefault(b => Util.IsInSquare(b.Position, game.PlayerPosition, 1));

  public static List<Beboo> BeboosUnderCursor(IGame game, int squareSide = 1)
      => game.Map?.Beboos.Where(b => Util.IsInSquare(b.Position, game.PlayerPosition, squareSide)).ToList()
         ?? [];

  // --- Hands ---------------------------------------------------------------

  public static void ShakeOrPetAtPlayerPosition(IGame game)
  {
    TreeLine? treeLine = game.Map?.GetTreeLineAtPosition(game.PlayerPosition);
    if (treeLine != null)
    {
      FruitSpecies? dropped = treeLine.Shake();
      if (dropped != null && game.Save.FruitsBasket != null)
      {
        if (game.Save.FruitsBasket.TryGetValue(dropped.Value, out _))
          game.Save.FruitsBasket[dropped.Value]++;
        else
          game.Save.FruitsBasket[dropped.Value] = 1;
      }
      return;
    }

    BebooUnderCursor(game)?.GetPetted();
  }

  public static void TakeOrPutDownBeboo(IGame game)
  {
    if (game.BebooInArms != null)
    {
      game.BebooInArms.PutDown();
      game.BebooInArms = null;
      game.SoundSystem.System.PlaySound(game.SoundSystem.ItemPutSound);
      return;
    }

    if (game.ItemInHand != null)
    {
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      Voice.Current.Say(BebooText.beboo_handsfull);
      return;
    }

    Beboo? beboo = BebooUnderCursor(game);
    if (beboo is null)
    {
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      return;
    }

    beboo.PickUp();
    game.BebooInArms = beboo;
  }

  public static void SwayBebooInArms(IGame game, bool toLeft) => game.BebooInArms?.Sway(toLeft);

  /// <summary>
  /// Picks something up off the ground, unless a mod wants to handle it instead. A mod that takes
  /// charge settles it in its own time, which may be after asking the player something.
  /// </summary>
  public static void TakeFromGround(IGame game, Item.Item item)
  {
    if (ModHost.Instance.PickupHandled(item, item.Take)) return;
    item.Take();
  }

  public static void TryPutItemInHand(IGame game)
  {
    Item.Item? item = game.ItemInHand;
    if (item is null) return;

    string? refusal = game.Map != null ? item.WhyItCannotGoOn(game.Map) : null;
    if (refusal != null) { RefuseToPutDown(game, item, refusal); return; }

    bool inWater = game.Map?.IsInWater(game.PlayerPosition) ?? false;
    if (inWater && !item.IsWaterProof)
    {
      RefuseToPutDown(game, item, BebooText.ui_warningwater);
      return;
    }

    // AddItem refuses a tree line. Ignoring that used to announce the drop, take the item out of
    // the bag and add it nowhere, which quietly destroyed it.
    if (game.Map?.AddItem(item, game.PlayerPosition) != true)
    {
      RefuseToPutDown(game, item, BebooText.ui_cantputhere);
      return;
    }

    Voice.Current.Say(string.Format(BebooText.ui_itemput, item.Name));
    game.SoundSystem.System.PlaySound(
        inWater ? game.SoundSystem.ItemPutWaterSound : game.SoundSystem.ItemPutSound);
    game.Inventory.Remove(item);
    game.ItemInHand = null;
  }

  /// <summary>
  /// Says why the item cannot go here and puts it back in the bag. Keeping hold of it would leave
  /// the action stuck on retrying the drop, with no way to do anything else.
  /// </summary>
  private static void RefuseToPutDown(IGame game, Item.Item item, string reason)
  {
    game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
    Voice.Current.Say(reason);
    Voice.Current.Say(string.Format(BebooText.ui_itembacktobag, item.Name));
    game.ItemInHand = null;
  }

  /// <summary>
  /// Walks through a gate to the map on the other side, taking any beboos standing close enough
  /// with you - and a fluff ball follows its friend across, which is the whole point of it.
  /// </summary>
  public static void TravelThrough(IGame game, MapConnexion connexion)
  {
    Map? from = game.Map;
    if (from is null) return;
    if (!Map.Maps.TryGetValue(connexion.MapPreset, out Map? to) || to is null) return;

    foreach (Beboo travelling in BeboosUnderCursor(game, 2))
    {
      from.Beboos.Remove(travelling);
      travelling.Position = new Vector3(0, 0, 0);
      to.Beboos.Add(travelling);
      FluffBall.FollowFriend(travelling, from, to);
    }

    game.ChangeMap(to);
    game.ChangeMapMusic();
    game.PlayerPosition = new Vector3(0, 0, 0);
  }

  /// <summary>
  /// Offers a beboo something from the fruit basket.
  ///
  /// The beboo is captured before the asking rather than looked up again afterwards. It used to be
  /// found a second time once a fruit had been chosen, and beboos walk about: choose slowly and the
  /// fruit went to whichever one had wandered over, or to nobody at all while still being taken out
  /// of the basket.
  /// </summary>
  public static void FeedBeboo(IGame game)
  {
    Beboo? beboo = BebooUnderCursor(game);
    if (beboo is null || game.Save.FruitsBasket is null) return;

    Dictionary<string, FruitSpecies> options = [];
    foreach ((FruitSpecies species, int count) in game.Save.FruitsBasket)
      if (count > 0) options.Add($"{species} {count}", species);

    if (options.Count == 0)
    {
      // Used to do nothing whatsoever, which is indistinguishable from the game ignoring you.
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      Voice.Current.Say(string.Format(BebooText.ui_basket, 0));
      return;
    }

    if (options.Count == 1)
    {
      Feed(game, beboo, options.First().Value);
      return;
    }

    game.Ui.Choose<FruitSpecies>(BebooText.ui_chooseitem, options, chosen =>
    {
      if (chosen != FruitSpecies.None) Feed(game, beboo, chosen);
    });
  }

  private static void Feed(IGame game, Beboo beboo, FruitSpecies fruit)
  {
    if (game.Save.FruitsBasket is null) return;
    if (!game.Save.FruitsBasket.TryGetValue(fruit, out int held) || held <= 0) return;

    beboo.Eat(fruit);
    game.Save.FruitsBasket[fruit] = held - 1;
  }

  /// <summary>
  /// Opens the bag and takes something out of it, ready to be put down.
  ///
  /// Picking an item up puts it in the bag, and only taking it back out sets ItemInHand - which is
  /// what putting it down needs. On Windows that happened in the inventory menu; the phone had no
  /// such menu, so anything picked up there went into a bag with no way into it and looked to have
  /// vanished. Identical items are counted rather than listed one by one: three fruits in a row all
  /// reading "apple" tells you nothing about which to choose.
  /// </summary>
  public static void OpenBag(IGame game)
  {
    if (game.Inventory.Count == 0)
    {
      game.SoundSystem.System.PlaySound(game.SoundSystem.WarningSound);
      Voice.Current.Say(BebooText.ui_emptyinventory);
      return;
    }

    Dictionary<string, Item.Item> options = [];
    foreach (Item.Item item in game.Inventory)
    {
      int count = game.Inventory.Count(other => other.Name == item.Name);
      string label = count > 1 ? $"{item.Name} {count}" : item.Name;
      // First of each name wins; they are interchangeable.
      options.TryAdd(label, item);
    }

    game.Ui.Choose<Item.Item>(BebooText.ui_chooseitem, options, chosen =>
    {
      if (chosen is null) return;
      game.ItemInHand = chosen;
      game.SoundSystem.System.PlaySound(game.SoundSystem.MenuOkSound);
      Voice.Current.Say(chosen.Name);
    });
  }

  // --- Calling -------------------------------------------------------------

  /// <summary>Whistles. Some of the beboos wake and start heading for where the player stands.</summary>
  public static void Whistle(IGame game)
  {
    game.SoundSystem.System.Get3DListenerAttributes(0, out Vector3 here, out _, out _, out _);
    game.SoundSystem.Whistle();
    if (game.Map is null) return;

    // A copy: waking a beboo can move it between maps, and the list would change underneath.
    foreach (Beboo beboo in game.Map.Beboos.ToList())
    {
      if (game.Map.Beboos.Count > 1 && game.Random.Next(2) != 1) continue;
      // The delay is drawn here rather than inside the callback: Random is shared and not safe to
      // use from several threads at once.
      beboo.Later(game.Random.Next(1000, 2000), () => beboo.WakeUp());
      beboo.Destination = here;
    }
  }

  /// <summary>Calls one beboo by number, the way the digit keys do on a desktop.</summary>
  public static void CallBeboo(IGame game, int oneBased)
  {
    if (game.Map is null || oneBased < 1 || oneBased > game.Map.Beboos.Count) return;
    Beboo beboo = game.Map.Beboos[oneBased - 1];
    game.SoundSystem.Whistle(true, beboo.VoicePitch);
    Voice.Current.Say(beboo.Name);
    beboo.Call(game, EventArgs.Empty);
  }

  // --- Telling the player where they are -----------------------------------

  public static void SayTickets(IGame game)
      => Voice.Current.Say(string.Format(BebooText.tickets, game.Save.Tickets));

  public static void SayBasketState(IGame game)
  {
    if (game.Save.FruitsBasket is null) return;
    int fruits = game.Save.FruitsBasket.Values.Sum();
    Voice.Current.Say(string.Format(BebooText.ui_basket, fruits));
  }

  public static void SayBebooState(IGame game)
  {
    if (game.Map is null) return;
    if (game.Map.Beboos.Count == 0)
    {
      Voice.Current.Say(BebooText.nobeboo);
      return;
    }

    string all = "";
    foreach (Beboo beboo in game.Map.Beboos)
    {
      string sentence;
      if (beboo.Sleeping) sentence = BebooText.beboo_sleep;
      else if (beboo.Happiness < 0) sentence = BebooText.beboo_verysad;
      // Read the same flag the music follows, so the two can never disagree.
      else if (!beboo.Happy) sentence = BebooText.beboo_littlesad;
      else sentence = beboo.EnergyLevel switch
      {
        EnergyStage.Exhausted => BebooText.beboo_exhausted,
        EnergyStage.Tired => BebooText.beboo_tired,
        EnergyStage.LittleTired => BebooText.beboo_littletired,
        EnergyStage.Ok => BebooText.beboo_okenergy,
        _ => BebooText.beboo_energetic,
      };
      all += string.Format(sentence, beboo.Name) + "\n";
    }
    Voice.Current.Say(all);
  }
}
