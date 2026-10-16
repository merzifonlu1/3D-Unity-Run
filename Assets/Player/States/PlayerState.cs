// Oyuncunun durum makinesindeki her durumun temel sinifi.
// PlayerMovement baglamdir (girdi, Rigidbody, hizlar); durumlar kurallari tasir.
// Yeni bir hareket eklemek icin bu siniftan (ya da LocomotionState'ten) turet.
public abstract class PlayerState
{
    protected readonly PlayerMovement player;

    protected PlayerState(PlayerMovement player)
    {
        this.player = player;
    }

    // Collider ve Animator'un kullandigi durus.
    public virtual PlayerStance Stance => PlayerStance.Standing;

    // Bu durumdayken sag tik ile nisan alinabilir mi.
    public virtual bool CanAim => true;

    // Animator'e giden yuruyus degeri: 0 durma, 0.5 yavas, 1 yuruyus, 2 kosu.
    public virtual float Gait => 0f;

    public virtual void Enter() { }
    public virtual void Exit() { }

    // Update'te: girdi ve diger durumlara gecis.
    public virtual void Tick() { }

    // FixedUpdate'te: fizik.
    public virtual void FixedTick() { }
}

public enum PlayerStance
{
    Standing = 0,
    Crouching = 1,
}
