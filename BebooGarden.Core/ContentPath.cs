using System;
using System.IO;
using System.Linq;

namespace BebooGarden;

/// <summary>
/// Finds content whose name is spelled with different capitals than the code asks for.
///
/// Windows does not care about the case of a path and Android does, so a folder the desktop has
/// opened happily for years can simply not exist on a phone. The beboo voices are the example that
/// matters: the folders are named after <see cref="GameCore.Pet.BebooType"/>, and on disk they are
/// "base", "Pink" and "green" against enum members Base, Pink and Green. On Android that meant a
/// pink beboo had a voice and every other one was mute - and mute in the worst way, because a
/// missing voice folder is tolerated on purpose, for mods, so nothing was reported at all.
///
/// Renaming the folders would fix those three and leave the next one to be discovered by a player.
/// This fixes the class of problem, for mod folders too, and only does any work when the exact
/// path missed - which on Windows is never.
/// </summary>
public static class ContentPath
{
  /// <summary>
  /// The directory as it is actually spelled on disk, or null if there is no such directory under
  /// any capitalisation.
  /// </summary>
  public static string? ResolveDirectory(string path)
  {
    if (string.IsNullOrEmpty(path)) return null;
    if (Directory.Exists(path)) return path;

    string full;
    try { full = Path.GetFullPath(path); }
    catch (Exception) { return null; }

    string[] parts = full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    if (parts.Length == 0) return null;

    // Start from the deepest ancestor that does exist, then match a segment at a time. Anything
    // above that is the device's own filesystem and is spelled however it is spelled.
    string current = parts[0].Length == 0 ? Path.DirectorySeparatorChar.ToString() : parts[0] + Path.DirectorySeparatorChar;
    if (!Directory.Exists(current)) return null;

    foreach (string segment in parts.Skip(1).Where(p => p.Length > 0))
    {
      string exact = Path.Combine(current, segment);
      if (Directory.Exists(exact))
      {
        current = exact;
        continue;
      }

      string? match;
      try
      {
        match = Directory.EnumerateDirectories(current)
            .FirstOrDefault(d => string.Equals(
                Path.GetFileName(d), segment, StringComparison.OrdinalIgnoreCase));
      }
      catch (Exception)
      {
        return null;
      }

      if (match is null) return null;
      current = match;
    }

    return current;
  }

  /// <summary>
  /// The file as it is actually spelled on disk, or null if there is no such file. Resolves the
  /// directory first, then the name within it.
  /// </summary>
  public static string? ResolveFile(string path)
  {
    if (string.IsNullOrEmpty(path)) return null;
    if (File.Exists(path)) return path;

    string? directory = ResolveDirectory(Path.GetDirectoryName(path) ?? string.Empty);
    if (directory is null) return null;

    string name = Path.GetFileName(path);
    string exact = Path.Combine(directory, name);
    if (File.Exists(exact)) return exact;

    try
    {
      return Directory.EnumerateFiles(directory)
          .FirstOrDefault(f => string.Equals(
              Path.GetFileName(f), name, StringComparison.OrdinalIgnoreCase));
    }
    catch (Exception)
    {
      return null;
    }
  }
}
