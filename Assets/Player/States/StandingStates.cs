// Ayakta yuruyus durumlari. Hepsi LocomotionState'in gecislerini kullanir,
// sadece hiz ve animasyon degeri farklidir.

public class WalkState : LocomotionState
{
    public WalkState(PlayerMovement player) : base(player) { }

    protected override float SpeedMultiplier => 1f;
}

public class SprintState : LocomotionState
{
    public SprintState(PlayerMovement player) : base(player) { }

    protected override float SpeedMultiplier => player.SprintMultiplier;
    public override float Gait => 2f;
}

public class SlowWalkState : LocomotionState
{
    public SlowWalkState(PlayerMovement player) : base(player) { }

    protected override float SpeedMultiplier => player.SlowWalkMultiplier;
    public override float Gait => 0.5f;
}
