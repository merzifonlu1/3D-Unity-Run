using UnityEngine;

// Oyuncunun trene binmesi, icinde gezmesi, koltuklara oturmasi ve inmesi.
// Disaridayken kapinin yaninda E: trene biner.
// Icerideyken koltugun yaninda E: oturur, kapinin yaninda E: iner.
// Otururken E: kalkar. Kondukter koltugunda oturulurken tren surulebilir;
// kondukter kalkinca tren fren yapip durur.
[RequireComponent(typeof(TrainController))]
public class TrainBoarding : MonoBehaviour
{
    private enum State { Outside, Inside, Seated }

    [SerializeField] private PlayerMovement player;
    [Tooltip("Kapilar. Her kapinin ileri yonu (mavi ok) trenin disina bakmali.")]
    [SerializeField] private Transform[] doors = new Transform[0];
    [Tooltip("Oyuncu icerideyken gizlenen parcalar (tavan, ust duvarlar, camlar) ki ic mekan yukaridan gorunsun")]
    [SerializeField] private Renderer[] hideWhenInside = new Renderer[0];
    [Tooltip("Zeminin ust yuzeyinin trenin tabanina gore yuksekligi")]
    [SerializeField] private float floorHeight = 0.2f;
    [SerializeField] private float doorRange = 2.2f;
    [SerializeField] private float seatRange = 1.2f;

    private TrainController controller;
    private TrainVision vision;
    private TrainSeat[] seats;
    private Rigidbody playerBody;
    private Collider playerCollider;
    private Animator playerAnimator;
    private WeaponEquip playerWeapons;
    private Transform playerParent;

    private State state = State.Outside;
    private TrainSeat currentSeat;
    private string prompt;

    public bool PlayerInside => state != State.Outside;

    public void Configure(Transform[] doorPoints, Renderer[] cutaway, float floor)
    {
        doors = doorPoints;
        hideWhenInside = cutaway;
        floorHeight = floor;
    }

    private void Awake()
    {
        controller = GetComponent<TrainController>();
        vision = GetComponent<TrainVision>();
        seats = GetComponentsInChildren<TrainSeat>();
        if (player == null)
            player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
        {
            playerBody = player.GetComponent<Rigidbody>();
            playerCollider = player.GetComponent<Collider>();
            playerAnimator = player.GetComponentInChildren<Animator>();
            playerWeapons = player.GetComponent<WeaponEquip>();
        }
    }

    private void OnEnable()
    {
        controller.Moved += CarryPassenger;
    }

    private void OnDisable()
    {
        controller.Moved -= CarryPassenger;
    }

    private void Update()
    {
        prompt = null;
        if (player == null)
            return;

        switch (state)
        {
            case State.Outside:
                if (NearestDoor(outside: true) != null)
                    prompt = "E: Trene bin";
                break;
            case State.Inside:
                TrainSeat seat = NearestFreeSeat();
                if (seat != null)
                    prompt = $"E: Otur ({seat.DisplayName})";
                else if (NearestDoor(outside: false) != null)
                    prompt = controller.Speed != 0f ? "Tren durmadan inilemez" : "E: Trenden in";
                break;
            case State.Seated:
                if (!currentSeat.IsDriver)
                    prompt = $"{currentSeat.DisplayName}   E: Kalk";
                break;
        }

        if (Input.GetButtonDown("Interact"))
            Interact();
    }

    // E tusunun yaptigi is: duruma gore bin / otur / kalk / in.
    public void Interact()
    {
        if (player == null)
            return;

        switch (state)
        {
            case State.Outside:
            {
                Transform door = NearestDoor(outside: true);
                if (door != null)
                    Board(door);
                break;
            }
            case State.Inside:
            {
                TrainSeat seat = NearestFreeSeat();
                Transform door = NearestDoor(outside: false);
                if (seat != null)
                    Sit(seat);
                else if (door != null && controller.Speed == 0f)
                    Leave(door);
                break;
            }
            case State.Seated:
                StandUp();
                break;
        }
    }

    private void Board(Transform door)
    {
        Vector3 inside = door.position - door.forward * 0.9f;
        inside.y = transform.TransformPoint(0f, floorHeight, 0f).y;
        TeleportPlayer(inside);
        SetCutaway(true);
        if (vision != null)
            vision.Show(null);
        state = State.Inside;
    }

    private void Leave(Transform door)
    {
        Vector3 outside = door.position + door.forward * 1.0f;
        outside.y = GroundHeight(outside);
        TeleportPlayer(outside);
        SetCutaway(false);
        if (vision != null)
            vision.Hide();
        state = State.Outside;
    }

    private void Sit(TrainSeat seat)
    {
        if (playerAnimator != null)
        {
            playerAnimator.SetBool("Walking", false);
            playerAnimator.SetBool("Running", false);
        }
        player.aiming = false;
        player.enabled = false;
        if (playerWeapons != null)
            playerWeapons.enabled = false;

        playerBody.linearVelocity = Vector3.zero;
        playerBody.isKinematic = true;
        playerParent = player.transform.parent;
        player.transform.SetParent(seat.SitPoint, true);
        player.transform.localPosition = new Vector3(0f, FeetOffset(), 0f);
        player.transform.localRotation = Quaternion.identity;

        seat.Occupied = true;
        currentSeat = seat;
        if (seat.IsDriver)
            controller.SetDriver(true);
        if (vision != null)
            vision.Show(seat);
        state = State.Seated;
    }

    private void StandUp()
    {
        Transform sit = currentSeat.SitPoint;
        Vector3 standPosition = sit.position + sit.forward * 0.7f;
        standPosition.y = transform.TransformPoint(0f, floorHeight, 0f).y;

        player.transform.SetParent(playerParent, true);
        playerBody.isKinematic = false;
        TeleportPlayer(standPosition);
        player.enabled = true;
        if (playerWeapons != null)
            playerWeapons.enabled = true;

        if (currentSeat.IsDriver)
            controller.SetDriver(false);
        currentSeat.Occupied = false;
        currentSeat = null;
        if (vision != null)
            vision.Show(null);
        state = State.Inside;
    }

    // Kondukter kalktiktan sonra tren fren yaparken icerdeki oyuncu trenle birlikte tasinir.
    private void CarryPassenger(Vector3 oldPosition, Quaternion oldRotation, Vector3 newPosition, Quaternion newRotation)
    {
        if (state != State.Inside || playerBody == null)
            return;

        Quaternion delta = newRotation * Quaternion.Inverse(oldRotation);
        playerBody.position = newPosition + delta * (playerBody.position - oldPosition);
        playerBody.rotation = delta * playerBody.rotation;
    }

    // feetY: ayaklarin basacagi yukseklik.
    private void TeleportPlayer(Vector3 feetPosition)
    {
        Vector3 position = feetPosition + Vector3.up * FeetOffset();
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
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform) || hit.transform.IsChildOf(player.transform))
                continue;
            return hit.point.y;
        }
        return transform.position.y;
    }

    private Transform NearestDoor(bool outside)
    {
        Transform best = null;
        float bestDistance = doorRange;
        foreach (var door in doors)
        {
            Vector3 side = door.position + door.forward * (outside ? 0.6f : -0.6f);
            float distance = FlatDistance(player.transform.position, side);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = door;
            }
        }
        return best;
    }

    private TrainSeat NearestFreeSeat()
    {
        TrainSeat best = null;
        float bestDistance = seatRange;
        foreach (var seat in seats)
        {
            if (seat.Occupied)
                continue;
            float distance = FlatDistance(player.transform.position, seat.SitPoint.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = seat;
            }
        }
        return best;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private void SetCutaway(bool inside)
    {
        foreach (var r in hideWhenInside)
        {
            if (r != null)
                r.forceRenderingOff = inside;
        }
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(prompt))
            return;

        var style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter };
        GUI.Box(new Rect(Screen.width / 2f - 150f, Screen.height - 190f, 300f, 34f), prompt, style);
    }
}
