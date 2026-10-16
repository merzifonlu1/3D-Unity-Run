// Comelme: yavas hareket, collider alcalir. C ya da Shift ile kalkar, Z ile yuzustu yatar.
// Ust tarafta yer yoksa ayaga kalkilmaz.
public class CrouchState : LocomotionState
{
    public CrouchState(PlayerMovement player) : base(player) { }

    public override PlayerStance Stance => PlayerStance.Crouching;
    protected override float SpeedMultiplier => player.CrouchMultiplier;

    protected override PlayerState NextState()
    {
        if (player.PronePressed && player.CanFit(PlayerStance.Prone))
            return player.Prone;
        if ((player.CrouchPressed || player.SprintPressed) && player.CanFit(PlayerStance.Standing))
            return StandingGait();
        return this;
    }
}
