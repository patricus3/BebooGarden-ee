using Android.App;
using Android.Content;
using Android.Text;
using Android.Widget;
using BebooGarden.Content;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using BebooGarden.MiniGames;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BebooGarden.Droid;

/// <summary>
/// The phone's <see cref="IGameUi"/>.
///
/// Where the Windows head draws these with Myra widgets that exist only to carry screen-reader
/// focus, this uses real Android dialogs. That is the point of doing it natively: TalkBack already
/// knows how to read an AlertDialog and its list, in the player's own voice, at their own rate, on
/// their braille display, and none of that had to be built again.
///
/// The wording is the shared BebooText, so every translation the desktop has, the phone has.
/// </summary>
public sealed class AndroidGameUi : IGameUi
{
  private readonly Func<Activity?> _activity;

  public AndroidGameUi(Func<Activity?> activity) => _activity = activity;

  /// <summary>The opening sequence. See <see cref="AndroidWelcome"/>.</summary>
  public void ShowWelcome()
  {
    Activity? activity = _activity();
    if (activity is null) return;
    new AndroidWelcome(activity).Run();
  }

  /// <summary>
  /// A beboo has hatched: say which colour came out of the shell - the one moment the player is
  /// told - and then ask for a name, exactly as NewBebooScene does.
  /// </summary>
  public void ShowNewBeboo(Beboo beboo)
  {
    string colorLine = "";
    string? color = Util.ColorOfBebooType(beboo.BebooType);
    if (color != null)
      colorLine = string.Format(BebooText.ui_hatchcolor, Util.LocalizedColor(color)) + " ";

    Voice.Current.Say(colorLine + BebooText.ui_letsname, interrupt: true);
    AskForName(beboo);
  }

  private void AskForName(Beboo beboo)
  {
    Activity? activity = _activity();
    if (activity is null) return;

    activity.RunOnUiThread(() =>
    {
      var input = new EditText(activity)
      {
        Hint = BebooText.ui_bebooname,
        ContentDescription = BebooText.ui_bebooname,
      };
      input.SetSingleLine(true);
      input.SetFilters([new InputFilterLengthFilter(12)]);

      new AlertDialog.Builder(activity)
          .SetTitle(BebooText.ui_bebooname)!
          .SetView(input)!
          .SetPositiveButton(Android.Resource.String.Ok, (_, _) =>
          {
            string name = input.Text?.Trim() ?? "";
            if (name.Length == 0) return;
            beboo.Name = name;
            if (GameHost.Current.Save.Flags.NewGame)
            {
              Voice.Current.Say(string.Format(BebooText.ui_quicktips, name));
              GameHost.Current.Save.Flags.NewGame = false;
            }
          })!
          .SetCancelable(false)!
          .Show();
    });
  }

  /// <summary>
  /// The podium. Spoken rather than drawn - on Windows this is a panel whose text only the screen
  /// reader ever reads, so there is nothing to lose by saying it directly.
  /// </summary>
  public void ShowRaceResult(
      (int, double) third,
      (int, double) second,
      (int, double) first,
      Beboo mainBeboo,
      RaceType raceType)
  {
    var beboos = GameHost.Current.Map?.Beboos;
    string NameAt(int index) =>
        beboos is not null && index >= 0 && index < beboos.Count ? beboos[index].Name : mainBeboo.Name;

    if (third.Item1 >= 0)
      Voice.Current.Say(string.Format(BebooText.race_third, NameAt(third.Item1)), interrupt: true);
    if (second.Item1 >= 0)
      Voice.Current.Say(string.Format(BebooText.race_second, NameAt(second.Item1), second.Item2));
    if (first.Item1 >= 0)
      Voice.Current.Say(string.Format(BebooText.race_first, NameAt(first.Item1), first.Item2));
  }

  public void Choose<T>(
      string title,
      Dictionary<string, T> choices,
      Action<T?> onChosen,
      bool allowCancel = true)
  {
    Activity? activity = _activity();
    if (activity is null)
    {
      onChosen(default);
      return;
    }

    string[] labels = [.. choices.Keys];
    T[] values = [.. choices.Values];

    // Dialogs must be built on the UI thread; the game logic asking for one is on the tick.
    activity.RunOnUiThread(() =>
    {
      var builder = new AlertDialog.Builder(activity);
      builder.SetTitle(title);
      builder.SetItems(labels, (_, args) => onChosen(values[args.Which]));
      if (allowCancel)
      {
        builder.SetNegativeButton(Android.Resource.String.Cancel, (_, _) => onChosen(default));
        builder.SetOnCancelListener(new CancelListener(() => onChosen(default)));
      }
      builder.SetCancelable(allowCancel);
      builder.Show();
    });
  }

  private sealed class CancelListener(Action onCancel)
      : Java.Lang.Object, IDialogInterfaceOnCancelListener
  {
    public void OnCancel(IDialogInterface? dialog) => onCancel();
  }
}
