using UnityEngine;

// Yerde hareket eden butun durumlarin ortak davranisi: WASD ile kameraya gore hareket,
// gidilen yone donme ve durus tuslari (C comel) ile gecis.
public abstract class LocomotionState : PlayerState
{
    protected LocomotionState(PlayerMovement player) : base(player) { }

    // BaseSpeed ile carpilan hiz carpani.
    protected abstract float SpeedMultiplier { get; }

    protected virtual float TurnSpeed => player.TurnSpeed;

    public override float Gait => 1f;

    public override void Enter()
    {
        player.SetBodyShape(Stance);
    }

    public override void Tick()
    {
        PlayerState next = NextState();
        if (next != null && next != this)
            player.ChangeState(next);
    }

    public override void FixedTick()
    {
        player.Move(player.MoveInput * (player.WalkSpeed * SpeedMultiplier), TurnSpeed);
    }

    // Ayaktaki durumlar icin varsayilan gecisler; comelme bunu ezer.
    protected virtual PlayerState NextState()
    {
        if (player.CrouchPressed)
            return player.Crouch;
        return StandingGait();
    }

    // Ayakta hangi yuruyus: Shift kosu, yoksa normal yuruyus.
    protected PlayerState StandingGait()
    {
        bool moving = player.MoveInput.sqrMagnitude > 0.01f;
        if (player.SprintHeld && moving && !player.aiming)
            return player.Sprint;
        return player.Walk;
    }
}
