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
    TypeNameHandling = TypeNameHandling.All
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
    catch (JsonException)
    {
      // Anything we can't read, an encrypted save from an older version included, starts over
      // rather than taking the game down on startup.
      return null;
    }
  }


  public static void WriteSave(SaveParameters parameters)
  {
    string json = JsonConvert.SerializeObject(parameters, Settings);
    File.WriteAllText(GamePaths.SaveFile, json);
  }
}