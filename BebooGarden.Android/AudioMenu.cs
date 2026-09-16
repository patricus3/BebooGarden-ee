using Android.App;
using Android.Content;
using Android.Views;
using BebooGarden.GameCore.Speech;
using System;
using System.Collections.Generic;

namespace BebooGarden.Droid;

/// <summary>
/// A menu you listen to rather than look at.
///
/// Flick in any direction to move through the options and each one is spoken; double tap to take
/// the one you are on. Nothing is drawn, because there is nothing worth drawing - the options are
/// the sound of them.
///
/// This replaces the native AlertDialogs the first version used. Those were the right call only if
/// TalkBack is running: they are properly labelled and TalkBack reads them, but that means the
/// player must have TalkBack on, must swipe past the dialog's furniture to reach the list, and
/// hears everything in TalkBack's voice rather than the game's. Speaking for itself, the game
/// needs none of that, which is how an audio game ought to behave - and it is then the same
/// gesture whether TalkBack is paused or not.
///
/// It does mean TalkBack must be PAUSED to use it, since TalkBack's touch exploration swallows
/// every gesture before an app sees it and Android has no per-view opt-out the way iOS does.
/// Holding both volume keys for three seconds is the usual way, and it is a gesture blind Android
/// users already know.
///
/// Text entry deliberately still uses a real input field: you cannot flick a name into existence,
/// and the keyboard is the player's own, already set up how they like it.
/// </summary>
internal sealed class AudioMenu : View
{
  /// <summary>How far a flick must travel before it counts, in pixels.</summary>
  private const float FlickThreshold = 60f;

  private readonly string _title;
  private readonly IReadOnlyList<string> _options;
  private readonly Action<int> _onChosen;
  private readonly Action? _onCancelled;
  private readonly ViewGroup _host;

  private int _index;
  private bool _finished;
  private float _downX, _downY;
  private long _lastTapAt;

  private AudioMenu(
      Context context,
      ViewGroup host,
      string title,
      IReadOnlyList<string> options,
      Action<int> onChosen,
      Action? onCancelled)
      : base(context)
  {
    _host = host;
    _title = title;
    _options = options;
    _onChosen = onChosen;
    _onCancelled = onCancelled;

    Focusable = true;
    FocusableInTouchMode = true;
    // The game speaks for itself here, so TalkBack should not also try to describe an empty view.
    ImportantForAccessibility = ImportantForAccessibility.NoHideDescendants;
  }

  /// <summary>
  /// Puts a menu up over the game and reads the first option. <paramref name="onChosen"/> gets the
  /// index that was taken; the menu is gone by then.
  /// </summary>
  internal static void Show(
      Activity activity,
      string title,
      IReadOnlyList<string> options,
      Action<int> onChosen,
      Action? onCancelled = null)
  {
    activity.RunOnUiThread(() =>
    {
      var host = (ViewGroup)activity.Window!.DecorView.FindViewById(Android.Resource.Id.Content)!;
      var menu = new AudioMenu(activity, host, title, options, onChosen, onCancelled);
      host.AddView(menu, new ViewGroup.LayoutParams(
          ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
      menu.BringToFront();
      menu.RequestFocus();
      menu.Announce(withTitle: true);
    });
  }

  /// <summary>
  /// A line to listen to with nothing to choose. Double tap moves on. Used for the talking parts
  /// of the opening sequence, where the desktop shows a talk dialog.
  /// </summary>
  internal static void Say(Activity activity, string line, Action onContinue)
      => Show(activity, line, [], _ => onContinue());

  private void Announce(bool withTitle)
  {
    string spoken = withTitle ? _title : string.Empty;

    if (_options.Count > 0)
    {
      if (spoken.Length > 0) spoken += ". ";
      spoken += _options[_index];
      // Where you are in the list, so it is possible to tell a long list from a short one.
      if (_options.Count > 1) spoken += $" ({_index + 1} of {_options.Count})";
      spoken += ". Double tap to choose.";
    }
    else if (withTitle)
    {
      spoken += " Double tap to continue.";
    }

    Voice.Current.Say(spoken, interrupt: true);
  }

  private void Move(int by)
  {
    if (_options.Count == 0) return;
    // Wraps, so flicking past the end comes round rather than stopping dead with no feedback.
    _index = (_index + by + _options.Count) % _options.Count;
    Announce(withTitle: false);
  }

  private void Choose()
  {
    if (_finished) return;
    _finished = true;
    Dismiss();
    _onChosen(_options.Count == 0 ? -1 : _index);
  }

  private void Dismiss() => _host.RemoveView(this);

  public override bool OnTouchEvent(MotionEvent? e)
  {
    if (e is null || _finished) return false;

    switch (e.ActionMasked)
    {
      case MotionEventActions.Down:
        _downX = e.GetX();
        _downY = e.GetY();
        return true;

      case MotionEventActions.Up:
        float dx = e.GetX() - _downX;
        float dy = e.GetY() - _downY;

        if (Math.Abs(dx) > FlickThreshold || Math.Abs(dy) > FlickThreshold)
        {
          // Whichever axis dominates, so a sloppy diagonal still goes where it was meant to.
          // Right and down are forwards, matching the way every screen reader moves through a list.
          Move(Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? 1 : -1) : (dy > 0 ? 1 : -1));
          _lastTapAt = 0;
          return true;
        }

        long now = Java.Lang.JavaSystem.CurrentTimeMillis();
        if (now - _lastTapAt <= ViewConfiguration.DoubleTapTimeout)
        {
          _lastTapAt = 0;
          Choose();
        }
        else
        {
          _lastTapAt = now;
          // A single tap repeats where you are, for when the line was missed or talked over.
          Announce(withTitle: _options.Count == 0);
        }
        return true;
    }

    return base.OnTouchEvent(e);
  }

  public override bool DispatchKeyEvent(KeyEvent? e)
  {
    if (e is null || _finished || e.Action != KeyEventActions.Down) return base.DispatchKeyEvent(e);

    // A Bluetooth keyboard costs nothing to support and plenty of blind Android users carry one.
    switch (e.KeyCode)
    {
      case Keycode.DpadDown or Keycode.DpadRight: Move(1); return true;
      case Keycode.DpadUp or Keycode.DpadLeft: Move(-1); return true;
      case Keycode.DpadCenter or Keycode.Enter or Keycode.Space: Choose(); return true;
      case Keycode.Back or Keycode.Escape:
        if (_onCancelled is null) return true;
        _finished = true;
        Dismiss();
        _onCancelled();
        return true;
    }

    return base.DispatchKeyEvent(e);
  }
}
