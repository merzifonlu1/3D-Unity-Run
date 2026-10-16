using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;

// Oyuncu hareketinin baglami: girdi, Rigidbody, collider ve nisan burada.
// Hareket kurallari PlayerState'ten tureyen durumlarda (Assets/Player/States):
// WalkState, SprintState, SlowWalkState, CrouchState, ProneState, SeatedState.
public class PlayerMovement : MonoBehaviour
{
    [Header("Hizlar")]
    [Tooltip("Normal yuruyus hizi (m/s)")]
    [SerializeField] private float BaseSpeed;
    [SerializeField, FormerlySerializedAs("SprintCarpanı")] private float SprintCarpani;
    [SerializeField] private float slowWalkMultiplier = 0.5f;
    [SerializeField] private float crouchMultiplier = 0.4f;
    [SerializeField] private float proneMultiplier = 0.15f;
    [Tooltip("Donus hizi (derece/saniye)")]
    [SerializeField] private float turnSpeed = 900f;
    [SerializeField] private float proneTurnSpeed = 200f;

    [Header("Tuslar")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode slowWalkKey = KeyCode.X;
    [SerializeField] private KeyCode crouchKey = KeyCode.C;
    [SerializeField] private KeyCode proneKey = KeyCode.Z;

    [Header("Collider (ayakta, comelme, yuzustu)")]
    [SerializeField] private float crouchHeight = 1.2f;
    [SerializeField] private float proneLength = 1.7f;

    [Header("Nisan")]
    public GameObject LookAtObject;
    public GameObject RigLayerWeaponAim;
    public GameObject RigLayerBodyAim;
    public float ADSSpeed;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private WeaponEquip weapons;
    private Rig WeaponAimRig;
    private Rig BodyAimRig;

    private float standHeight;
    private Vector3 standCenter;
    private float feetY;

    public float WalkSpeed => BaseSpeed;
    public float SprintMultiplier => SprintCarpani;
    public float SlowWalkMultiplier => slowWalkMultiplier;
    public float CrouchMultiplier => crouchMultiplier;
    public float ProneMultiplier => proneMultiplier;
    public float TurnSpeed => turnSpeed;
    public float ProneTurnSpeed => proneTurnSpeed;

    // Kameraya gore hareket yonu (uzunlugu 0..1).
    public Vector3 MoveInput { get; private set; }
    public bool SprintHeld { get; private set; }
    public bool SprintPressed { get; private set; }
    public bool SlowWalkHeld { get; private set; }
    public bool CrouchPressed { get; private set; }
    public bool PronePressed { get; private set; }

    public AnimationHandler Animation { get; private set; }
    public PlayerState CurrentState { get; private set; }
    public WalkState Walk { get; private set; }
    public SprintState Sprint { get; private set; }
    public SlowWalkState SlowWalk { get; private set; }
    public CrouchState Crouch { get; private set; }
    public ProneState Prone { get; private set; }
    public SeatedState Seated { get; private set; }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        weapons = GetComponent<WeaponEquip>();

        var animator = transform.GetChild(0).GetComponent<Animator>();
        Animation = animator.GetComponent<AnimationHandler>();
        if (Animation == null)
            Animation = animator.gameObject.AddComponent<AnimationHandler>();

        standHeight = capsule.height;
        standCenter = capsule.center;
        feetY = standCenter.y - standHeight / 2f;

        Walk = new WalkState(this);
        Sprint = new SprintState(this);
        SlowWalk = new SlowWalkState(this);
        Crouch = new CrouchState(this);
        Prone = new ProneState(this);
        Seated = new SeatedState(this);
    }

    private void Start()
    {
        WeaponAimRig = RigLayerWeaponAim.GetComponent<Rig>();
        BodyAimRig = RigLayerBodyAim.GetComponent<Rig>();
        ChangeState(Walk);
    }

    private void Update()
    {
        ReadInput();

        if (CurrentState.CanAim && Input.GetButton("Fire2"))
            AimOn();
        else
            aiming = false;

        CurrentState.Tick();
        UpdateAimWeights();

        if (aiming)
            OnAimAnimations();
        Animation.SetLocomotion(MoveInput.magnitude * CurrentState.Gait, CurrentState.Stance);
    }

    private void FixedUpdate()
    {
        CurrentState.FixedTick();
    }

    public void ChangeState(PlayerState next)
    {
        if (next == CurrentState)
            return;
        CurrentState?.Exit();
        CurrentState = next;
        CurrentState.Enter();
    }

    // ------------------------------------------------------------------ Tren koltugu
    // TrainBoarding oturtup kaldirirken cagirir.
    public void EnterSeat()
    {
        aiming = false;
        aimBlend = 0f;
        UpdateAimWeights();
        MoveInput = Vector3.zero;
        ChangeState(Seated);
    }

    public void ExitSeat()
    {
        ChangeState(Walk);
    }

    // ------------------------------------------------------------------ Hareket
    private void ReadInput()
    {
        var input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);

        // WASD kameranin baktigi yone gore: W ekranda yukari.
        Camera cam = Camera.main;
        if (cam != null)
            input = Quaternion.Euler(0f, cam.transform.eulerAngles.y, 0f) * input;
        MoveInput = input;

        SprintHeld = Input.GetKey(sprintKey);
        SprintPressed = Input.GetKeyDown(sprintKey);
        SlowWalkHeld = Input.GetKey(slowWalkKey);
        CrouchPressed = Input.GetKeyDown(crouchKey);
        PronePressed = Input.GetKeyDown(proneKey);
    }

    // Durumlar FixedUpdate'te cagirir: yatay hizi ayarlar ve gidilen yone doner.
    public void Move(Vector3 velocity, float degreesPerSecond)
    {
        rb.linearVelocity = new Vector3(velocity.x, rb.linearVelocity.y, velocity.z);
        if (aiming || velocity.sqrMagnitude < 0.0001f)
            return;

        Quaternion target = Quaternion.LookRotation(new Vector3(velocity.x, 0f, velocity.z));
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.fixedDeltaTime);
    }

    // ------------------------------------------------------------------ Collider
    // Ayaklar ayni yukseklikte kalacak sekilde capsule'u durusa gore degistirir.
    public void SetBodyShape(PlayerStance stance)
    {
        GetShape(stance, out int direction, out float height, out Vector3 center);
        capsule.direction = direction;
        capsule.height = height;
        capsule.center = center;
    }

    // Bu durusa gececek yer var mi (ornegin comelmisken ustte tavan).
    public bool CanFit(PlayerStance stance)
    {
        GetShape(stance, out int direction, out float height, out Vector3 center);
        float radius = capsule.radius * 0.95f;
        Vector3 axis = direction == 2 ? transform.forward : transform.up;
        Vector3 middle = transform.TransformPoint(center);
        float half = Mathf.Max(0f, height / 2f - radius);
        // Ayaklarin altindaki zemine degmemek icin biraz yukaridan bak.
        Vector3 lift = Vector3.up * 0.05f;
        foreach (var hit in Physics.OverlapCapsule(middle - axis * half + lift, middle + axis * half + lift, radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (!hit.transform.IsChildOf(transform))
                return false;
        }
        return true;
    }

    private void GetShape(PlayerStance stance, out int direction, out float height, out Vector3 center)
    {
        switch (stance)
        {
            case PlayerStance.Crouching:
                direction = 1;
                height = crouchHeight;
                center = new Vector3(standCenter.x, feetY + crouchHeight / 2f, standCenter.z);
                break;
            case PlayerStance.Prone:
                direction = 2;
                height = proneLength;
                center = new Vector3(standCenter.x, feetY + capsule.radius, standCenter.z);
                break;
            default:
                direction = 1;
                height = standHeight;
                center = standCenter;
                break;
        }
    }

    // ------------------------------------------------------------------ Nisan
    public bool aiming = false;

    private void OnAimAnimations()
    {
        var (success, position) = GetMousePosition();
        if (!success)
            return;

        float MouseDirecX = Mathf.Clamp(position.x - transform.position.x, -1f, 1f);
        float MouseDirecZ = Mathf.Clamp(position.z - transform.position.z, -1f, 1f);
        Animation.Animator.SetFloat("MouseDirecx", MouseDirecX);
        Animation.Animator.SetFloat("MouseDirecz", MouseDirecZ);
        Animation.Animator.SetFloat("Velocityx", rb.linearVelocity.x);
        Animation.Animator.SetFloat("Velocityz", rb.linearVelocity.z);
    }

    private (bool success, Vector3 position) GetMousePosition()
    {
        Ray mousepos = Camera.main.ScreenPointToRay(Input.mousePosition);

        int playerLayer = LayerMask.NameToLayer("Player");
        int layerMask = ~(1 << playerLayer);

        if (Physics.Raycast(mousepos, out var hitInfo, Mathf.Infinity, layerMask))
            return (success: true, position: hitInfo.point);
        return (success: false, position: Vector3.zero);
    }

    private void AimOn()
    {
        aiming = true;
        var (success, position) = GetMousePosition();
        if (success)
        {
            LookAtObject.transform.position = position;
            var Direction = position - transform.position;
            Direction.y = 0;
            transform.forward = Direction;
            transform.Rotate(new Vector3(0, 31.2f, 0));
        }
    }

    // Rig'ler ve animator katmani tek bir blend degerinden beslenir; nisan yarida birakilirsa blend kaldigi yerden geri doner.
    private float aimBlend;
    private void UpdateAimWeights()
    {
        float target = aiming ? 1f : 0f;
        aimBlend = ADSSpeed > 0f ? Mathf.MoveTowards(aimBlend, target, Time.deltaTime / ADSSpeed) : target;

        float weight = Mathf.SmoothStep(0f, 1f, aimBlend);
        if (WeaponAimRig != null)
            WeaponAimRig.weight = weight;
        if (BodyAimRig != null)
            BodyAimRig.weight = weight;
        Animation.SetAimWeight(weight, CurrentState != null ? CurrentState.Stance : PlayerStance.Standing, weapons != null && weapons.hasWeapon);
    }
}
