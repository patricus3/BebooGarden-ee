using BebooGarden.Content;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using BebooGarden.Interface.UI;
using BebooGarden.Minigame;
using BebooGarden.MiniGames;
using BebooGarden.Save;
using CrossSpeak;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BebooGarden;

public partial class Game1
{
  private Beboo? _contester;

  /// <summary>The beboo currently carried by the player, if any.</summary>
  public Beboo? BebooInArms { get; set; }

  private void TakeOrPutDownBeboo() => GameCore.PlayerActions.TakeOrPutDownBeboo(this);

  private void SwayBebooInArms(bool toLeft) => GameCore.PlayerActions.SwayBebooInArms(this, toLeft);

  /// <summary>Lets go of the carried beboo when something else took it away, a race for instance.</summary>
  private void ReleaseBebooInArmsIfGone()
  {
    if (BebooInArms == null) return;
    if (Map != null && !Map.IsRaceMap && Map.Beboos.Contains(BebooInArms) && !BebooInArms.Racer) return;
    BebooInArms.PutDown(false);
    BebooInArms = null;
  }

  private void SayBebooState() => GameCore.PlayerActions.SayBebooState(this);

  private void FeedBeboo()
  {
    Beboo? bebooUnderCursor = BebooUnderCursor();
    if (Save.FruitsBasket == null || bebooUnderCursor == null) return;
    Dictionary<string, FruitSpecies> options = [];
    foreach (KeyValuePair<FruitSpecies, int> fruit in Save.FruitsBasket)
    {
      if (fruit.Value > 0) options.Add(fruit.Key.ToString() + " " + fruit.Value.ToString(), fruit.Key);
    }
    if (options.Count == 1)
    {
      OnFruitSelected(options.First().Value);
    }
    else if (options.Count > 0)
    {
      new ChooseMenu<FruitSpecies>(BebooText.ui_chooseitem, options, OnFruitSelected)
       .Show();
    }
  }

  private void OnFruitSelected(FruitSpecies choice)
  {
    if (choice != FruitSpecies.None)
    {
      Beboo? bebooUnderCursor = BebooUnderCursor();
      bebooUnderCursor?.Eat(choice);
      Save.FruitsBasket[choice]--;
    }
  }

  private CompetitionType _competitionType = CompetitionType.None;

  /// <summary>
  /// The competition centre's front desk: pick a contest, then who competes, then any options that
  /// contest has. Every contest spends from the same daily allowance.
  /// </summary>
  /// <summary>
  /// The race gate. Choosing an event, a beboo and a track is shared with the Android head; see
  /// GameCore.Competitions.
  /// </summary>
  public void ShowCompetitionMenu() => GameCore.Competitions.ShowMenu(this);

  public void ChooseBebooForCompetition() => GameCore.Competitions.ShowMenu(this);

  private void StartMiniGame(IMiniGame miniGame)
  {
    if (Competition.IsRunning || CurrentPlayingMiniGame != null) return;
    CurrentPlayingMiniGame = miniGame;
    CurrentPlayingMiniGame.Start();
  }
}
