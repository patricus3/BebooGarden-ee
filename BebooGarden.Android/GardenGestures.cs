using Android.OS;
using Android.Views;
using BebooGarden.GameCore;
using System;
using System.Numerics;

namespace BebooGarden.Droid;

/// <summary>What a gesture in the garden meant.</summary>
public enum GardenGesture
{
  WalkWest,
  WalkEast,
  WalkNorth,
  WalkSouth,
  /// <summary>Tap: pet what is here, shake a tree, or act on what is under you.</summary>
  Interact,
  /// <summary>Two-finger tap: pick a beboo up, or put down the one being carried.</summary>
  CarryBeboo,
  /// <summary>Three-finger tap: whistle, and the beboos come to you.</summary>
  Whistle,
  /// <summary>Long press: how are the beboos doing.</summary>
  BebooState,
  /// <summary>Two-finger long press: tickets and fruit basket.</summary>
  Inventory,
  /// <summary>Double tap: take what is on the ground, or use a gate or the shop.</summary>
  Use,
  /// <summary>Two-finger stroke up or down: pet the beboo here, or shake a tree.</summary>
  Pet,
  /// <summary>Two-finger stroke left: rock a carried beboo that way.</summary>
  RockLeft,
  /// <summary>Two-finger stroke right: rock a carried beboo that way.</summary>
  RockRight,
  /// <summary>Three-finger stroke down: say again where you are and what is here.</summary>
  WhereAmI,
  /// <summary>Three-finger press and hold: choose a beboo to call by name.</summary>
  CallByName,
  /// <summary>Back: open the menu.</summary>
  Menu,
  /// <summary>Two-finger double tap: offer the beboo here a fruit.</summary>
  Feed,
}

/// <summary>
/// Turns touches on the garden into things the player meant.
///
/// The garden is walked, not pointed at, so there is no on-screen anything: a flick in a direction
/// walks that way, and taps with one, two or three fingers are the verbs. A blind player has
/// nothing to aim at, which is exactly why a d-pad drawn in a corner is the one design that cannot
/// work here.
///
/// Flicks repeat if you hold at the end of one, so crossing the garden is a flick and a wait
/// rather than forty separate flicks. The repeat runs at the same
/// <see cref="PlayerActions.StepInterval"/> the desktop uses for a held arrow key, so the two
/// platforms walk at the same speed.
/// </summary>
internal sealed class GardenGestures
{
  private const float FlickThreshold = 55f;
  private const long LongPressMs = 550;

  private readonly Action<GardenGesture> _on;
  private readonly Handler _handler = new(Looper.MainLooper!);
  private Action? _pendingTap;
  private long _lastTapAt;
  private long _lastTwoFingerTapAt;

  private float _downX, _downY;
  private long _downAt;
  private int _maxFingers;
  private bool _flicked;
  private bool _longPressSent;

  /// <summary>The direction being held, if a flick ended without the finger lifting.</summary>
  internal Vector3? Held { get; private set; }

  internal GardenGestures(Action<GardenGesture> on) => _on = on;

  private void CancelPendingTap() => _pendingTap = null;

  internal bool OnTouch(MotionEvent e)
  {
    long now = Java.Lang.JavaSystem.CurrentTimeMillis();

    switch (e.ActionMasked)
    {
      case MotionEventActions.Down:
        _downX = e.GetX();
        _downY = e.GetY();
        _downAt = now;
        _maxFingers = 1;
        _flicked = false;
        _longPressSent = false;
        Held = null;
        return true;

      case MotionEventActions.PointerDown:
        // Remember the most fingers that were ever down, not how many are left at the end: people
        // do not lift three fingers at exactly the same instant.
        _maxFingers = Math.Max(_maxFingers, e.PointerCount);
        CancelPendingTap();
        return true;

      case MotionEventActions.Move:
      {
        if (_longPressSent) return true;

        float dx = e.GetX() - _downX;
        float dy = e.GetY() - _downY;

        // Two and three fingers stroke rather than walk. Petting a beboo is a stroke in real life
        // and it is a stroke here; rocking one is the same sideways motion the desktop asks for
        // with enter and alternating arrows.
        if (_maxFingers >= 2)
        {
          if (Math.Abs(dx) <= FlickThreshold && Math.Abs(dy) <= FlickThreshold) return true;

          bool sideways = Math.Abs(dx) > Math.Abs(dy);
          GardenGesture stroke = _maxFingers >= 3
              ? GardenGesture.WhereAmI
              : sideways
                  ? (dx > 0 ? GardenGesture.RockRight : GardenGesture.RockLeft)
                  : GardenGesture.Pet;

          _flicked = true;
          CancelPendingTap();
          _on(stroke);

          // Re-anchor, so stroking back and forth keeps petting rather than firing once.
          _downX = e.GetX();
          _downY = e.GetY();
          return true;
        }

        if (Math.Abs(dx) > FlickThreshold || Math.Abs(dy) > FlickThreshold)
        {
          // Whichever axis dominates, so a sloppy diagonal still goes where it was meant to.
          GardenGesture direction = Math.Abs(dx) > Math.Abs(dy)
              ? (dx > 0 ? GardenGesture.WalkEast : GardenGesture.WalkWest)
              : (dy > 0 ? GardenGesture.WalkSouth : GardenGesture.WalkNorth);

          if (!_flicked)
          {
            _flicked = true;
            CancelPendingTap();
            _on(direction);
          }

          // Keep walking while the finger stays out here.
          Held = direction switch
          {
            GardenGesture.WalkWest => PlayerActions.West,
            GardenGesture.WalkEast => PlayerActions.East,
            GardenGesture.WalkNorth => PlayerActions.North,
            _ => PlayerActions.South,
          };

          // Re-anchor, so a long drag is a walk rather than one step and then nothing.
          _downX = e.GetX();
          _downY = e.GetY();
          return true;
        }

        // Held still and long enough, with nothing having been flicked: a long press.
        if (!_flicked && now - _downAt > LongPressMs)
        {
          _longPressSent = true;
          _on(GardenGesture.BebooState);
        }
        return true;
      }

      case MotionEventActions.Up:
      case MotionEventActions.Cancel:
      {
        Vector3? wasHeld = Held;
        Held = null;

        if (e.ActionMasked == MotionEventActions.Cancel || _flicked || wasHeld != null) return true;

        bool longPress = now - _downAt > LongPressMs;

        if (longPress)
        {
          if (!_longPressSent)
            _on(_maxFingers switch
            {
              >= 3 => GardenGesture.CallByName,
              2 => GardenGesture.Inventory,
              _ => GardenGesture.BebooState,
            });
          return true;
        }

        if (_maxFingers >= 3)
        {
          // Three fingers is unambiguous, so it fires at once.
          _on(GardenGesture.Whistle);
          return true;
        }

        if (_maxFingers == 2)
        {
          // Two fingers carries two meanings, so it waits out the double-tap window like one
          // finger does: once picks the beboo up, twice offers it a fruit.
          if (now - _lastTwoFingerTapAt <= ViewConfiguration.DoubleTapTimeout)
          {
            CancelPendingTap();
            _lastTwoFingerTapAt = 0;
            _on(GardenGesture.Feed);
            return true;
          }

          _lastTwoFingerTapAt = now;
          _pendingTap = () => _on(GardenGesture.CarryBeboo);
          Action carry = _pendingTap;
          _handler.PostDelayed(() =>
          {
              if (!ReferenceEquals(_pendingTap, carry)) return;
              _pendingTap = null;
              carry();
          }, ViewConfiguration.DoubleTapTimeout);
          return true;
        }

        // One finger is ambiguous until the double-tap window has passed: one tap means interact,
        // two means use what is here. Holding the first back by that window is the price of
        // telling them apart, and matches the double-tap-to-choose the menus already use.
        if (now - _lastTapAt <= ViewConfiguration.DoubleTapTimeout)
        {
          CancelPendingTap();
          _lastTapAt = 0;
          _on(GardenGesture.Use);
          return true;
        }

        _lastTapAt = now;
        _pendingTap = () => _on(GardenGesture.Interact);
        Action fire = _pendingTap;
        _handler.PostDelayed(() =>
        {
            if (!ReferenceEquals(_pendingTap, fire)) return;
            _pendingTap = null;
            fire();
        }, ViewConfiguration.DoubleTapTimeout);
        return true;
      }
    }

    return false;
  }
}
