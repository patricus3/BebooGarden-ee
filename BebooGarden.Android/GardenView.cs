using Android.Content;
using Android.Views;
using BebooGarden.Content;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using BebooGarden.GameCore.World;
using System;
using System.Collections.Generic;
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
  /// A keyboard, for anyone who has one paired. Arrows walk, enter uses, space interacts - the
  /// same keys as the desktop, so the two do not have to be learned separately.
  /// </summary>
  public override bool DispatchKeyEvent(KeyEvent? e)
  {
    if (e is null || e.Action != KeyEventActions.Down) return base.DispatchKeyEvent(e);

    GardenGesture? gesture = e.KeyCode switch
    {
      Keycode.DpadLeft => GardenGesture.WalkWest,
      Keycode.DpadRight => GardenGesture.WalkEast,
      Keycode.DpadUp => GardenGesture.WalkNorth,
      Keycode.DpadDown => GardenGesture.WalkSouth,
      Keycode.Space => GardenGesture.Interact,
      Keycode.Enter or Keycode.DpadCenter => GardenGesture.Use,
      Keycode.P => GardenGesture.CarryBeboo,
      Keycode.W => GardenGesture.Whistle,
      Keycode.F => GardenGesture.BebooState,
      Keycode.T or Keycode.G => GardenGesture.Inventory,
      Keycode.C => GardenGesture.CallByName,
      Keycode.E => GardenGesture.Feed,
      Keycode.B => GardenGesture.Bag,
      _ => null,
    };

    if (gesture is null) return base.DispatchKeyEvent(e);
    Perform(gesture.Value);
    return true;
  }

  /// <summary>
  /// How to play, in this platform's own terms. The shipped tips talk about arrow keys and the
  /// space bar, which is no use whatsoever on a phone.
  /// </summary>
  internal static string Tips() =>
      "Beboo Garden. " +
      "Flick up, down, left or right to walk, and hold at the end of a flick to keep going. " +
      "Tap to pet or use what is where you stand. " +
      "Double tap to pick something up, or to go through a gate. " +
      "Stroke up or down with two fingers to pet your beboo, or to shake a tree. " +
      "Stroke left and right with two fingers to rock the beboo you are carrying. " +
      "Two finger tap picks a beboo up, and puts it down. " +
      "Two finger double tap offers it a fruit. " +
      "Three finger double tap opens your bag; tap to put down what you take out. " +
      "Three finger tap whistles and calls your beboos. " +
      "Stroke with three fingers to hear again where you are. " +
      "Press and hold to hear how your beboos are. " +
      "Two finger press and hold for your tickets and fruit. " +
      "Three finger press and hold to call one beboo by name. " +
      "Pause TalkBack, by holding both volume keys, to play with gestures.";

  /// <summary>
  /// Hands the gesture to the game thread. Never runs it here: this is Android's main thread, and
  /// a good deal of the game blocks until a sound has finished playing.
  /// </summary>
  private void Perform(GardenGesture gesture)
  {
    if (GameHost.Current is AndroidGame game) game.Post(() => Act(game, gesture));
  }

  private static void Act(IGame game, GardenGesture gesture)
  {
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

      case GardenGesture.Interact: PlayerActions.Interact(game); break;
      case GardenGesture.Use: PlayerActions.UseHere(game); break;

      case GardenGesture.Pet: PlayerActions.ShakeOrPetAtPlayerPosition(game); break;
      case GardenGesture.Feed: PlayerActions.FeedBeboo(game); break;
      case GardenGesture.Bag: PlayerActions.OpenBag(game); break;
      case GardenGesture.RockLeft: PlayerActions.SwayBebooInArms(game, true); break;
      case GardenGesture.RockRight: PlayerActions.SwayBebooInArms(game, false); break;

      case GardenGesture.WhereAmI: PlayerActions.SpeakObjectUnderCursor(game); break;
      case GardenGesture.CallByName: PlayerActions.CallBebooByName(game); break;
      case GardenGesture.Menu: Voice.Current.Say(Tips(), interrupt: true); break;
    }
  }

}
