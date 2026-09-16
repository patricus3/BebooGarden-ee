using BebooGarden.Content;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using BebooGarden.MiniGames;
using BebooGarden.Save;
using BebooGarden.UI;
using BebooGarden.UI.ScriptedScene;
using CrossSpeak;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BebooGarden;

public partial class Game1
{
  private bool _firstScreenTipsSayed = false;

  public IScriptedScene? _scriptedScene;
  public KeyboardState _currentKeyboardState;
  private bool _paused;

  public bool EscapeJustPressed { get; set; }

  protected override void Update(GameTime gameTime)
  {
    GetKeyStates(out _currentKeyboardState, out MouseState currentMouseState);
    BeginGardenIfNeeded();
    _desktop.UpdateInput();
    CloseMenuIfNeeded();
    foreach (GameCore.Pet.Beboo beboo in Map?.Beboos)
    {
      beboo.Update();
      if (Map.IsLullabyPlaying) beboo.GoAsleep();
      else if (Map.IsDansePlaying) beboo.WakeUp();
      if (!Map.IsRaceMap && beboo.Racer) beboo.Pause();
      if (beboo.Age >= 2 && !Save.Flags.VoiceRecoPopupPrinted)
      {
        Save.Flags.VoiceRecoPopupPrinted = true;
        beboo.KnowItsName = true;
      }
      else if (beboo.Age >= 3 && !Save.Flags.UnlockSnowyMap)
      {
        Save.Flags.UnlockSnowyMap = true;
        SoundSystem.System.PlaySound(SoundSystem.JingleComplete);
        _talkDialog = new TalkDialog(BebooText.unlocksnowy);
        _talkDialog?.Show();
        Map.Snowy.AddItem(new Egg("none"), new(0, 0, 0));
        Save.Flags.UnlockEggInShop = true;
      }
      if (beboo.Happiness >= 8 && !Save.Flags.UnlockFluffMap)
      {
        Save.Flags.UnlockFluffMap = true;
        SoundSystem.System.PlaySound(SoundSystem.JingleComplete);
        _talkDialog = new TalkDialog(String.Format(BebooText.unlockfluff, beboo.Name));
        _talkDialog?.Show();
        // A second beboo, on the house, for anyone who gets one happy enough to earn the trip.
        Map.Fluff.AddItem(new Egg("none"), new(0, 0, 0));
      }
      if (beboo.SwimLevel >= 10 && !Save.Flags.UnlockPerfectSwimming)
      {
        Save.Flags.UnlockPerfectSwimming = true;
        SoundSystem.System.PlaySound(SoundSystem.JingleComplete);
        _talkDialog = new TalkDialog(String.Format(BebooText.unlockswimming, beboo.Name));
        _talkDialog?.Show();
        Save.Flags.UnlockUnderwaterMap = true;
        Map.UnderWater.AddItem(new Egg("blue"), new(0, 0, 0));
      }
    }
    foreach (var map in Map.Maps.Values.ToList())
    {
      map?.Update();
    }
    foreach (Item item in Map?.Items.ToList())
    {
      item?.Update();
    }

    ReleaseBebooInArmsIfGone();
    HandleKeyboardNavigation(_currentKeyboardState);
    UpdateMinigames(gameTime, _currentKeyboardState);
    UpdateScriptedScene(gameTime);
    UpdateUIState();
    SetPreviousKeyboardStates(_currentKeyboardState, currentMouseState);
    SoundSystem.UpdateWaterPoints(Map, PlayerPosition);
    SoundSystem.System.Update();
    // A crash or a power cut should cost a couple of minutes, not the whole session.
    BebooGarden.Save.AutoSave.Tick(this);
    base.Update(gameTime);
  }

  private void CloseMenuIfNeeded()
  {
    if (_aMenuShouldBeClosed)
    {
      SoundSystem.System.PlaySound(SoundSystem.MenuBackSound);
      _aMenuShouldBeClosed = false;
      if (_desktop.Root is not Panel root)
      {
        SwitchToScreen(GameScreen.game);
        return;
      }
      if (PreviousPanels.TryGetValue(root, out var previousPanal))
      {
        if (previousPanal == _gamePanel)
        {
          SwitchToScreen(GameScreen.game);
        }
        else
        {
          _desktop.Root = previousPanal;
        }
      }
      else if (_desktop.Root == _mainShopPanel)
      {
        CloseShop();
      }
      else if (_desktop.Root == _itemsSopPanel || _desktop.Root == _rollsShopPanel)
      {
        ShowMainShopMenu();
      }
      else
      {
        SwitchToScreen(GameScreen.game);
      }
    }
  }

  private void UpdateScriptedScene(GameTime gameTime)
  {
    _scriptedScene?.Update(gameTime);
  }

  private void SetPreviousKeyboardStates(KeyboardState currentKeyboardState, MouseState currentMouseState)
  {
    _previousKeyboardState = currentKeyboardState;
    _previousMouseState = currentMouseState;
    lock (_keyLock)
    {
      _updateProcessed = true;
      _keysToProcess.Clear(); // Vider les touches à traiter puisqu'elles ont été traitées
    }
  }

  private void UpdateMinigames(GameTime gameTime, KeyboardState currentKeyboardState)
  {
    if (CurrentPlayingMiniGame?.IsRunning ?? false)
    {
      CurrentPlayingMiniGame?.Update(new KeyboardInputFrame(this, currentKeyboardState));
    }
  }

  private void GetKeyStates(out KeyboardState currentKeyboardState, out MouseState currentMouseState)
  {
    currentMouseState = Mouse.GetState();
    if (!IsActive)
    {
      // Nothing typed in somebody else's window is meant for the garden.
      lock (_keyLock)
      {
        _hookPressedKeys.Clear();
        _keysToProcess.Clear();
        _updateProcessed = true;
      }
      currentKeyboardState = new KeyboardState();
      _wasActive = false;
      return;
    }
    KeyboardState nativeKeyboardState = Keyboard.GetState();
    List<Keys> allPressedKeys = nativeKeyboardState.GetPressedKeys().ToList();
    lock (_keyLock)
    {
      allPressedKeys.AddRange(_hookPressedKeys);
      if (!_updateProcessed)
      {
        allPressedKeys.AddRange(_keysToProcess);
        allPressedKeys = allPressedKeys.Distinct().ToList();
      }
    }
    if (EscapeJustPressed)
    {
      allPressedKeys.RemoveAll(key => key == Keys.Escape);
      EscapeJustPressed = false;
    }
    currentKeyboardState = new(allPressedKeys.ToArray());
    if (!_wasActive)
    {
      // First frame back in focus: treat whatever is still held as already seen, so alt-tabbing
      // home does not fire the action that key is bound to.
      _previousKeyboardState = currentKeyboardState;
      _wasActive = true;
    }
  }
  public void RemoveEscapeKey()
  {
    List<Keys> allPressedKeys = _currentKeyboardState.GetPressedKeys().ToList();
    allPressedKeys.RemoveAll(key => key == Keys.Escape);
    _currentKeyboardState = new(allPressedKeys.ToArray());
  }
  public void Pause()
  {
    _paused = true;
    foreach (Beboo beboo in Map?.Beboos) beboo.Pause();
    SoundSystem.DisableAmbiTimer();
    if (Map == null) return;
    SoundSystem.Pause(Map);
    SoundSystem.Music?.Paused = true;
  }
  public void Unpause()
  {
    _paused = false;
    foreach (Beboo beboo in Map?.Beboos) beboo.Unpause();
    SoundSystem.EnableAmbiTimer();
    if (Map == null) return;
    SoundSystem.Unpause(Map);
    SoundSystem.Music.Paused = false;
  }

}
