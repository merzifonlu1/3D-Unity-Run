using UnityEngine;

// Oyuncunun trene binip surmesi ve inmesi.
// Trenin yaninda E: kabine biner ve hemen surmeye baslar (kontrol paneli acilir).
// Tren dururken E: indigi taraftan trenden iner.
// Surucu yokken tren vites alamaz; bu yuzden oyuncu inince tren hareket etmez.
[RequireComponent(typeof(TrainController))]
public class TrainBoarding : MonoBehaviour
{
    [SerializeField] private PlayerMovement player;
    [Tooltip("Surucunun oturdugu nokta (kabin). Ileri yonu trenin onune bakmali.")]
    [SerializeField] private Transform driverPoint;
    [Tooltip("Trenin yari genisligi (x) ve yari uzunlugu (z), metre")]
    [SerializeField] private Vector2 bodyHalfSize = new Vector2(1.1f, 5.2f);
    [Tooltip("Trene binmek icin trenin kenarina ne kadar yakin olmak gerekir (metre)")]
    [SerializeField] private float boardRange = 1.5f;

    private TrainController controller;
    private TrainVision vision;
    private Rigidbody playerBody;
    private Collider playerCollider;
    private WeaponEquip playerWeapons;
    private Renderer[] playerRenderers;
    private Transform playerParent;

    private bool driving;
    private float exitSide = -1f; // -1 sol, 1 sag: oyuncu bindigi taraftan iner
    private float feetOffset;
    private string prompt;

    public Transform DriverPoint => driverPoint;

    public void Configure(Transform driver, Vector2 halfSize)
    {
        driverPoint = driver;
        bodyHalfSize = halfSize;
    }

    private void Awake()
    {
        controller = GetComponent<TrainController>();
        vision = GetComponent<TrainVision>();
        if (player == null)
            player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody>();
            playerCollider = player.GetComponent<Collider>();
            playerWeapons = player.GetComponent<WeaponEquip>();
        }
    }

    private void Update()
    {
        prompt = null;
        if (player == null || driverPoint == null)
            return;

        // Surerken ipuclarini kontrol paneli gosterir.
        if (!driving && PlayerNearby())
            prompt = "E: Trene bin ve sur";

        if (Input.GetButtonDown("Interact"))
            Interact();
    }

    // E tusunun yaptigi is: duruma gore bin ya da in.
    public void Interact()
    {
        if (player == null || driverPoint == null)
            return;

        if (!driving)
        {
            if (PlayerNearby())
                Board();
        }
        else if (controller.Speed == 0f)
        {
            Leave();
        }
    }

    private void Board()
    {
        exitSide = transform.InverseTransformPoint(player.transform.position).x < 0f ? -1f : 1f;

        feetOffset = FeetOffset();
        player.EnterSeat();
        if (playerWeapons != null)
            playerWeapons.enabled = false;

        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        if (playerCollider != null)
            playerCollider.enabled = false;
        playerParent = player.transform.parent;
        player.transform.SetParent(driverPoint, true);
        player.transform.localPosition = Vector3.zero;
        player.transform.localRotation = Quaternion.identity;

        // Oyuncu kabinin icinde; modelin icinden gorunmesin.
        playerRenderers = player.GetComponentsInChildren<Renderer>();
        SetPlayerVisible(false);

        controller.SetDriver(true);
        if (vision != null)
            vision.Show(driverPoint);
        driving = true;
    }

    private void Leave()
    {
        Vector3 outside = transform.TransformPoint(exitSide * (bodyHalfSize.x + 1f), 0f, transform.InverseTransformPoint(driverPoint.position).z);
        outside.y = GroundHeight(outside);

        player.transform.SetParent(playerParent, true);
        player.transform.rotation = Quaternion.LookRotation(transform.right * exitSide, Vector3.up);
        if (playerCollider != null)
            playerCollider.enabled = true;
        playerBody.isKinematic = false;
        TeleportPlayer(outside);
        SetPlayerVisible(true);
        player.ExitSeat();
        if (playerWeapons != null)
            playerWeapons.enabled = true;

        controller.SetDriver(false);
        if (vision != null)
            vision.Hide();
        driving = false;
    }

    private bool PlayerNearby()
    {
        Vector3 local = transform.InverseTransformPoint(player.transform.position);
        return Mathf.Abs(local.x) <= bodyHalfSize.x + boardRange && Mathf.Abs(local.z) <= bodyHalfSize.y + 0.5f;
    }

    private void SetPlayerVisible(bool visible)
    {
        foreach (var r in playerRenderers)
        {
            if (r != null)
                r.forceRenderingOff = !visible;
        }
    }

    // feetPosition: ayaklarin basacagi nokta.
    private void TeleportPlayer(Vector3 feetPosition)
    {
        Vector3 position = feetPosition + Vector3.up * feetOffset;
        playerBody.linearVelocity = Vector3.zero;
        playerBody.position = position;
        player.transform.position = position;
    }

    // Oyuncunun pivotu ile ayaklari arasindaki yukseklik farki.
    private float FeetOffset()
    {
        if (playerCollider == null)
            return 0f;
        return player.transform.position.y - playerCollider.bounds.min.y;
    }

    private float GroundHeight(Vector3 point)
    {
        var origin = point + Vector3.up * 5f;
        float height = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform))
                continue;
            height = Mathf.Max(height, hit.point.y); // RaycastAll sirasiz doner; en ustteki yuzey
        }
        return float.IsNegativeInfinity(height) ? transform.position.y : height;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(prompt))
            return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 150f, Screen.height - 190f, 300f, 34f), prompt, style);
    }
}
