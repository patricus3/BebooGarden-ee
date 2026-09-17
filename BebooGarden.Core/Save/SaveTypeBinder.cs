using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Text.RegularExpressions;

namespace BebooGarden.Save;

/// <summary>
/// Finds the type a save is talking about, whichever assembly it used to live in.
///
/// Saves are written with TypeNameHandling.All, so every object carries its type AND the assembly
/// that held it: "BebooGarden.Save.Flags, BebooGarden". Moving the game logic into
/// BebooGarden.Core changed the second half of every one of those names, and Newtonsoft could no
/// longer resolve a single type in any save written before the move. Loading threw, the throw was
/// caught as an unreadable save, and the player was handed a brand new garden - with their real
/// one still sitting on disk, perfectly intact and unreadable.
///
/// The repair is narrow on purpose. Newtonsoft's own binder already resolves everything correctly,
/// including framework generics like SortedDictionary`2, which is harder than it looks: the
/// assembly is recorded separately for the outer type and again inside each type argument. So
/// this does not do the finding. It renames the assembly that moved, in all the places the name
/// can appear, and lets the normal binder do the rest.
/// </summary>
public sealed class SaveTypeBinder : DefaultSerializationBinder
{
  public static readonly SaveTypeBinder Instance = new();

  /// <summary>Where the game's saved types used to live, and where they live now.</summary>
  private const string MovedFrom = "BebooGarden";
  private const string MovedTo = "BebooGarden.Core";

  /// <summary>
  /// The old assembly name where it is used as one: after a comma, ending the name or a type
  /// argument. Matching the bare word would also rewrite namespaces, and every one of these type
  /// names begins with BebooGarden.
  /// </summary>
  private static readonly Regex AsAssemblyName =
      new($@",\s*{Regex.Escape(MovedFrom)}(?=\]|,|$)", RegexOptions.Compiled);

  public override Type BindToType(string? assemblyName, string typeName)
  {
    try
    {
      return base.BindToType(assemblyName, typeName);
    }
    catch (JsonSerializationException)
    {
      // Probably a save from before the move. Say where those types are now and ask again; if it
      // still cannot be found, the original failure is the honest one to report.
      string movedTypeName = AsAssemblyName.Replace(typeName, $", {MovedTo}");
      string? movedAssembly = assemblyName == MovedFrom ? MovedTo : assemblyName;

      if (movedTypeName == typeName && movedAssembly == assemblyName) throw;

      return base.BindToType(movedAssembly, movedTypeName);
    }
  }
}
