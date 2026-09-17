using BebooGarden.GameCore;
using Newtonsoft.Json;
using System;
using System.IO;

namespace BebooGarden.Save;

public class SaveManager
{

  private static readonly JsonSerializerSettings Settings = new()
  {
    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
    NullValueHandling = NullValueHandling.Ignore,
    DefaultValueHandling = DefaultValueHandling.Populate,
    TypeNameHandling = TypeNameHandling.All,
    // Every object in a save carries the assembly it came from. The game logic moved to
    // BebooGarden.Core, which changed that for every type, and without this a save written before
    // the move names types that no longer exist anywhere - so it fails to load and the player is
    // handed a new garden while their real one sits on disk intact. See SaveTypeBinder.
    SerializationBinder = SaveTypeBinder.Instance
  };

  public static SaveParameters LoadSave()
  {
    GameHost.Current.SoundSystem.LoadMenuSounds();
    SaveParameters parameters = LoadJson() ?? new SaveParameters();
    // Saves written before TipsShown existed have no value for it. Anyone already past the
    // opening sequence has long since been told how to play, so do not tell them again.
    if (!parameters.Flags.NewGame) parameters.Flags.TipsShown = true;
    return parameters;
  }

  private static SaveParameters? LoadJson()
  {
    GamePaths.MigrateLegacyFiles();
    if (!File.Exists(GamePaths.SaveFile)) return null;
    string json = File.ReadAllText(GamePaths.SaveFile);
    try
    {
      return JsonConvert.DeserializeObject<SaveParameters>(json, Settings);
    }
    catch (JsonException error)
    {
      // Anything we can't read, an encrypted save from an older version included, starts over
      // rather than taking the game down on startup. But it is kept: starting over is a guess
      // about what the player wants, and overwriting the only copy of their garden on the next
      // save turns that guess into a certainty.
      KeepUnreadableSave(error);
      return null;
    }
  }


  /// <summary>
  /// Puts an unreadable save somewhere safe before the game carries on without it.
  ///
  /// A save that will not parse is not necessarily a lost one - it was a renamed assembly that
  /// made this happen, and that was fixable. Whatever the reason, the file is the only copy of
  /// somebody's garden and the alternative is writing over it within two minutes.
  /// </summary>
  private static void KeepUnreadableSave(Exception error)
  {
    try
    {
      string kept = Path.Combine(
          GamePaths.DataFolder,
          $"save-unreadable-{DateTime.Now:yyyyMMdd-HHmmss}.dat");
      File.Copy(GamePaths.SaveFile, kept, overwrite: false);
      File.AppendAllText(GamePaths.CrashLog,
          $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  save could not be read, kept a copy at {kept}"
          + $"{Environment.NewLine}{error}{Environment.NewLine}{Environment.NewLine}");
    }
    catch (Exception)
    {
      // Nothing useful left to do; the game still starts.
    }
  }

  public static void WriteSave(SaveParameters parameters)
  {
    string json = JsonConvert.SerializeObject(parameters, Settings);
    File.WriteAllText(GamePaths.SaveFile, json);
  }
}