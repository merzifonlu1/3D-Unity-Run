using UnityEditor;
using UnityEngine;

// Tools > Train > Create Test Train: acik sahneye virajli bir test rotasi ve
// ici bos dikdortgen prizma seklinde bir tren ekler.
public static class TrainSetupMenu
{
    private static readonly Vector2[] RoutePoints =
    {
        new Vector2(-20f, -30f),
        new Vector2(-20f, 0f),
        new Vector2(-12f, 20f),
        new Vector2(5f, 32f),
        new Vector2(25f, 35f),
        new Vector2(40f, 25f),
        new Vector2(45f, 0f),
    };

    private const float CarLength = 12f;
    private const float CarWidth = 3.5f;
    private const float CarHeight = 3.2f;
    private const float WallThickness = 0.2f;

    [MenuItem("Tools/Train/Create Test Train")]
    private static void CreateTestTrain()
    {
        var routeObject = new GameObject("TrainRoute");
        Undo.RegisterCreatedObjectUndo(routeObject, "Create Test Train");
        var route = routeObject.AddComponent<TrainRoute>();
        for (int i = 0; i < RoutePoints.Length; i++)
        {
            var waypoint = new GameObject($"Waypoint {i}");
            waypoint.transform.SetParent(routeObject.transform);
            waypoint.transform.position = GroundPoint(RoutePoints[i]);
        }

        var train = new GameObject("Train");
        Undo.RegisterCreatedObjectUndo(train, "Create Test Train");
        train.transform.position = routeObject.transform.GetChild(0).position;
        train.AddComponent<Rigidbody>().isKinematic = true;
        var controller = train.AddComponent<TrainController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("route").objectReferenceValue = route;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        BuildHollowCar(train.transform);
        Selection.activeGameObject = train;
    }

    // Ici bos prizma: taban, tavan ve dort duvar ayri kuplerden olusur.
    private static void BuildHollowCar(Transform parent)
    {
        var body = new GameObject("Body").transform;
        body.SetParent(parent, false);

        float halfHeight = CarHeight / 2f;
        AddPart(body, "Floor", new Vector3(0f, WallThickness / 2f, 0f), new Vector3(CarWidth, WallThickness, CarLength));
        AddPart(body, "Roof", new Vector3(0f, CarHeight - WallThickness / 2f, 0f), new Vector3(CarWidth, WallThickness, CarLength));
        AddPart(body, "Wall Left", new Vector3(-(CarWidth - WallThickness) / 2f, halfHeight, 0f), new Vector3(WallThickness, CarHeight, CarLength));
        AddPart(body, "Wall Right", new Vector3((CarWidth - WallThickness) / 2f, halfHeight, 0f), new Vector3(WallThickness, CarHeight, CarLength));
        AddPart(body, "Wall Front", new Vector3(0f, halfHeight, (CarLength - WallThickness) / 2f), new Vector3(CarWidth, CarHeight, WallThickness));
        AddPart(body, "Wall Back", new Vector3(0f, halfHeight, -(CarLength - WallThickness) / 2f), new Vector3(CarWidth, CarHeight, WallThickness));
    }

    private static void AddPart(Transform parent, string name, Vector3 localPosition, Vector3 size)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;
    }

    private static Vector3 GroundPoint(Vector2 xz)
    {
        var origin = new Vector3(xz.x, 100f, xz.y);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f))
            return hit.point;
        return new Vector3(xz.x, 0f, xz.y);
    }
}
