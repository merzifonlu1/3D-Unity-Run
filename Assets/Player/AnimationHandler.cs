using UnityEngine;

// Oyuncunun Animator parametrelerini tek yerden yazar.
// Speed: 0 durma, 0.5 yavas yuruyus, 1 yuruyus, 2 kosu (comelme icinde 0..1).
// Stance: 0 ayakta, 1 comelme. Seated: koltukta.
[RequireComponent(typeof(Animator))]
public class AnimationHandler : MonoBehaviour
{
    private static readonly int SpeedId = Animator.StringToHash("Speed");
    private static readonly int StanceId = Animator.StringToHash("Stance");
    private static readonly int SeatedId = Animator.StringToHash("Seated");

    public const int AimLayer = 1;
    public const int AimUpperBodyLayer = 3;

    [Tooltip("Speed degerinin yumusatma suresi (saniye)")]
    [SerializeField] private float speedDamp = 0.1f;

    private Animator animator;

    public Animator Animator
    {
        get
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            return animator;
        }
    }

    public void SetLocomotion(float speed, PlayerStance stance)
    {
        Animator.SetFloat(SpeedId, speed, speedDamp, Time.deltaTime);
        Animator.SetInteger(StanceId, (int)stance);
    }

    public void SetSeated(bool seated)
    {
        Animator.SetBool(SeatedId, seated);
        Animator.SetInteger(StanceId, (int)PlayerStance.Standing);
        Animator.SetFloat(SpeedId, 0f);
    }

    // Ayakta nisan tum vucudu, comelirken sadece ust vucudu nisan katmanina verir.
    public void SetAimWeight(float weight, PlayerStance stance, bool hasWeapon)
    {
        bool upperOnly = stance != PlayerStance.Standing;
        Animator.SetLayerWeight(AimLayer, upperOnly ? 0f : weight);
        if (Animator.layerCount > AimUpperBodyLayer)
            Animator.SetLayerWeight(AimUpperBodyLayer, upperOnly && hasWeapon ? weight : 0f);
    }
}
