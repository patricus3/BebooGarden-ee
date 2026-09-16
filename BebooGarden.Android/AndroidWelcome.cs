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
/// the game speaks for itself through AudioMenu - flick to move, double tap to take - rather than
/// drawing Myra widgets that exist only to hold a screen reader's focus. Typing a name still uses
/// a real input field, because you cannot flick a name into existence.
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

  /// <summary>Speaks a line and waits for a double tap. Nothing to look at: the line is the screen.</summary>
  private void Say(string line, Action next) => AudioMenu.Say(_activity, line, () => OnGameThread(next));

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
        // ui.welcome2 is "Right, {0}, here is your garden..." - it wants the player's name, and
        // without the format it reads the placeholder out loud.
        Say(BebooText.ui_allgood,
            () => Say(string.Format(Controls.Welcome2, _answers.PlayerName), Finish));
      });

  /// <summary>
  /// Every answer arrives on the UI thread, and what the game does next plays music or a cinematic
  /// - which blocks until the sound ends. That is an ANR if it happens on Android's main thread.
  /// </summary>
  private static void OnGameThread(Action work)
  {
    if (GameHost.Current is AndroidGame game) game.Post(work);
    else work();
  }

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
              (_, _) =>
              {
                string typed = input.Text?.Trim() ?? string.Empty;
                OnGameThread(() => onAnswered(typed));
              })!
          .SetCancelable(false)!
          .Show();
    });
  }

  private void AskChoice(string question, Dictionary<string, string> options, Action<string> onAnswered)
  {
    string[] labels = [.. options.Keys];
    string[] values = [.. options.Values];
    AudioMenu.Show(_activity, question, labels, i => OnGameThread(() => onAnswered(values[i])));
  }
}
