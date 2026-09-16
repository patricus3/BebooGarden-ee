namespace BebooGarden.Save;

public class Flags
{
  public Flags()
  {
  }

  public Flags(bool newGame, bool hasUnlockOnline, bool unlockShop, bool voiceRecoPopupPrinted)
  {
    NewGame = newGame;
    HasUnlockOnline = hasUnlockOnline;
    UnlockShop = unlockShop;
    VoiceRecoPopupPrinted = voiceRecoPopupPrinted;
  }

  /// <summary>
  /// True until the opening sequence has been completed. While it is set the game throws away
  /// whatever is in the save and builds a fresh garden, so it must be cleared the moment there is
  /// anything worth keeping - which is when the welcome finishes, not later.
  /// </summary>
  public bool NewGame { get; set; } = true;

  /// <summary>
  /// Whether the player has been told how to play. Separate from <see cref="NewGame"/> on purpose:
  /// the tips come after the first beboo is named, which is much later than the point at which the
  /// garden becomes worth saving. Sharing one flag for both meant anyone who finished the welcome
  /// and quit before naming a beboo came back to the opening sequence again, and had their garden
  /// discarded and overwritten.
  /// </summary>
  public bool TipsShown { get; set; }
  public bool HasUnlockOnline { get; set; }
  public bool UnlockShop { get; set; }
  public bool VoiceRecoPopupPrinted { get; set; }
  public bool UnlockSnowyMap { get; set; }
  public bool UnlockBeachMap { get; set; }
  public bool UnlockPerfectSwimming { get; set; }
  public bool UnlockEggInShop { get; set; }
  public bool UnlockUnderwaterMap { get; set; }
  public bool UnlockFluffMap { get; set; }
}