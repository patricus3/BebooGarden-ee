using BebooGarden.Content;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using BebooGarden.Minigame;
using BebooGarden.Save;
using BebooGarden.UI;
using CrossSpeak;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Myra.Graphics2D.UI;
using SharpHook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace BebooGarden;

public partial class Game1
{
  public KeyboardState _previousKeyboardState;
  private MouseState _previousMouseState;

  public SaveParameters Save { get; private set; }

  private readonly object _keyLock = new();
  private readonly HashSet<Keys> _hookPressedKeys = [];
  private readonly HashSet<Keys> _keysToProcess = []; // Nouvelles touches à traiter
  private bool _updateProcessed = false;
  private bool _lastSwayWasLeft;
  private bool _wasActive = true;

  /// <summary>How much one press of a volume key moves it.</summary>
  private const float VOLUMESTEP = 0.05f;

  private void HandleKeyboardNavigation(KeyboardState currentKeyboardState)
  {
    // Before anything else, and outside every other check: the volume keys work in the garden, in
    // a menu and in the middle of a minigame alike, which is where you most want them.
    HandleVolumeKeys(currentKeyboardState);
    // A minigame owns the keyboard for as long as it runs, so the menus do not act on the same
    // keys behind it - escape in particular, which now leaves the minigame.
    if (CurrentPlayingMiniGame?.IsRunning ?? false) return;
    if (_currentScreen == GameScreen.game && !_paused)
    {
      MainGameKeyboardLogic(currentKeyboardState);
    }
    else if (_currentScreen == GameScreen.TalkDialog)
    {
      if (IsKeyPressed(currentKeyboardState, Keys.Escape, Keys.Space, Keys.Enter))
      {
        _talkDialog?.Next();
      }
      else if (currentKeyboardState.GetPressedKeyCount() > 0)
      {
        _talkDialog?.DisplayCurrentLine();
      }
    }
    else
    {
      HandleMenuNavigation(currentKeyboardState);
    }
  }

  /// <summary>
  /// F2 and F3 for everything, F5 and F6 for the music, F4 to silence the music. These are the
  /// keys the manual has always listed; they were dropped when the input was rewritten and had
  /// been doing nothing since.
  /// </summary>
  private void HandleVolumeKeys(KeyboardState currentKeyboardState)
  {
    if (IsKeyPressed(currentKeyboardState, Keys.F2)) SetVolume(SoundSystem.Volume - VOLUMESTEP);
    if (IsKeyPressed(currentKeyboardState, Keys.F3)) SetVolume(SoundSystem.Volume + VOLUMESTEP);
    if (IsKeyPressed(currentKeyboardState, Keys.F5)) SetMusicVolume(SoundSystem.MusicVolume - VOLUMESTEP);
    if (IsKeyPressed(currentKeyboardState, Keys.F6)) SetMusicVolume(SoundSystem.MusicVolume + VOLUMESTEP);
    // Alt F4 closes the window and is dealt with below. It must not also mute the music on its
    // way out, and it must not mute it when the window refuses to close either.
    if (IsKeyPressed(currentKeyboardState, Keys.F4)
        && !currentKeyboardState.IsKeyDown(Keys.LeftAlt)
        && !currentKeyboardState.IsKeyDown(Keys.RightAlt))
    {
      SoundSystem.MusicMuted = !SoundSystem.MusicMuted;
      CrossSpeakManager.Instance.Output(
          SoundSystem.MusicMuted ? BebooText.ui_musicmuted : BebooText.ui_musicunmuted);
    }
  }

  private void SetVolume(float volume)
  {
    SoundSystem.Volume = volume;
    // The memory brings its own sound system, and is the only thing you can hear while it runs.
    CurrentPlayingMiniGame?.SetVolume(SoundSystem.Volume);
    // A bip after the change, so you hear the level you have just set rather than only hear it
    // named. The music needs none: the music is the thing being turned down.
    SoundSystem.System.PlaySound(SoundSystem.MenuBipSound);
    CrossSpeakManager.Instance.Output(
        String.Format(BebooText.ui_volume, (int)Math.Round(SoundSystem.Volume * 100)));
  }

  private void SetMusicVolume(float volume)
  {
    SoundSystem.MusicVolume = volume;
    CrossSpeakManager.Instance.Output(
        String.Format(BebooText.ui_musicvolume, (int)Math.Round(SoundSystem.MusicVolume * 100)));
  }

  private void MainGameKeyboardLogic(KeyboardState currentKeyboardState)
  {
    Item? itemUnderCursor = Map?.GetItemArroundPosition(PlayerPosition);
    Map?.IsInWater(PlayerPosition);
    if ((DateTime.Now - LastPressedKeyTime).TotalMilliseconds > 150)
    {
      if (currentKeyboardState.IsKeyDown(Keys.Left)
      || Wasd && currentKeyboardState.IsKeyDown(Keys.A)
      || !Wasd && currentKeyboardState.IsKeyDown(Keys.Q))
      {
        if (BebooInArms != null && currentKeyboardState.IsKeyDown(Keys.Enter))
        {
          if (!_lastSwayWasLeft)
          {
            SwayBebooInArms(true);
            _lastSwayWasLeft = true;
          }
        }
        else
        {
          MoveOf(new Vector3(-1, 0, 0));
        }
      }
      else if (currentKeyboardState.IsKeyDown(Keys.Right) || currentKeyboardState.IsKeyDown(Keys.D))
      {
        if (BebooInArms != null && currentKeyboardState.IsKeyDown(Keys.Enter))
        {
          if (_lastSwayWasLeft)
          {
            SwayBebooInArms(false);
            _lastSwayWasLeft = false;
          }
        }
        else
        {
          MoveOf(new Vector3(1, 0, 0));
        }
      }
      else if (currentKeyboardState.IsKeyDown(Keys.Up)
        || Wasd && IsKeyPressed(currentKeyboardState, Keys.W)
        || !Wasd && currentKeyboardState.IsKeyDown(Keys.Z))
      {
        if (currentKeyboardState.IsKeyDown(Keys.Enter))
        {
          if (!_lastArrowWasUp)
          {
            ShakeOrPetAtPlayerPosition();
            _lastArrowWasUp = true;
          }
        }
        else
        {
          MoveOf(new Vector3(0, 1, 0));
        }
      }
      else if (currentKeyboardState.IsKeyDown(Keys.Down)
        || Wasd && currentKeyboardState.IsKeyDown(Keys.S)
        || !Wasd && currentKeyboardState.IsKeyDown(Keys.S))
      {
        if (currentKeyboardState.IsKeyDown(Keys.Enter))
        {
          if (_lastArrowWasUp)
          {
            ShakeOrPetAtPlayerPosition();
            _lastArrowWasUp = false;
          }
        }
        else
        {
          MoveOf(new Vector3(0, -1, 0));
        }
      }
      LastPressedKeyTime = DateTime.Now;
    }
    if (IsKeyPressed(currentKeyboardState, Keys.F))
    {
      SayBebooState();
    }
    if (IsKeyPressed(currentKeyboardState, Keys.G))
    {
      SayBasketState();
    }
    if (IsKeyPressed(currentKeyboardState, Keys.T))
    {
      SayTickets();
    }
    if (IsKeyPressed(currentKeyboardState, Keys.P))
    {
      TakeOrPutDownBeboo();
    }
    if (IsKeyPressed(currentKeyboardState, Keys.Enter))
    {
      // Shared with the phone's double tap; see GameCore.PlayerActions.UseHere.
      GameCore.PlayerActions.UseHere(this);
    }
    if (currentKeyboardState.GetPressedKeyCount() > 0)
    {
      var key = currentKeyboardState.GetPressedKeys()[0];
      if (MonoUtil.IsKeyDigit(key, out int keyInt) && keyInt > 0)
      {
        if (Map != null && keyInt <= Map.Beboos.Count)
        {
          var sortedBeboos = new List<Beboo>(Map.Beboos);
          sortedBeboos.Sort(delegate (Beboo x, Beboo y) { return x.Age.CompareTo(y.Age); });
          var beboo = Map.Beboos[keyInt - 1];
          SoundSystem.Whistle(true, beboo.VoicePitch);
          CrossSpeakManager.Instance.Output(beboo.Name);
          beboo.Call(this, new EventArgs());
        }
      }
    }
    if (IsKeyPressed(currentKeyboardState, Keys.Escape) && !EscapeJustPressed)
    {
      SwitchToScreen(GameScreen.MainMenu);
    }
    if (IsKeyPressed(currentKeyboardState, Keys.Space))
    {
      // Shared with the phone's tap; see GameCore.PlayerActions.Interact.
      GameCore.PlayerActions.Interact(this);
    }
    if (IsKeyPressed(currentKeyboardState, Keys.F4) && IsKeyPressed(currentKeyboardState, Keys.LeftAlt, Keys.RightAlt))
    {
      Exit();
    }
  }

  private void HandleMenuNavigation(KeyboardState currentKeyboardState)
  {
    Widget focused = _desktop.FocusedKeyboardWidget;
    if (IsKeyPressed(currentKeyboardState, Keys.Down) || IsKeyPressed(currentKeyboardState, Keys.Right))
    {
      _desktop.FocusNext();
    }

    if (IsKeyPressed(currentKeyboardState, Keys.Up) || IsKeyPressed(currentKeyboardState, Keys.Left))
    {
      _desktop.FocusPrevious();
    }
    if (IsKeyPressed(currentKeyboardState, Keys.Enter) || IsKeyPressed(currentKeyboardState, Keys.Space))
    {
      // CheckButton is not a Button in Myra, so a checkbox needs saying separately.
      if (focused is CheckButton checkButton) checkButton.IsPressed = !checkButton.IsPressed;
      else if (focused is Button button) button.DoClick();
    }
    // The mod list shown before the garden is not a menu you can back out of; it is the way in.
    // The same list opened from the main menu is an ordinary menu and closes like one.
    if (IsKeyPressed(currentKeyboardState, Keys.Escape) && !EscapeJustPressed
        && !(_currentScreen == GameScreen.ModMenu && UI.ModMenu.BlocksEscape))
    {
      _aMenuShouldBeClosed = true;
    }
  }

  public bool IsKeyPressed(KeyboardState currentKeyboardState, params Keys[] keys)
  {
    foreach (Keys key in keys)
    {
      if (currentKeyboardState.IsKeyDown(key) && !_previousKeyboardState.IsKeyDown(key))
      {
        return true;
      }
    }
    return false;
  }

  private void OnKeyPressed(object sender, KeyboardHookEventArgs e)
  {
    if (!IsActive) return;
    Keys monogameKey = MonoUtil.ConvertKeyCodeToMonogameKey(e.Data.KeyCode);

    if (monogameKey != Keys.None)
    {
      lock (_keyLock)
      {
        _hookPressedKeys.Add(monogameKey);
        _keysToProcess.Add(monogameKey); // Ajouter aux touches à traiter
        _updateProcessed = false; // Réinitialiser le flag
      }
    }
  }

  private void OnKeyReleased(object sender, KeyboardHookEventArgs e)
  {
    // Deliberately not gated on IsActive: a key let go of while the window is in the background
    // still has to stop being held here, or it stays down forever.
    Keys monogameKey = MonoUtil.ConvertKeyCodeToMonogameKey(e.Data.KeyCode);

    if (monogameKey != Keys.None)
    {
      lock (_keyLock)
      {
        _hookPressedKeys.Remove(monogameKey);
        // Do not withdraw from _KeStoprocess - These keys must be treated at least once      }
      }
    }
  }

}
