using Android.App;
using Android.Text;
using Android.Widget;
using BebooGarden.Content;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Speech;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BebooGarden.Droid;

/// <summary>
/// The phone's opening sequence for a new game.
///
/// Same questions, same order and the same BebooText as the desktop scene, so every translation
/// carries over and a player who knows one knows the other. What differs is only how they are put:
/// native dialogs that TalkBack reads, rather than Myra widgets that exist to hold focus.
///
/// The language step is left out. On the desktop the game asks because it has no other way of
/// knowing; a phone already has a language the player chose for the whole device, and .NET picks
/// it up as the current culture, so asking again would be asking a question we have the answer to.
///
/// Every step ends by calling the next one, because Android dialogs return immediately and the
/// answer arrives on a callback. The last one hands everything to GameStart.FinishWelcome, which
/// is the same code the desktop ends up in.
/// </summary>
internal sealed class AndroidWelcome
{
  private readonly Activity _activity;
  private readonly WelcomeAnswers _answers = new();

  internal AndroidWelcome(Activity activity) => _activity = activity;

  internal void Run()
  {
    GameHost.Current.SoundSystem.PlayNWelcomeMusic();
    Say(BebooText.ui_welcome, () => AskName());
  }

  /// <summary>Speaks a line, then moves on. Nothing to click: the line is the screen.</summary>
  private void Say(string line, Action next)
  {
    Voice.Current.Say(line, interrupt: true);
    _activity.RunOnUiThread(() =>
        new AlertDialog.Builder(_activity)
            .SetMessage(line)!
            .SetPositiveButton(Android.Resource.String.Ok, (_, _) => next())!
            .SetCancelable(false)!
            .Show());
  }

  private void AskName()
      => AskText(BebooText.ui_yourname, 12, name =>
      {
        if (name.Length == 0)
        {
          // The name is used everywhere afterwards, so keep asking. Same rule as the desktop.
          GameHost.Current.SoundSystem.System.PlaySound(GameHost.Current.SoundSystem.WarningSound);
          Voice.Current.Say(BebooText.ui_empty, interrupt: true);
          AskName();
          return;
        }
        _answers.PlayerName = name;
        Say(string.Format(BebooText.ui_aboutyou, name), AskColor);
      });

  private void AskColor()
      => AskChoice(BebooText.ui_color, Util.Colors.ToDictionary(Util.LocalizedColor), color =>
      {
        _answers.FavoredColor = color;
        AskFreeTime();
      });

  private void AskFreeTime()
      => AskText(BebooText.ui_freetime, 200, answer =>
      {
        _answers.FreeTime = answer;
        AskDessert();
      });

  private void AskDessert()
      => AskChoice(BebooText.ui_dessert, Desserts(), dessert =>
      {
        _answers.Dessert = dessert;
        Say(BebooText.ui_allgood, () => Say(BebooText.ui_welcome2, Finish));
      });

  private static Dictionary<string, string> Desserts() => new()
  {
    { BebooText.chocolatekake, "chocolatekake" },
    { BebooText.icecream, "icecream" },
    { BebooText.fruitsalad, "fruitsalad" },
    { BebooText.coffee, "coffee" },
  };

  private void Finish() => GameStart.FinishWelcome(GameHost.Current, _answers);

  private void AskText(string question, int maxLength, Action<string> onAnswered)
  {
    Voice.Current.Say(question, interrupt: true);
    _activity.RunOnUiThread(() =>
    {
      var input = new EditText(_activity) { Hint = question, ContentDescription = question };
      input.SetSingleLine(true);
      input.SetFilters([new InputFilterLengthFilter(maxLength)]);

      new AlertDialog.Builder(_activity)
          .SetTitle(question)!
          .SetView(input)!
          .SetPositiveButton(Android.Resource.String.Ok,
              (_, _) => onAnswered(input.Text?.Trim() ?? string.Empty))!
          .SetCancelable(false)!
          .Show();
    });
  }

  private void AskChoice(string question, Dictionary<string, string> options, Action<string> onAnswered)
  {
    Voice.Current.Say(question, interrupt: true);
    string[] labels = [.. options.Keys];
    string[] values = [.. options.Values];

    _activity.RunOnUiThread(() =>
        new AlertDialog.Builder(_activity)
            .SetTitle(question)!
            .SetItems(labels, (_, args) => onAnswered(values[args.Which]))!
            .SetCancelable(false)!
            .Show());
  }
}
