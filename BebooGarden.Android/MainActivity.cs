using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using Android.Runtime;
using Android.Views;
using BebooGarden.GameCore;
using BebooGarden.GameCore.Input;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.Speech;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace BebooGarden.Droid;

[Activity(
    Label = "Beboo Garden",
    MainLauncher = true,
    Exported = true,
    // The garden is not a thing you look at, so let it keep running through a rotation rather than
    // tearing the whole world down and rebuilding it.
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
    ScreenOrientation = ScreenOrientation.Sensor)]
public sealed class MainActivity : Activity, AudioManager.IOnAudioFocusChangeListener
{
  /// <summary>
  /// How fast the world ticks. Nothing is drawn, and the beboos time themselves off the wall clock
  /// rather than off frames, so 20 Hz costs a third of the battery of 60 and behaves identically.
  /// </summary>
  private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(50);

  /// <summary>
  /// Time away counts at a tenth of its real length here. A phone game is opened for a minute and
  /// closed again all day long; at full rate a pocket beboo would live permanently at the sad floor.
  /// </summary>
  private const double PhoneIdleRate = 0.1;

  private AndroidGame? _game;
  private AndroidSpeech? _speech;
  private BebooNotifier? _notifier;
  private TouchInputFrame? _input;
  private CancellationTokenSource? _ticking;
  private AudioFocusRequestClass? _focusRequest;

  protected override void OnCreate(Bundle? savedInstanceState)
  {
    base.OnCreate(savedInstanceState);

    // First, before anything at all can throw. A crash that happens on the way up is exactly the
    // one a player most needs to be able to send you, and until this runs the log would be written
    // into private storage where nobody can get at it.
    AndroidStorage.PrepareCrashLog(this);
    AppDomain.CurrentDomain.UnhandledException +=
        (_, e) => Record("unhandled", e.ExceptionObject as Exception);
    // Beboo behaviour runs plenty of delayed work on the thread pool; a throw in one of those is
    // otherwise swallowed entirely. Same reasoning as Program.cs on Windows.
    TaskScheduler.UnobservedTaskException +=
        (_, e) => Record("background task", e.Exception);

    // Nothing is drawn, but a view is still needed to receive touches and to be what TalkBack
    // focuses when it is running.
    var surface = new GardenView(this);
    SetContentView(surface);
    surface.RequestFocus();

    _speech = new AndroidSpeech(this);
    Voice.Use(_speech);

    // Android 13+ will not show a notification without this. Asked for here rather than at the
    // moment one is due, because that moment is when the app is being closed and no dialog can
    // be shown. Refusing it costs only the notification; the garden is unaffected.
    if (OperatingSystem.IsAndroidVersionAtLeast(33) &&
        CheckSelfPermission(Android.Manifest.Permission.PostNotifications) != Permission.Granted)
    {
      RequestPermissions([Android.Manifest.Permission.PostNotifications], 1);
    }

    _notifier = new BebooNotifier(this);
    // Whatever was pending is no longer true: you are here.
    _notifier.CancelPending();

    OfflineProgress.IdleRate = PhoneIdleRate;

    _input = new TouchInputFrame();
    surface.Actions = _input;

    Task.Run(() => Boot(surface));
  }

  /// <summary>
  /// Unpacks the content, points FMOD at its native library and starts the garden. Off the UI
  /// thread: the first run copies several hundred megabytes out of the package.
  /// </summary>
  private void Boot(GardenView surface)
  {
    try
    {
      Voice.Current.Say("Setting up your garden. This happens once.");
      AndroidStorage.Prepare(this);

      // FMOD's Android build is half native and half Java, and the Java half has to be given the
      // Context before any system can be created - that is how it reaches the audio device and the
      // asset manager. Without this, System_Create returns ERR_INTERNAL and says nothing else.
      // The native side needs no configuring: the bindings P/Invoke against "fmod" and Android
      // resolves that to libfmod.so out of the apk's own native library directory.
      FmodJava.Init(this);

      _game = new AndroidGame(new AndroidGameUi(() => this));
      GameHost.Use(_game);
      _game.Start();

      RequestAudioFocus();
      StartTicking();

      // Opening the garden is shared with the desktop: it starts the player's mods and then either
      // runs the opening sequence or drops them into the garden they already had. Saying a welcome
      // line here instead, which is what this used to do, announced something and then left the
      // player in a world that had never actually been started.
      RunOnUiThread(() => GameStart.Begin(_game));
    }
    catch (Exception error)
    {
      // A crash here leaves a black screen and no explanation, which on an audio game is
      // indistinguishable from the app simply not working.
      Record("startup", error);
      RunOnUiThread(() => Voice.Current.Say(
          "Beboo Garden could not start. The details were written to the crash log.", interrupt: true));
    }
  }

  /// <summary>
  /// Appends a crash to the log the player can reach. Never throws: this is what runs when things
  /// have already gone wrong.
  /// </summary>
  private static void Record(string origin, Exception? error)
  {
    if (error == null) return;
    try
    {
      File.AppendAllText(GamePaths.CrashLog,
          $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {origin}{System.Environment.NewLine}{error}{System.Environment.NewLine}{System.Environment.NewLine}");
    }
    catch (Exception)
    {
      // Nothing sensible left to do if even writing the log fails.
    }
  }

  private void StartTicking()
  {
    _ticking?.Cancel();
    _ticking = new CancellationTokenSource();
    CancellationToken token = _ticking.Token;

    Task.Run(async () =>
    {
      while (!token.IsCancellationRequested)
      {
        try
        {
          _input?.BeginTick();
          _game?.Update();
          _game?.CurrentPlayingMiniGame?.Update(_input!);
        }
        catch (Exception error)
        {
          Record("tick", error);
        }
        await Task.Delay(TickInterval, token).ConfigureAwait(false);
      }
    }, token);
  }

  // --- Lifecycle -----------------------------------------------------------

  protected override void OnPause()
  {
    base.OnPause();
    // Going away: stop the world, write it down, and work out whether to say anything later.
    _ticking?.Cancel();
    _game?.Pause();
    _speech?.Stop();

    try
    {
      _game?.WriteSave();
      ScheduleNotification();
    }
    catch (Exception error)
    {
      Record("closing down", error);
    }

    AbandonAudioFocus();
  }

  protected override void OnResume()
  {
    base.OnResume();
    if (_game is null) return;

    _notifier?.CancelPending();
    RequestAudioFocus();
    _game.Unpause();
    StartTicking();
  }

  protected override void OnDestroy()
  {
    _ticking?.Cancel();
    _speech?.Shutdown();
    // Hands back what Init took. Paired with the call in Boot.
    try { FmodJava.Close(); } catch (Exception error) { Record("fmod shutdown", error); }
    base.OnDestroy();
  }

  /// <summary>
  /// Schedules the single notification for this absence, if the beboos would actually want one.
  /// The happiest beboo decides: if any of them is content, there is nothing to report.
  /// </summary>
  private void ScheduleNotification()
  {
    if (_game?.Map is null || _notifier is null) return;

    Beboo[] beboos = [.. _game.Map.Beboos.Where(b => !b.Racer)];
    if (beboos.Length == 0) return;

    Beboo happiest = beboos.MaxBy(b => b.Happiness)!;
    DateTime? when = BebooNotifier.NextNotificationTime(
        happiest.Happiness, DateTime.Now, lastNotified: null, DateTime.Now);

    if (when is DateTime at) _notifier.Schedule(at, happiest.Name);
  }

  // --- Audio focus ---------------------------------------------------------

  private void RequestAudioFocus()
  {
    var audio = GetSystemService(AudioService) as AudioManager;
    if (audio is null) return;

    AudioAttributes? attributes = new AudioAttributes.Builder()
        .SetUsage(AudioUsageKind.Game)!
        .SetContentType(AudioContentType.Music)!
        .Build();
    if (attributes is null) return;

    _focusRequest = new AudioFocusRequestClass.Builder(AudioFocus.Gain)
        .SetAudioAttributes(attributes)!
        .SetOnAudioFocusChangeListener(this)!
        .Build();
    if (_focusRequest is null) return;

    audio.RequestAudioFocus(_focusRequest);
  }

  private void AbandonAudioFocus()
  {
    var audio = GetSystemService(AudioService) as AudioManager;
    if (audio is not null && _focusRequest is not null)
      audio.AbandonAudioFocusRequest(_focusRequest);
  }

  /// <summary>
  /// A call, an alarm or a podcast wants the speakers. The garden is nothing but sound, so ducking
  /// is pointless - it goes quiet and comes back.
  /// </summary>
  public void OnAudioFocusChange(AudioFocus focusChange)
  {
    if (_game is null) return;
    switch (focusChange)
    {
      case AudioFocus.Loss:
      case AudioFocus.LossTransient:
      case AudioFocus.LossTransientCanDuck:
        _game.Pause();
        _speech?.Stop();
        break;
      case AudioFocus.Gain:
        _game.Unpause();
        break;
    }
  }
}

/// <summary>
/// The gesture surface. Draws nothing - it exists to catch touches and to be something TalkBack can
/// focus.
///
/// Walking uses a relative anchor rather than an on-screen d-pad: wherever the first finger lands
/// becomes the origin, and dragging from it is a direction. A blind player has nothing to aim at,
/// so a fixed control in a fixed corner is the one thing that cannot work.
/// </summary>
public sealed class GardenView : View
{
  private const float DeadZonePx = 48f;

  private float _anchorX, _anchorY;
  private bool _anchored;

  public TouchInputFrame? Actions { get; set; }

  public GardenView(Context context) : base(context)
  {
    Focusable = true;
    FocusableInTouchMode = true;
    ContentDescription =
        "Beboo Garden. Drag to walk, tap with two fingers to interact. " +
        "Pause TalkBack by holding both volume keys for three seconds to play with gestures.";
  }

  public override bool OnTouchEvent(MotionEvent? e)
  {
    if (e is null || Actions is null) return false;

    switch (e.ActionMasked)
    {
      case MotionEventActions.Down:
        _anchorX = e.GetX();
        _anchorY = e.GetY();
        _anchored = true;
        return true;

      case MotionEventActions.Move when _anchored:
        float dx = e.GetX() - _anchorX;
        float dy = e.GetY() - _anchorY;
        if (dx * dx + dy * dy < DeadZonePx * DeadZonePx) return true;
        // Whichever axis dominates wins, so a rough drag still goes where it was meant to.
        Actions.Raise(Math.Abs(dx) > Math.Abs(dy)
            ? (dx > 0 ? GameAction.Slot2 : GameAction.Slot1)
            : (dy > 0 ? GameAction.Slot4 : GameAction.Slot3));
        return true;

      case MotionEventActions.PointerDown when e.PointerCount >= 2:
        Actions.Raise(GameAction.Back);
        return true;

      case MotionEventActions.Up:
      case MotionEventActions.Cancel:
        _anchored = false;
        return true;
    }

    return base.OnTouchEvent(e);
  }
}
