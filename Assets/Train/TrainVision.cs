using UnityEngine;

// Oyuncu treni surerken trenin disini karartir. Trenin kendisi her zaman gorunur;
// disarisi sadece surucu kabininin gorus acilarindan (on cam ve yan camlarin bir kismi) gorunur.
// Karartmayi URP renderer'daki "Train Vision" Full Screen Pass (TrainVision.shader) cizer;
// bu bilesen sadece shader'in okudugu global degerleri gunceller.
public class TrainVision : MonoBehaviour
{
    [System.Serializable]
    public struct ViewSector
    {
        [Tooltip("Trenin onune gore aci (derece). 0 = on, 90 = sag, -90 = sol, 180 = arka")]
        public float centerAngle;
        [Tooltip("Gorus acisinin genisligi (derece)")]
        public float width;

        public ViewSector(float centerAngle, float width)
        {
            this.centerAngle = centerAngle;
            this.width = width;
        }
    }

    private const int MaxSectors = 4;

    private static readonly int OnId = Shader.PropertyToID("_TrainVisionOn");
    private static readonly int WorldToLocalId = Shader.PropertyToID("_TrainVisionWorldToLocal");
    private static readonly int HullId = Shader.PropertyToID("_TrainVisionHull");
    private static readonly int OriginId = Shader.PropertyToID("_TrainVisionOrigin");
    private static readonly int SectorsId = Shader.PropertyToID("_TrainVisionSectors");

    [Tooltip("Trenin yari genisligi (x) ve yari uzunlugu (z), metre")]
    [SerializeField] private Vector2 hullHalfSize = new Vector2(1.1f, 5.2f);
    [Tooltip("Surucu kabininden gorulen acilar (en fazla 4)")]
    [SerializeField] private ViewSector[] viewSectors =
    {
        new ViewSector(0f, 70f),    // on cam
        new ViewSector(-62f, 22f),  // sol kabin cami (kismi)
        new ViewSector(62f, 22f),   // sag kabin cami (kismi)
    };
    [Tooltip("Kabinden ne kadar uzagi gorebilir (metre)")]
    [SerializeField] private float viewDistance = 50f;
    [Tooltip("Gorulmeyen yerlerin ne kadar karanlik oldugu (0-1)")]
    [SerializeField, Range(0f, 1f)] private float darkness = 0.96f;
    [Tooltip("Karartmanin acilip kapanma hizi (saniyede)")]
    [SerializeField] private float fadeSpeed = 3f;

    private readonly Vector4[] sectors = new Vector4[MaxSectors];
    private Transform viewPoint;
    private float strength;

    public void SetHullHalfSize(Vector2 halfSize)
    {
        hullHalfSize = halfSize;
    }

    // point: gorusun basladigi yer (surucu kabini).
    public void Show(Transform point)
    {
        viewPoint = point;
    }

    public void Hide()
    {
        viewPoint = null;
    }

    private void LateUpdate()
    {
        strength = Mathf.MoveTowards(strength, viewPoint != null ? darkness : 0f, fadeSpeed * Time.deltaTime);
        Shader.SetGlobalFloat(OnId, strength);
        if (strength <= 0f || viewPoint == null)
            return;

        Shader.SetGlobalMatrix(WorldToLocalId, transform.worldToLocalMatrix);
        Shader.SetGlobalVector(HullId, hullHalfSize);

        int count = 0;
        foreach (var sector in viewSectors)
        {
            if (count == MaxSectors)
                break;
            sectors[count++] = new Vector4(sector.centerAngle * Mathf.Deg2Rad, sector.width * 0.5f * Mathf.Deg2Rad, 0f, 0f);
        }
        for (int i = count; i < sectors.Length; i++)
            sectors[i] = Vector4.zero;

        Vector3 origin = transform.InverseTransformPoint(viewPoint.position);
        Shader.SetGlobalVector(OriginId, new Vector4(origin.x, origin.z, viewDistance, count));
        Shader.SetGlobalVectorArray(SectorsId, sectors);
    }

    private void OnDisable()
    {
        // Global degerler play mode bitince de kalir; sahne karanlik kalmasin.
        strength = 0f;
        Shader.SetGlobalFloat(OnId, 0f);
    }

    private void OnDrawGizmosSelected()
    {
        Transform point = viewPoint;
        if (point == null && TryGetComponent(out TrainBoarding boarding))
            point = boarding.DriverPoint;
        if (point == null)
            return;

        Vector3 origin = point.position + Vector3.up * 0.5f;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        foreach (var sector in viewSectors)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = sector.centerAngle + side * sector.width / 2f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * transform.forward;
                Gizmos.DrawLine(origin, origin + dir * viewDistance);
            }
        }
    }
}
