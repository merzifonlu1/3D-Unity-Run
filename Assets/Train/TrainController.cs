using UnityEngine;

// Treni rota uzerinde sadece ileri ve geri hareket ettirir.
// Vitesler: -1 (geri), 0 (dur), 1, 2, 3 (ileri). W vites yukseltir, S dusurur.
// Hiz, yeni vitesin hizina aniden degil yavasca ulasir.
// Tren sadece kondukter koltugunda biri varken (HasDriver) vites alir; surucu
// yoksa vites 0'a alinir ve tren fren yaparak durur.
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
    [SerializeField] private bool showControlPanel = true;

    public int Gear { get; private set; }
    public float Speed { get; private set; }
    public float Distance { get; private set; }
    public bool HasDriver { get; private set; }

    // Her fizik adiminda cagrilir: (eski konum, eski donus, yeni konum, yeni donus).
    // Trenin icinde yuruyen yolcular bununla trenle birlikte tasinir.
    public event System.Action<Vector3, Quaternion, Vector3, Quaternion> Moved;

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
        if (!HasDriver)
            return;

        if (Input.GetKeyDown(KeyCode.W))
            ShiftUp();
        if (Input.GetKeyDown(KeyCode.S))
            ShiftDown();
    }

    public void SetDriver(bool hasDriver)
    {
        HasDriver = hasDriver;
        if (!hasDriver)
            Gear = 0;
    }

    public void ShiftUp()
    {
        if (!HasDriver)
            return;
        Gear = Mathf.Min(Gear + 1, MaxGear);
    }

    public void ShiftDown()
    {
        if (!HasDriver)
            return;
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

        Vector3 oldPosition = rb.position;
        Quaternion oldRotation = rb.rotation;
        route.Evaluate(Distance, out Vector3 position, out Vector3 forward);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        rb.MovePosition(position);
        rb.MoveRotation(rotation);
        Moved?.Invoke(oldPosition, oldRotation, position, rotation);
    }

    private void OnGUI()
    {
        if (!showControlPanel || !HasDriver)
            return;

        // Kontrol ekrani: sadece kondukter koltugunda oturulurken gorunur.
        var area = new Rect(Screen.width / 2f - 160f, Screen.height - 130f, 320f, 110f);
        GUI.Box(area, "Tren Kontrol");
        GUILayout.BeginArea(new Rect(area.x + 12f, area.y + 24f, area.width - 24f, area.height - 30f));
        GUILayout.BeginHorizontal();
        for (int g = MinGear; g <= MaxGear; g++)
        {
            string label = g == Gear ? $"[{GearName(g)}]" : GearName(g);
            GUILayout.Label(label, GUILayout.Width(52f));
        }
        GUILayout.EndHorizontal();
        GUILayout.Label($"Hiz: {Speed * 3.6f:0} km/sa");
        GUILayout.Label("W: vites yukselt   S: vites dusur   E: kalk");
        GUILayout.EndArea();
    }

    private static string GearName(int gear)
    {
        return gear < 0 ? "G" : gear == 0 ? "N" : gear.ToString();
    }
}
