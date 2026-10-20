using System.Collections.Generic;
using UnityEngine;

// Trenin gidecegi rota. Alt objeler (waypoint) sirasiyla Catmull-Rom egrisiyle
// birlestirilir, boylece rota duz cizgi degil viraj da olabilir.
public class TrainRoute : MonoBehaviour
{
    [SerializeField, Min(2)] private int samplesPerSegment = 16;

    private readonly List<Vector3> points = new List<Vector3>();
    private readonly List<float> distances = new List<float>();

    public float Length { get; private set; }

    // OnEnable: play mode'da kod yeniden derlenince de rota yeniden kurulur.
    private void OnEnable()
    {
        Build();
    }

    public void Build()
    {
        points.Clear();
        distances.Clear();
        Length = 0f;

        int count = transform.childCount;
        if (count < 2)
            return;

        for (int i = 0; i < count - 1; i++)
        {
            Vector3 p0 = Waypoint(i - 1);
            Vector3 p1 = Waypoint(i);
            Vector3 p2 = Waypoint(i + 1);
            Vector3 p3 = Waypoint(i + 2);
            for (int s = 0; s < samplesPerSegment; s++)
                points.Add(CatmullRom(p0, p1, p2, p3, (float)s / samplesPerSegment));
        }
        points.Add(Waypoint(count - 1));

        distances.Add(0f);
        for (int i = 1; i < points.Count; i++)
            distances.Add(distances[i - 1] + Vector3.Distance(points[i - 1], points[i]));
        Length = distances[distances.Count - 1];
    }

    // Rotanin basindan 'distance' metre ilerideki nokta ve o noktadaki yon.
    public void Evaluate(float distance, out Vector3 position, out Vector3 forward)
    {
        if (points.Count < 2)
        {
            position = transform.position;
            forward = transform.forward;
            return;
        }

        distance = Mathf.Clamp(distance, 0f, Length);

        int low = 0;
        int high = distances.Count - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (distances[mid] <= distance)
                low = mid;
            else
                high = mid;
        }

        float segmentLength = distances[high] - distances[low];
        float t = segmentLength > 0f ? (distance - distances[low]) / segmentLength : 0f;
        position = Vector3.Lerp(points[low], points[high], t);
        forward = (points[high] - points[low]).normalized;
    }

    private Vector3 Waypoint(int index)
    {
        return transform.GetChild(Mathf.Clamp(index, 0, transform.childCount - 1)).position;
    }

    private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            Build();

        Gizmos.color = Color.yellow;
        for (int i = 1; i < points.Count; i++)
            Gizmos.DrawLine(points[i - 1], points[i]);

        Gizmos.color = Color.cyan;
        for (int i = 0; i < transform.childCount; i++)
            Gizmos.DrawSphere(transform.GetChild(i).position, 0.4f);
    }
}
