using UnityEngine;

// Treni rota uzerinde sadece ileri ve geri hareket ettirir.
// Vitesler: -1 (geri), 0 (dur), 1, 2, 3 (ileri). W vites yukseltir, S dusurur.
// Hiz, yeni vitesin hizina aniden degil yavasca ulasir.
[RequireComponent(typeof(Rigidbody))]
public class TrainController : MonoBehaviour
{
    public const int MinGear = -1;
    public const int MaxGear = 3;

    [SerializeField] private TrainRoute route;
    [Tooltip("Her vitesin hizi (m/s), sirasiyla -1, 0, 1, 2, 3")]
    [SerializeField] private float[] gearSpeeds = { -3f, 0f, 4f, 8f, 13f };
    [Tooltip("Hizlanma (m/s^2)")]
    [SerializeField] private float acceleration = 1.5f;
    [Tooltip("Yavaslama (m/s^2)")]
    [SerializeField] private float braking = 3f;
    [SerializeField] private float startDistance;
    [SerializeField] private bool acceptInput = true;
    [SerializeField] private bool showDebugHud = true;

    public int Gear { get; private set; }
    public float Speed { get; private set; }
    public float Distance { get; private set; }

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        Distance = startDistance;
    }

    private void Start()
    {
        if (route == null)
        {
            Debug.LogError("TrainController: route atanmamis.", this);
            enabled = false;
            return;
        }

        route.Evaluate(Distance, out Vector3 position, out Vector3 forward);
        transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));
    }

    private void Update()
    {
        if (!acceptInput)
            return;

        if (Input.GetKeyDown(KeyCode.W))
            ShiftUp();
        if (Input.GetKeyDown(KeyCode.S))
            ShiftDown();
    }

    public void ShiftUp()
    {
        Gear = Mathf.Min(Gear + 1, MaxGear);
    }

    public void ShiftDown()
    {
        Gear = Mathf.Max(Gear - 1, MinGear);
    }

    private void FixedUpdate()
    {
        float targetSpeed = gearSpeeds[Gear - MinGear];
        // Hiz sifira yaklasiyorsa (yavaslama ya da yon degisimi) fren degeri kullanilir.
        bool slowingDown = targetSpeed * Speed < 0f || Mathf.Abs(targetSpeed) < Mathf.Abs(Speed);
        float rate = slowingDown ? braking : acceleration;
        Speed = Mathf.MoveTowards(Speed, targetSpeed, rate * Time.fixedDeltaTime);

        Distance += Speed * Time.fixedDeltaTime;
        if ((Distance <= 0f && Speed < 0f) || (Distance >= route.Length && Speed > 0f))
        {
            // Rotanin sonuna gelince tren durur ve bosa alinir.
            Distance = Mathf.Clamp(Distance, 0f, route.Length);
            Speed = 0f;
            Gear = 0;
        }

        route.Evaluate(Distance, out Vector3 position, out Vector3 forward);
        rb.MovePosition(position);
        rb.MoveRotation(Quaternion.LookRotation(forward, Vector3.up));
    }

    private void OnGUI()
    {
        if (!showDebugHud)
            return;

        GUI.Label(new Rect(10, 10, 300, 22), $"Vites: {Gear}   Hiz: {Speed:0.0} m/s");
    }
}
