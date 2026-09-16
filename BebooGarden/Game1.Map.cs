using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BebooGarden;

public partial class Game1
{
  private Map? _backedMap;
  /// <summary>
  /// Goes through the gate between two maps. The moving is shared with the Android head; see
  /// GameCore.PlayerActions.TravelThrough.
  /// </summary>
  private void TravelBetwieen(MapPreset a, MapPreset b)
  {
    if (Map is null) return;
    MapPreset target = Map.Preset == a ? b : Map.Preset == b ? a : Map.Preset;
    if (target == Map.Preset) return;
    MapConnexion? connexion = Map.GetConnexionArroundPosition(PlayerPosition);
    GameCore.PlayerActions.TravelThrough(this,
        connexion ?? new MapConnexion(PlayerPosition, target, () => string.Empty));
  }

  public void ChangeMapMusic()
  {
    if (Map != null) SoundSystem.PlayMapMusic(Map);
  }

  public void ChangeMap(Map map, bool backup = true)
  {
    if (Map != null) SoundSystem.Pause(Map);
    if (backup) _backedMap = Map;
    Map = map;
    SoundSystem.LoadMap(map);
    foreach (var otherMap in Map.Maps.Values)
      if (otherMap != map) SoundSystem.Pause(otherMap);
  }
  public void LoadBackedMap()
  {
    if (_backedMap == null) return;
    SoundSystem.Pause(Map);
    Map = _backedMap;
    _backedMap = null;
    SoundSystem.Unpause(Map);
  }

}
