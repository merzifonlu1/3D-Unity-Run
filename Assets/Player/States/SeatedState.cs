// Trende koltukta oturma. Girisi ve cikisi TrainBoarding yonetir
// (PlayerMovement.EnterSeat / ExitSeat); bu durumda hareket ve nisan yoktur.
public class SeatedState : PlayerState
{
    public SeatedState(PlayerMovement player) : base(player) { }

    public override bool CanAim => false;

    public override void Enter()
    {
        player.SetBodyShape(PlayerStance.Standing);
        player.Animation.SetSeated(true);
    }

    public override void Exit()
    {
        player.Animation.SetSeated(false);
    }
}
