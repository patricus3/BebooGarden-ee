using BebooGarden.Content;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.World;
using CrossSpeak;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Myra.Graphics2D.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BebooGarden.UI.ScriptedScene;

/// <summary>
/// The questions a new player answers before the garden opens. Every panel is built at the moment
/// it is shown rather than up front, so the whole scene follows the language picked in the first
/// step.
/// </summary>
public class WelcomeScene : IScriptedScene
{
  private enum Step
  {
    Language,
    Welcome,
    AskName,
    WaitName,
    AboutYou,
    AskColor,
    WaitColor,
    AskFreeTime,
    WaitFreeTime,
    AskDessert,
    WaitDessert,
    AllGood,
    Garden,
    Finished
  }

  private readonly Panel _welcomePanel = new();
  private Step _step = Step.Language;
  private Step _stepAfterTalk;
  private TalkDialog? _talk;

  private string _yourName = string.Empty;
  private string? _favoredColor;
  private string _freeTime = string.Empty;
  private string? _dessert;

  public void Show()
  {
    Game1.Instance.SwitchToScreen(GameScreen.ScriptedScene);
    Game1.Instance._scriptedScene = this;
    Game1.Instance.SoundSystem.PlayNWelcomeMusic();
    Game1.Instance.ShowLanguageMenu(_welcomePanel, false, () => _step = Step.Welcome);
  }

  public void Update(GameTime gameTime)
  {
    // A talk dialog runs on its own screen; let it finish before touching anything.
    if (_talk != null)
    {
      if (!_talk.Closed) return;
      _talk = null;
      _step = _stepAfterTalk;
    }
    if (Game1.Instance._currentScreen != GameScreen.ScriptedScene) return;
    switch (_step)
    {
      case Step.Welcome:
        Talk(BebooText.ui_welcome, Step.AskName);
        break;
      case Step.AskName:
        AskText(BebooText.ui_yourname, new FancyTextField(12, true), OnNameTyped);
        _step = Step.WaitName;
        break;
      case Step.AboutYou:
        Talk(String.Format(BebooText.ui_aboutyou, _yourName), Step.AskColor);
        break;
      case Step.AskColor:
        AskChoice(BebooText.ui_color, Util.Colors.ToDictionary(Util.LocalizedColor), OnColorPicked);
        _step = Step.WaitColor;
        break;
      case Step.AskFreeTime:
        AskText(BebooText.ui_freetime, new FancyTextField(200), OnFreeTimeTyped);
        _step = Step.WaitFreeTime;
        break;
      case Step.AskDessert:
        AskChoice(BebooText.ui_dessert, Desserts(), OnDessertPicked);
        _step = Step.WaitDessert;
        break;
      case Step.AllGood:
        Talk(BebooText.ui_allgood, Step.Garden);
        break;
      case Step.Garden:
        // Wants the player's name; unformatted it reads "{0}" out loud.
        Talk(String.Format(BebooText.ui_welcome2, _yourName), Step.Finished);
        break;
      case Step.Finished:
        Finish();
        break;
    }
  }

  private static Dictionary<string, string> Desserts() => new()
  {
    { BebooText.chocolatekake, "chocolatekake" },
    { BebooText.icecream, "icecream" },
    { BebooText.fruitsalad, "fruitsalad" },
    { BebooText.coffee, "coffee" },
  };

  private void OnNameTyped(string name)
  {
    if (name.Length == 0)
    {
      // The name is used everywhere afterwards, so keep asking for it.
      Game1.Instance.SoundSystem.System.PlaySound(Game1.Instance.SoundSystem.WarningSound);
      CrossSpeakManager.Instance.Output(BebooText.ui_empty);
      _step = Step.AskName;
      return;
    }
    _yourName = name;
    _step = Step.AboutYou;
  }

  private void OnColorPicked(string color)
  {
    _favoredColor = color;
    _step = Step.AskFreeTime;
  }

  private void OnFreeTimeTyped(string freeTime)
  {
    _freeTime = freeTime;
    _step = Step.AskDessert;
  }

  private void OnDessertPicked(string dessert)
  {
    _dessert = dessert;
    _step = Step.AllGood;
  }

  private void Talk(string text, Step next)
  {
    _stepAfterTalk = next;
    _talk = new TalkDialog(text, GameScreen.ScriptedScene);
    _talk.Show();
  }

  /// <summary>
  /// A question with a text field, as a plain panel rather than a Myra dialog: a dialog answers
  /// Enter by closing itself, which left the scene with nothing on screen and no way forward.
  /// </summary>
  private void AskText(string question, FancyTextField field, Action<string> onAnswered)
  {
    Panel panel = new();
    VerticalStackPanel grid = new()
    {
      Spacing = 15,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center
    };
    grid.Widgets.Add(new Label { Text = question, HorizontalAlignment = HorizontalAlignment.Center });
    grid.Widgets.Add(field);
    panel.Widgets.Add(grid);
    field.KeyDown += (sender, args) =>
    {
      if ((Keys)args.Data != Keys.Enter) return;
      Game1.Instance.SoundSystem.System.PlaySound(Game1.Instance.SoundSystem.MenuOkSound);
      onAnswered(((FancyTextField)sender).Text ?? string.Empty);
    };
    Game1.Instance._desktop.Root = panel;
    Game1.Instance._desktop.FocusedKeyboardWidget = field;
    CrossSpeakManager.Instance.Output(question);
  }

  /// <param name="options">Spoken label mapped to the value that gets stored.</param>
  private void AskChoice(string question, Dictionary<string, string> options, Action<string> onAnswered)
  {
    Panel panel = new();
    VerticalStackPanel grid = new()
    {
      Spacing = 15,
      HorizontalAlignment = HorizontalAlignment.Center,
      VerticalAlignment = VerticalAlignment.Center
    };
    grid.Widgets.Add(new Label { Text = question, HorizontalAlignment = HorizontalAlignment.Center });
    Widget? firstButton = null;
    foreach (var option in options)
    {
      ConfirmButton button = new(option.Key) { Id = $"choice_{option.Value}" };
      var value = option.Value;
      button.Click += (_, _) => onAnswered(value);
      grid.Widgets.Add(button);
      firstButton ??= button;
    }
    panel.Widgets.Add(grid);
    Game1.Instance._desktop.Root = panel;
    if (firstButton != null) Game1.Instance._desktop.FocusedKeyboardWidget = firstButton;
    CrossSpeakManager.Instance.Output(question);
  }

  private void Finish()
  {
    // What the answers mean is decided in Core, so the phone reaches exactly the same garden from
    // its own version of this sequence. This scene's job ends at collecting them.
    Game1.Instance._scriptedScene = null;
    GameCore.GameStart.FinishWelcome(Game1.Instance, new GameCore.WelcomeAnswers
    {
      PlayerName = _yourName,
      FavoredColor = _favoredColor,
      FreeTime = _freeTime,
      Dessert = _dessert,
    });
  }

  /// <summary>
  /// The starting egg is placed before the player is asked anything, so swap it for one of the
  /// color they just chose.
  /// </summary>
  private void PutEggOfFavoredColor()
  {
    Map? map = Game1.Instance.Map;
    if (map == null) return;
    foreach (Egg egg in map.Items.OfType<Egg>().ToList())
    {
      egg.SoundLoopBehaviour.Stop();
      if (egg.Channel != null && egg.Channel.IsPlaying) egg.Channel.Stop();
      map.Items.Remove(egg);
    }
    map.AddItem(new Egg(Game1.Instance.Save.FavoredColor), new System.Numerics.Vector3(2, 0, 0));
  }
}
