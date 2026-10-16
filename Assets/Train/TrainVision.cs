using UnityEngine;

// Oyuncu trenin icindeyken trenin disini karartir. Trenin ici her zaman gorunur;
// disarisi sadece oturulan koltugun gorus acilarindan (TrainSeat.ViewSectors) gorunur.
// Ayakta durulurken disarisi tamamen kapalidir.
// Karartmayi URP renderer'daki "Train Vision" Full Screen Pass (TrainVision.shader) cizer;
// bu bilesen sadece shader'in okudugu global degerleri gunceller.
public class TrainVision : MonoBehaviour
{
    private static readonly int OnId = Shader.PropertyToID("_TrainVisionOn");
    private static readonly int WorldToLocalId = Shader.PropertyToID("_TrainVisionWorldToLocal");
    private static readonly int HullId = Shader.PropertyToID("_TrainVisionHull");
    private static readonly int OriginId = Shader.PropertyToID("_TrainVisionOrigin");
    private static readonly int SectorsId = Shader.PropertyToID("_TrainVisionSectors");

    [Tooltip("Trenin ic alaninin yari genisligi (x) ve yari uzunlugu (z), metre")]
    [SerializeField] private Vector2 hullHalfSize = new Vector2(1.75f, 6f);
    [Tooltip("Gorulmeyen yerlerin ne kadar karanlik oldugu (0-1)")]
    [SerializeField, Range(0f, 1f)] private float darkness = 0.96f;
    [Tooltip("Karartmanin acilip kapanma hizi (saniyede)")]
    [SerializeField] private float fadeSpeed = 3f;

    private readonly Vector4[] sectors = new Vector4[TrainSeat.MaxSectors];
    private bool active;
    private TrainSeat seat;
    private float strength;

    public void SetHullHalfSize(Vector2 halfSize)
    {
        hullHalfSize = halfSize;
    }

    // seat == null: ayakta, disarisi kapali.
    public void Show(TrainSeat currentSeat)
    {
        active = true;
        seat = currentSeat;
    }

    public void Hide()
    {
        active = false;
        seat = null;
    }

    private void LateUpdate()
    {
        strength = Mathf.MoveTowards(strength, active ? darkness : 0f, fadeSpeed * Time.deltaTime);
        Shader.SetGlobalFloat(OnId, strength);
        if (strength <= 0f)
            return;

        Shader.SetGlobalMatrix(WorldToLocalId, transform.worldToLocalMatrix);
        Shader.SetGlobalVector(HullId, hullHalfSize);

        int count = 0;
        Vector3 origin = Vector3.zero;
        float distance = 0f;
        if (seat != null)
        {
            origin = transform.InverseTransformPoint(seat.SitPoint.position);
            distance = seat.ViewDistance;
            foreach (var sector in seat.ViewSectors)
            {
                if (count == TrainSeat.MaxSectors)
                    break;
                sectors[count++] = new Vector4(sector.centerAngle * Mathf.Deg2Rad, sector.width * 0.5f * Mathf.Deg2Rad, 0f, 0f);
            }
        }
        for (int i = count; i < sectors.Length; i++)
            sectors[i] = Vector4.zero;

        Shader.SetGlobalVector(OriginId, new Vector4(origin.x, origin.z, distance, count));
        Shader.SetGlobalVectorArray(SectorsId, sectors);
    }

    private void OnDisable()
    {
        // Global degerler play mode bitince de kalir; sahne karanlik kalmasin.
        strength = 0f;
        Shader.SetGlobalFloat(OnId, 0f);
    }
}
