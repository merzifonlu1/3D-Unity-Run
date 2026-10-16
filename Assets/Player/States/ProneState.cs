// Yuzustu: surunerek cok yavas hareket, yavas donus, nisan alinamaz.
// Z ile ayaga kalkar, C ile comelir.
public class ProneState : LocomotionState
{
    public ProneState(PlayerMovement player) : base(player) { }

    public override PlayerStance Stance => PlayerStance.Prone;
    public override bool CanAim => false;
    protected override float SpeedMultiplier => player.ProneMultiplier;
    protected override float TurnSpeed => player.ProneTurnSpeed;

    protected override PlayerState NextState()
    {
        if (player.CrouchPressed && player.CanFit(PlayerStance.Crouching))
            return player.Crouch;
        if ((player.PronePressed || player.SprintPressed) && player.CanFit(PlayerStance.Standing))
            return StandingGait();
        return this;
    }
}
