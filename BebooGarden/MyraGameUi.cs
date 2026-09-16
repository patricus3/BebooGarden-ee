using BebooGarden.GameCore;
using BebooGarden.GameCore.Pet;
using BebooGarden.Interface.UI;
using BebooGarden.MiniGames;
using BebooGarden.UI.ScriptedScene;
using System;
using System.Collections.Generic;

namespace BebooGarden;

/// <summary>
/// The Windows head's <see cref="IGameUi"/>: the existing Myra scenes and menus, unchanged. This
/// file is the only thing that knows the shared code's three requests are drawn with widgets.
/// </summary>
internal sealed class MyraGameUi : IGameUi
{
  public void ShowWelcome() => new WelcomeScene().Show();

  public void ShowNewBeboo(Beboo beboo) => new NewBebooScene(beboo).Show();

  public void ShowRaceResult(
      (int, double) third,
      (int, double) second,
      (int, double) first,
      Beboo mainBeboo,
      RaceType raceType)
      => new RaceResult(third, second, first, mainBeboo, raceType).Show();

  public void Choose<T>(
      string title,
      Dictionary<string, T> choices,
      Action<T?> onChosen,
      bool allowCancel = true)
      => new ChooseMenu<T>(title, choices, onChosen, allowCancel).Show();
}
