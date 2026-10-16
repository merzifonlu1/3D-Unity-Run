using UnityEngine;

// Trendeki bir koltuk. Oyuncu oturdugunda bu koltugun gorus acilarindan
// disarisi gorunur, geri kalan her yer kapanir.
// Kondukter koltugu (isDriver) ayrica treni surme yetkisi verir.
public class TrainSeat : MonoBehaviour
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

    public const int MaxSectors = 4;

    [SerializeField] private string displayName = "Koltuk";
    [SerializeField] private bool isDriver;
    [Tooltip("Oyuncunun oturdugu nokta (bos birakilirsa koltugun kendisi)")]
    [SerializeField] private Transform sitPoint;
    [SerializeField] private ViewSector[] viewSectors = new ViewSector[0];
    [Tooltip("Bu koltuktan ne kadar uzagi gorebilir (metre)")]
    [SerializeField] private float viewDistance = 45f;

    public string DisplayName => displayName;
    public bool IsDriver => isDriver;
    public Transform SitPoint => sitPoint != null ? sitPoint : transform;
    public ViewSector[] ViewSectors => viewSectors;
    public float ViewDistance => viewDistance;
    public bool Occupied { get; set; }

    public void Configure(string name, bool driver, Transform sit, ViewSector[] sectors, float distance)
    {
        displayName = name;
        isDriver = driver;
        sitPoint = sit;
        viewSectors = sectors;
        viewDistance = distance;
    }

    private void OnDrawGizmosSelected()
    {
        Transform train = GetComponentInParent<TrainController>() != null
            ? GetComponentInParent<TrainController>().transform
            : transform;
        Vector3 origin = SitPoint.position + Vector3.up * 0.5f;
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.8f);
        foreach (var sector in viewSectors)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = sector.centerAngle + side * sector.width / 2f;
                Vector3 dir = Quaternion.AngleAxis(angle, Vector3.up) * train.forward;
                Gizmos.DrawLine(origin, origin + dir * viewDistance);
            }
        }
    }
}
