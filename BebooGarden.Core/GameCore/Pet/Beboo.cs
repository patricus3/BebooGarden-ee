using BebooGarden.GameCore;
using BebooGarden.GameCore.Speech;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BebooGarden.Content;
using BebooGarden.GameCore.World;
using BebooGarden.Audio;


namespace BebooGarden.GameCore.Pet;

public partial class Beboo
{
  /// <summary>
  /// How long a beboo goes before losing a point of energy. Slow enough that a well fed beboo can
  /// stay lively through a play session rather than sliding into exhausted on its own.
  /// </summary>
  private const int GOINGTIREDMINMS = 60000 * 6;
  private const int GOINGTIREDMAXMS = 60000 * 11;

  /// <summary>A beboo takes itself to bed at this share of its energy, and sleeps until that one.</summary>
  public const float SLEEPYAT = 0.25f;
  // Above the Energetic threshold rather than exactly on it, so a full night reads as energetic.
  public const float RESTEDAT = 0.85f;

  /// <summary>
  /// Share of its maximum a beboo recovers each sleeping tick. Ticks run every 5 to 10 seconds, so
  /// this fills an empty beboo of any size in roughly six minutes.
  /// </summary>
  public const float RECOVERYPERTICK = 0.016f;

  private DateTime _lastPetted = DateTime.MinValue;

  private int _petCount;
  private float voicePitch = 1.1f;
  private float age = 1;
  public bool BootsSlippedOn { get; set; } = false;
  public bool RubberRingSlippedOn { get; set; } = false;
  /// <summary>
  /// What this beboo is saying now, if anything. One voice at a time: a new one replaces it.
  /// </summary>
  public Channel? Channel { get; set; }

  /// <summary>
  /// The noises a beboo makes that are not its voice - footsteps, rustling, the breathing it does
  /// while asleep. Kept apart from <see cref="Channel"/> because they used to share it: every
  /// footstep quietly overwrote the handle on whatever the beboo was saying, so the next chirp
  /// stopped the footstep instead of the previous chirp, and the chirps piled up on each other.
  /// </summary>
  public Channel? EffectChannel { get; set; }
  public float VoicePitch
  {
    get => voicePitch; set
    {
      VoiceDsp.SetParameterFloat(0, value);
      voicePitch = value;
    }
  }
  public Dsp VoiceDsp { get; }
  public int SwimLevel { get; set; } = 0;
  public bool Racer { get; set; } = false;
  public BebooType BebooType { get; set; }

  /// <summary>
  /// Id of the mod creature this beboo is, when it came from a mod. Null for the built in types.
  /// </summary>
  public string? ModCreature { get; set; }

  /// <summary>
  /// Which voice folder this beboo speaks with: a mod creature's id when it has one, otherwise the
  /// name of its built in type.
  /// </summary>
  public string VoiceId => ModCreature ?? BebooType.ToString();
  public Beboo(string name, BebooType bebooType, float age, DateTime lastPlayed, int happiness = 3, float energy = 3, int swimLevel = 0, bool racer = false, float voicePitch = 1, Trait? trait = null)
  {
    // Before the behaviours below: some of their timings are drawn from it.
    Trait = trait ?? RandomTrait();
    Racer = racer;
    Position = new Vector3(0, 0, 0);
    Name = name == string.Empty ? "boby" : name;
    BebooType = bebooType;
    VoiceDsp = GameHost.Current.SoundSystem.System.CreateDSPByType(DspType.PitchShift);
    VoiceDsp.SetParameterFloat(0, voicePitch);
    SwimLevel = swimLevel;
    bool isSleepingAtStart = !racer && (DateTime.Now.Hour < 8 || DateTime.Now.Hour > 22);
    Sleeping = isSleepingAtStart;
    CuteBehaviour =
      new TimedBehaviour((int)(10000 * ChatterRate), (int)(35000 * ChatterRate), !isSleepingAtStart);
    MoveBehaviour =
        new TimedBehaviour(200, 400, !isSleepingAtStart);
    GoToSleepOrWakeUpBehaviour =
        new TimedBehaviour(10000, 150000, true);
    FancyMoveBehaviour =
        new TimedBehaviour((int)(10000 * RestlessRate), (int)(20000 * RestlessRate), true);
    GoingTiredBehaviour =
        new TimedBehaviour(GOINGTIREDMINMS, GOINGTIREDMAXMS, !isSleepingAtStart || !racer);
    GoingSadBehaviour =
        new TimedBehaviour(120000, 150000, !racer);
    EmotionBehaviour = new TimedBehaviour(1000, 1500, true);
    CryBehaviour =
      new TimedBehaviour(5000, 15000, false);
    PresentBehaviour =
      new TimedBehaviour(60000 * 3, 60000 * 7, !racer);
    SleepingBehaviour = new(5000, 10000, isSleepingAtStart);
    //+0.1 every 3mn=1lvl/30mn
    GrowthBehaviour = new(3000 * 60, 3000 * 60, !racer);
    TimeSpan elapsedTime = DateTime.Now - lastPlayed;
    Happiness = OfflineProgress.HappinessAfter(happiness, elapsedTime);
    Age = age;
    KnowItsName = age > 2;
    switch (age)
    {
      case < 2:
        MaxEnergy = 10;
        MaxHappinness = 10;
        break;
      case < 3:
        MaxEnergy = 15;
        MaxHappinness = 15;
        break;
      case < 4:
        MaxEnergy = 20;
        MaxHappinness = 20;
        break;
    }
    // Left alone overnight or longer, a beboo has had all the sleep it needs.
    Energy = OfflineProgress.EnergyAfter(energy, MaxEnergy * RESTEDAT, elapsedTime);
    //SpeechRecognizer = new BebooSpeechRecognition(this);
    //SpeechRecognizer.BebooCalled += Call;
  }

  public string Name { get; set; }
  public float Age
  {
    get => age; set
    {
      value = Math.Clamp(value, 1, 10);
      if (age < value) TestLevelUp(age, value);
      age = value;
    }
  }

  private void TestLevelUp(float preview, float updated)
  {
    if (preview >= 1 && preview < 2 && updated >= 2)
    {
      KnowItsName = true;
    }
    else if (preview > 2 && preview < 3 && updated >= 3)
    {
      MaxHappinness = 15;
      MaxEnergy = 15;
    }
    else if (preview > 3 && preview < 4 && updated >= 4)
    {
      MaxEnergy = 20;
      MaxHappinness = 20;
    }
  }

  public float Energy
  {
    get;
    set
    {
      value = Math.Clamp(value, -10, MaxEnergy);
      field = value;
    }
  }

  public int Happiness
  {
    get;
    set
    {
      field = Math.Clamp(value, -10, MaxHappinness);
    }
  }

  /// <summary>
  /// Happiness a sad beboo has to climb back to before it counts as cheered up. Bursting into
  /// tears happens at 0, so there is a gap in between: a beboo on the way up keeps its sad music
  /// until it is genuinely better, and one on the way down does not flip at the first bad tick.
  /// </summary>
  public const int CHEEREDUPAT = 3;

  public Vector3 Position { get; set; }
  public bool Happy { get; private set; } = true;
  public bool Sleeping { get; private set; }
  public bool Panik { get; private set; } = false;
  private TimedBehaviour CuteBehaviour { get; }

  public Vector3? Destination
  {
    get;
    set => field = value.HasValue ? GameHost.Current.Map?.Clamp(value.Value) : null;
  }

  private TimedBehaviour GoingTiredBehaviour { get; }
  private TimedBehaviour GoingSadBehaviour { get; }
  public TimedBehaviour EmotionBehaviour { get; private set; }
  private TimedBehaviour MoveBehaviour { get; }
  public TimedBehaviour GoToSleepOrWakeUpBehaviour { get; private set; }
  private TimedBehaviour FancyMoveBehaviour { get; }
  private TimedBehaviour CryBehaviour { get; }
  private TimedBehaviour PresentBehaviour { get; }
  private TimedBehaviour SleepingBehaviour { get; }
  public TimedBehaviour GrowthBehaviour { get; }
  //public BebooSpeechRecognition SpeechRecognizer { get; }
  public bool KnowItsName { get; set; }
  public int MaxEnergy { get; private set; } = 10;

  /// <summary>
  /// How rested this beboo is, as a share of what it can hold. Thresholds are fractions rather
  /// than fixed numbers because MaxEnergy grows from 10 to 20 as a beboo ages.
  /// </summary>
  public EnergyStage EnergyLevel
  {
    get
    {
      if (Energy <= 0) return EnergyStage.Exhausted;
      float share = Energy / MaxEnergy;
      if (share <= 0.25f) return EnergyStage.Tired;
      if (share <= 0.5f) return EnergyStage.LittleTired;
      if (share <= 0.75f) return EnergyStage.Ok;
      return EnergyStage.Energetic;
    }
  }
  public int MaxHappinness { get; private set; } = 10;
  public bool Paused { get; private set; }

  /// <summary>Set when this beboo's mood changed and the map's music has yet to catch up.</summary>
  private bool _moodMusicDirty;

  private void BurstInTearrs()
  {
    if (!Happy || Sleeping) return;
    Happy = false;
    Voice.Current.Say(String.Format(BebooText.beboo_sadstart, Name));
    _moodMusicDirty = true;
    CuteBehaviour.Stop();
    CryBehaviour.Start();
    MoveBehaviour.MinMS = 800;
    MoveBehaviour.MaxMS = 1000;
  }

  private void BeHappy()
  {
    if (Happy || Happiness < CHEEREDUPAT) return;
    Happy = true;
    // Cheering up said nothing at all, so somebody comforting a crying beboo had no way of knowing
    // it had worked - they simply stopped hearing crying at some point. This is where the jingle
    // belongs: it marks the beboo being alright again, which is the thing worth marking.
    GameHost.Current.SoundSystem.System.PlaySound(GameHost.Current.SoundSystem.JingleComplete);
    CryBehaviour.Stop();
    CuteBehaviour.Start();
    MoveBehaviour.MinMS = 200;
    MoveBehaviour.MaxMS = 400;
    _moodMusicDirty = true;
  }

  /// <summary>
  /// The sad music belongs to the map rather than to one beboo, so hand the choice back to
  /// PlayMapMusic: it keeps the sad tune while somebody is still crying and lifts it once nobody
  /// is. Skipped during a contest, which is playing its own music.
  /// </summary>
  private void RefreshMoodMusic()
  {
    if (!_moodMusicDirty) return;
    _moodMusicDirty = false;
    if (GameHost.Current.Map?.IsRaceMap ?? false) return;
    GameHost.Current.ChangeMapMusic();
  }

  private bool MoveTowardGoal()
  {
    if (Destination == null || Destination == Position || Sleeping || IsHeld) return false;
    Vector3 direction = (Vector3)Destination - Position;
    Vector3 directionNormalized = Vector3.Normalize(direction);
    directionNormalized.X = Math.Sign(directionNormalized.X);
    directionNormalized.Y = Math.Sign(directionNormalized.Y);
    Position += directionNormalized;
    if (BootsSlippedOn && GameHost.Current.Random.Next(2) == 1) Position += directionNormalized;
    if (GameHost.Current.Map?.IsInWater(Position) ?? false)
    {
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepWaterSound, this, false);
      if (SwimLevel <= NerveInWater || (SwimLevel < 10 && GameHost.Current.Random.Next(SwimLevel) == 1))
      {
        StartPanik(inWater: true);
        Destination = GameHost.Current.Map.GenerateRandomUnoccupedPosition(true);
      }
    }
    else if (GameHost.Current.Map == Map.Snowy)
    {
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepSnowSound, this, false);
      if (BootsSlippedOn || GameHost.Current.Random.Next(4) == 1)
      {
        GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepSlipSound, this, false);
        Position += Util.DIRECTIONS[GameHost.Current.Random.Next(Util.DIRECTIONS.Length)];
      }
    }
    else if (GameHost.Current.Map == Map.SnowyRace)
    {
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepSnowSound, this, false);
      if (BootsSlippedOn || GameHost.Current.Random.Next(4) == 1)
      {
        GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepSlipSound, this, false);
        Position += Util.DIRECTIONS[GameHost.Current.Random.Next(Util.DIRECTIONS.Length)] * 2;
      }
    }
    else
    {
      if (BootsSlippedOn) GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BoingSounds, this, false);
      else GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooStepSound, this, false);
      EndPanik();
    }

    PlayArround();
    if (GameHost.Current.Random.Next(20) == 1) BootsSlippedOn = false;
    bool moved = Position != Destination;
    if (!moved) Destination = null;
    return moved;
  }

  private void PlayArround()
  {
    var proximityBeboos = GameHost.Current.Map?.GetBeboosArround(Position);
    (proximityBeboos ??= []).Remove(this);
    if (GameHost.Current.Random.Next(Trait == Trait.Playful ? 3 : 4) == 1)
    {
      Item.Item? proximityItem = GameHost.Current.Map?.GetItemArroundPosition(Position);
      if (proximityItem != null)
      {
        proximityItem.BebooAction(this);
        GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooFunSounds, this);
        Happiness++;
      }
    }
    if (proximityBeboos.Count > 0 && GameHost.Current.Random.Next(3) == 1)
    {
      InteractWith(proximityBeboos[GameHost.Current.Random.Next(proximityBeboos.Count)]);
      Happiness++;
    }
  }

  private void InteractWith(Beboo friend)
  {
    if (friend.Sleeping) ForceWakeUp(friend);
    else if (GameHost.Current.Map?.IsDansePlaying ?? false)
      SingWith(friend);
    else
    {
      var rnd = GameHost.Current.Random.Next(5);
      switch (rnd)
      {
        case 1: case 2: SingWith(friend); break;
        case 3: Follow(friend); break;
        case 4: Scare(friend); break;
      }
    }
  }

  /// <summary>Whether the current panic began in water. Only those teach a beboo to swim.</summary>
  private bool _panikInWater;

  private readonly List<(DateTime At, Action What)> _later = [];
  private readonly object _laterLock = new();

  /// <summary>
  /// Runs something after a delay, on the game thread, from Update. Sounds, screen reader output
  /// and anything that reads the current map must not happen on a pool thread: the map can be
  /// swapped out from under them the moment the player walks through a path.
  /// </summary>
  public void Later(int delayMs, Action what)
  {
    lock (_laterLock) _later.Add((DateTime.Now.AddMilliseconds(delayMs), what));
  }

  /// <summary>Runs whatever has come due. Called from Update, so never while the game is paused.</summary>
  public void RunDueWork()
  {
    List<Action> due = [];
    lock (_laterLock)
    {
      for (int i = _later.Count - 1; i >= 0; i--)
      {
        if (DateTime.Now < _later[i].At) continue;
        due.Add(_later[i].What);
        _later.RemoveAt(i);
      }
    }
    foreach (Action what in due) what();
  }

  private void EndPanik()
  {
    if (!Panik) return;
    Panik = false;
    // Floundering in water and getting out again is what teaches swimming. Being startled by a
    // friend on dry land used to count too, and beboos startle each other constantly, so swim
    // levels piled up without any water and unlocked the underwater map on the strength of it.
    if (_panikInWater) SwimLevel += 1;
    _panikInWater = false;
    MoveBehaviour.Restart();
    FancyMoveBehaviour.Restart();
  }

  private void StartPanik(bool inWater = false)
  {
    if (Panik) return;
    Panik = true;
    _panikInWater = inWater;
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooScreamSound, this);
    Happiness -= Trait == Trait.Timid ? 3 : 2;
    Energy -= 2;
    FancyMoveBehaviour.MinMS = 400;
    FancyMoveBehaviour.MaxMS = 400;
    MoveBehaviour.MinMS = 100;
    MoveBehaviour.MaxMS = 100;
  }

  /// <summary>Whether this beboo is in the middle of saying something.</summary>
  private bool IsSpeaking
  {
    get
    {
      try
      {
        return Channel != null && Channel.IsPlaying;
      }
      catch (FmodException)
      {
        // The channel has already finished and been recycled, so nothing is playing.
        return false;
      }
    }
  }

  private void DoCuteThing()
  {
    // Idle chatter waits its turn. It used to cut off whatever was already playing, so the delight
    // of being stroked would be chopped in half by the next scheduled noise.
    if (IsSpeaking) return;
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooCuteSounds, this);
  }

  private void WannaGoToRandomPlace()
  {
    if (GameHost.Current.Map?.Items.Count > 0 && GameHost.Current.Random.Next(2) == 1)
    {
      Item.Item? targetItem = GameHost.Current.Map?.Items[GameHost.Current.Random.Next(GameHost.Current.Map.Items.Count)];
      if (targetItem != null) Destination = targetItem.Position;
    }
    else if (GameHost.Current.Map?.Beboos.Count > 1
        && (Trait == Trait.Cuddly || GameHost.Current.Random.Next(2) == 1))
    {
      var otherBeboos = new List<Beboo>(GameHost.Current.Map.Beboos);
      otherBeboos.Remove(this);
      var targetBeboo = otherBeboos[GameHost.Current.Random.Next(otherBeboos.Count)];
      Destination = targetBeboo.Position;
    }
    else
    {
      Vector3 randomMove = new(GameHost.Current.Random.Next(-4, 5), GameHost.Current.Random.Next(-4, 5), 0);
      Destination = Position + randomMove;
    }
  }

  public void GoAsleep(bool cradled = false)
  {
    if (Sleeping || IsBeingShaken) return;
    if (cradled || IsHeld || SwimLevel >= 10 || GameHost.Current.Map == Map.UnderWater || (!GameHost.Current.Map?.IsInWater(Position) ?? false))
    {
      Voice.Current.Say(String.Format(cradled ? BebooText.beboo_sleepinarms : BebooText.beboo_gosleep, Name));
      GoingTiredBehaviour.Stop();
      MoveBehaviour.Stop();
      FancyMoveBehaviour.Stop();
      CuteBehaviour.Stop();
      GoingSadBehaviour.Stop();
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.GrassSound, this);
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooYawningSounds, this);
      Sleeping = true;
      SleepingBehaviour.Start();
      StartCradleWakeResistance();
    }
    else
    {
      Destination = new(0, 0, 0); //TODO do somethin better
    }
  }

  public void WakeUp(bool force = false)
  {
    if (GameHost.Current.Map != null && (!Sleeping || GameHost.Current.Map.IsLullabyPlaying)) return;
    if (!force && ResistCradleWakeUp()) return;
    Voice.Current.Say(String.Format(BebooText.beboo_wakeup, Name));
    SleepingBehaviour.Stop();
    GoingTiredBehaviour.Start();
    FancyMoveBehaviour.Start();
    MoveBehaviour.Start(3000);
    CuteBehaviour.Start();
    GoingSadBehaviour.Start();
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.GrassSound, this);
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooYawningSounds, this);
    Sleeping = false;
    ForgetSnuggling();
    BeHappy();
  }

  public void Eat(FruitSpecies fruitSpecies)
  {
    if (Sleeping) return;
    if (fruitSpecies == FruitSpecies.Normal)
    {
      Energy++;
      Happiness++;
    }
    else if (fruitSpecies == FruitSpecies.Energetic)
    {
      Energy += 3;
      Happiness++;
    }
    else if (fruitSpecies == FruitSpecies.Shrink) VoicePitch += 0.1f;
    else if (fruitSpecies == FruitSpecies.Growth) VoicePitch -= 0.1f;

    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooChewSounds, this, true, 0.5f);
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooYumySounds, this);
  }

  public void GetPetted()
  {
    if ((DateTime.Now - _lastPetted).TotalMilliseconds < 800) return;
    _lastPetted = DateTime.Now;
    // A sleeping beboo does not chirp happily at being stroked. You get its breathing instead, so
    // the touch still answers you and tells you what it is doing.
    if (Sleeping)
    {
      GameHost.Current.SoundSystem.PlayBebooSound(
          GameHost.Current.SoundSystem.BebooSleepingSounds, this, false, 0.3f);
      return;
    }
    _petCount++;
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooPetSound, this, false);

    // Comforting climbs on every stroke, not every third or fourth. Happiness floors at -10 and
    // crying stops at CHEEREDUPAT, so even at one per delight a beboo at the bottom took half a
    // minute of unbroken stroking to console - long enough that it reads as not working at all,
    // which is the opposite of what comforting something should feel like. Once it is content
    // again the delight gate takes over and happiness climbs slowly, as before.
    if (Happiness < CHEEREDUPAT)
    {
      Happiness++;
      if (Happiness >= CHEEREDUPAT) _petCount = PetsBeforeDelight;
    }

    if (_petCount + GameHost.Current.Random.Next(2) >= PetsBeforeDelight)
    {
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooDelightSounds, this);
      // A contented beboo warms up slowly; the coin flip is what makes it gradual. Consoling a sad
      // one is handled above, on every stroke.
      if (Happiness <= 7 && Happiness >= CHEEREDUPAT && GameHost.Current.Random.Next(2) == 1)
      {
        Happiness++;
        GameHost.Current.SoundSystem.System.PlaySound(GameHost.Current.SoundSystem.JingleComplete);
      }

      _petCount = 0;
    }
    //if (GameHost.Current.Random.Next(101) == 1) GameHost.Current.GainTicket(GameHost.Current.Random.Next(3));
  }

  /// <summary>
  /// Sets how fast this beboo moves for a contest. Racers never run the emotion tick that would
  /// otherwise set their pace, and a contest should be decided by condition rather than by mood.
  /// </summary>
  public void SetCompetitionPace()
  {
    (MoveBehaviour.MinMS, MoveBehaviour.MaxMS) = EnergyLevel switch
    {
      EnergyStage.Energetic => (110, 190),
      EnergyStage.Ok => (150, 250),
      EnergyStage.LittleTired => (210, 330),
      EnergyStage.Tired => (300, 460),
      _ => (430, 650),
    };
  }

  private void BeNormal()
  {
    MoveBehaviour.Restart();
    FancyMoveBehaviour.Restart();
    //CuteBehaviour.Restart();
  }

  private void BeFloppy()
  {
    MoveBehaviour.MinMS = 800;
    MoveBehaviour.MaxMS = 1000;
    FancyMoveBehaviour.MinMS = 40000;
    FancyMoveBehaviour.MaxMS = 70000;
    CuteBehaviour.MinMS = 15000;
    CuteBehaviour.MaxMS = 25000;
  }

  private void BeOverexcited()
  {
    MoveBehaviour.MinMS = 50;
    MoveBehaviour.MaxMS = 150;
    FancyMoveBehaviour.MinMS = 5000;
    FancyMoveBehaviour.MaxMS = 10000;
  }
  public void Pause()
  {
    Paused = true;
    CuteBehaviour.Stop();
    MoveBehaviour.Stop();
    GoingSadBehaviour.Stop();
    CryBehaviour.Stop();
  }
  public void Unpause()
  {
    Paused = false;
    SleepingBehaviour.Start();
    if (Happy)
    {
      CuteBehaviour.Start();
    }
    else CryBehaviour.Start();
    MoveBehaviour.Start();
    GoingSadBehaviour.Start();
  }
  public void Call(object? sender, EventArgs eventArgs)
  {
    if (Paused || Sleeping || !KnowItsName) return;
    Later(1000, () => WakeUp());
    Destination = GameHost.Current.PlayerPosition;
  }
  public void Scare(Beboo friend)
  {
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooInteractSounds, this);
    friend.GetScared(this);
  }
  public void ForceWakeUp(Beboo friend)
  {
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooInteractSounds, this);
    friend.GetWakeUped(this);
  }
  public void GetScared(Beboo friend)
  {
    // A brave beboo shrugs off about half of what its friends spring on it.
    if (Trait == Trait.Brave && GameHost.Current.Random.Next(2) == 1) return;
    StartPanik();
    Later(PanikMs, EndPanik);
  }
  public void GetWakeUped(Beboo friend)
  {
    if (ResistCradleWakeUp()) return;
    if (Trait == Trait.Dreamy && GameHost.Current.Random.Next(3) != 1) return;
    GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooSurpriseSounds, this);
    Later(2000, () =>
    {
      GameHost.Current.SoundSystem.PlayBebooSound(GameHost.Current.SoundSystem.BebooAngrySounds, this);
      WakeUp(true);
    });
  }
  public void Follow(Beboo friend)
  {
    Destination = friend.Destination;
  }
  public void SingWith(Beboo friend)
  {
    if (friend.Channel != null && friend.Channel.IsPlaying) return;
    var songsList = SoundSystem.GetBebooSounds(GameHost.Current.SoundSystem.BebooSongSounds, this);
    var songsListFriend = SoundSystem.GetBebooSounds(GameHost.Current.SoundSystem.BebooSongSounds, friend);
    if (songsList.Count > 0 && songsListFriend.Count > 0)
    {
      var randomSong = songsList[GameHost.Current.Random.Next(songsList.Count)];
      // The friend's song, out of the friend's own songs. This drew from this beboo's list using
      // the friend's count, which sang in the wrong voice and threw outright whenever the friend
      // had more songs to choose from - easy to hit now that a mod creature can have a voice of a
      // different size.
      var randomSongFriend = songsListFriend[GameHost.Current.Random.Next(songsListFriend.Count)];
      GameHost.Current.SoundSystem.PlayBebooSound(randomSong, this);
      friend.Later(100, () =>
          GameHost.Current.SoundSystem.PlayBebooSound(randomSongFriend, friend));
      Happiness++;
      friend.Happiness++;
    }
  }
}