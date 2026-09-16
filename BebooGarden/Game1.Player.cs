using BebooGarden.Content;
using BebooGarden.GameCore.Item;
using BebooGarden.GameCore.Pet;
using BebooGarden.GameCore.World;
using BebooGarden.Interface.UI;
using BebooGarden.Minigame;
using BebooGarden.Modding;
using BebooGarden.MiniGames;
using BebooGarden.Save;
using BebooGarden.UI;
using CrossSpeak;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BebooGarden;

public partial class Game1
{
  // Walking, looking, petting and carrying are shared with the Android head; see
  // GameCore.PlayerActions. What is left here is the desktop's own wiring.
  public void MoveOf(Vector3 movement) => GameCore.PlayerActions.MoveOf(this, movement);

  private void ShakeOrPetAtPlayerPosition() => GameCore.PlayerActions.ShakeOrPetAtPlayerPosition(this);

  public Beboo? BebooUnderCursor() => GameCore.PlayerActions.BebooUnderCursor(this);

  public List<Beboo> BeboosUnderCursor(int squareSide = 1)
      => GameCore.PlayerActions.BeboosUnderCursor(this, squareSide);

  private void SayTickets() => GameCore.PlayerActions.SayTickets(this);

  private void SayBasketState() => GameCore.PlayerActions.SayBasketState(this);

  private void TakeFromGround(Item item) => GameCore.PlayerActions.TakeFromGround(this, item);

  private void TryPutItemInHand() => GameCore.PlayerActions.TryPutItemInHand(this);

  private void Whistle() => GameCore.PlayerActions.Whistle(this);

  public void GainTicket(int amount)
  {
    if (amount > 0)
    {
      Save.Tickets += amount;
      CrossSpeakManager.Instance.Output(String.Format(BebooText.gainticket, amount));
      SoundSystem.System.PlaySound(SoundSystem.MenuOk2Sound);
      if (!Save.Flags.UnlockShop && Map != Map.SnowyRace && Map != Map.BasicRace)
      {
        Save.Flags.UnlockShop = true;
        SoundSystem.System.PlaySound(SoundSystem.JingleComplete);
        new TalkDialog(BebooText.shopunlock)
          .Show();
      }
    }
  }
}
