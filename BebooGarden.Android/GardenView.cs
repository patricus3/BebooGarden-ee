using Android.Content;
using Android.Views;
using BebooGarden.Content;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using BebooGarden.GameCore.World;
using System;
using System.Numerics;

namespace BebooGarden.Droid;

/// <summary>
/// The garden itself, as a thing you touch.
///
/// Draws nothing - there has never been anything to draw. It exists to catch gestures and turn
/// them into the same <see cref="PlayerActions"/> the desktop's arrow keys call, so a beboo picked
/// up on a phone is picked up by exactly the code that picks one up on Windows.
/// </summary>
internal sealed class GardenView : View
{
  private readonly GardenGestures _gestures;

  internal GardenView(Context context) : base(context)
  {
    Focusable = true;
    FocusableInTouchMode = true;
    _gestures = new GardenGestures(Perform);
    ContentDescription = Tips();
  }

  /// <summary>The direction being held, for the tick to keep walking. Null when standing still.</summary>
  internal Vector3? HeldDirection => _gestures.Held;

  public override bool OnTouchEvent(MotionEvent? e)
      => e is not null && _gestures.OnTouch(e) || base.OnTouchEvent(e);

  /// <summary>
  /// How to play, in this platform's own terms. The shipped tips talk about arrow keys and the
  /// space bar, which is no use whatsoever on a phone.
  /// </summary>
  internal static string Tips() =>
      "Beboo Garden. " +
      "Flick up, down, left or right to walk, and hold at the end of a flick to keep going. " +
      "Tap to pet or use what is where you stand. " +
      "Double tap to pick something up, or to go through a gate. " +
      "Two finger tap to pick a beboo up or put it down. " +
      "Three finger tap to whistle and call your beboos. " +
      "Press and hold to hear how your beboos are. " +
      "Two finger press and hold for your tickets and fruit. " +
      "Pause TalkBack, by holding both volume keys, to play with gestures.";

  private void Perform(GardenGesture gesture)
  {
    IGame game = GameHost.Current;
    if (game.Map is null) return;

    switch (gesture)
    {
      case GardenGesture.WalkWest: PlayerActions.MoveOf(game, PlayerActions.West); break;
      case GardenGesture.WalkEast: PlayerActions.MoveOf(game, PlayerActions.East); break;
      case GardenGesture.WalkNorth: PlayerActions.MoveOf(game, PlayerActions.North); break;
      case GardenGesture.WalkSouth: PlayerActions.MoveOf(game, PlayerActions.South); break;

      case GardenGesture.CarryBeboo: PlayerActions.TakeOrPutDownBeboo(game); break;
      case GardenGesture.Whistle: PlayerActions.Whistle(game); break;
      case GardenGesture.BebooState: PlayerActions.SayBebooState(game); break;

      case GardenGesture.Inventory:
        PlayerActions.SayTickets(game);
        PlayerActions.SayBasketState(game);
        break;

      case GardenGesture.Interact: Interact(game); break;
      case GardenGesture.Use: Use(game); break;
    }
  }

  /// <summary>
  /// What the space bar does on a desktop: put down what is held, pet or feed a beboo, act on
  /// whatever is underfoot, and whistle when there is nothing else to do.
  /// </summary>
  private static void Interact(IGame game)
  {
    if (game.ItemInHand != null)
    {
      PlayerActions.TryPutItemInHand(game);
      return;
    }

    Beboo? beboo = PlayerActions.BebooUnderCursor(game);
    if (beboo != null)
    {
      // A sleeping beboo is woken rather than fed; feeding needs it awake to eat.
      if (beboo.Sleeping) PlayerActions.Whistle(game);
      else PlayerActions.ShakeOrPetAtPlayerPosition(game);
      return;
    }

    if (game.Map?.GetTreeLineAtPosition(game.PlayerPosition) != null)
    {
      PlayerActions.ShakeOrPetAtPlayerPosition(game);
      return;
    }

    Item? here = game.Map?.GetItemArroundPosition(game.PlayerPosition);
    if (here != null) here.Action();
    else PlayerActions.Whistle(game);
  }

  /// <summary>
  /// What enter does on a desktop: take what is lying here, or go through whatever is here.
  /// The shop and the race gate are not built on this head yet and say so rather than going quiet.
  /// </summary>
  private static void Use(IGame game)
  {
    if (game.Map is null) return;

    Item? takable = game.BebooInArms is null
        ? game.Map.GetTakableItemArroundPosition(game.PlayerPosition)
        : null;
    if (takable != null)
    {
      PlayerActions.TakeFromGround(game, takable);
      return;
    }

    MapConnexion? connexion = game.Map.GetConnexionArroundPosition(game.PlayerPosition);
    if (connexion?.Map.IsUnlocked() ?? false)
    {
      PlayerActions.TravelThrough(game, connexion);
      return;
    }

    if (game.Save.Flags.UnlockShop && game.Map.IsArroundShop(game.PlayerPosition))
    {
      Voice.Current.Say(BebooText.shop);
      return;
    }

    if (game.Map.IsArroundRaceGate(game.PlayerPosition))
      Voice.Current.Say(BebooText.competition_closed);
  }
}
