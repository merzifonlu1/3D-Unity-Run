using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Tools > Train > Build Railway: Quaternius ray parcalariyla (18 m duz, 18 m yaricapli
// 90 derece viraj) bir hat doser ve TrainRoute'un waypoint'lerini hattin ortasina dizer.
// Sahnede TrainRoute varsa ayni obje kullanilir, trenin rota baglantisi kopmaz.
// Tools > Train > Create Train: hattin uzerine Quaternius "Cargo Train front" lokomotifini koyar.
// Ayrica URP renderer'a surucu kabininin gorus alanini cizen "Train Vision" gecisini ekler.
public static class TrainSetupMenu
{
    // Hat: S = duz parca, R = saga viraj, L = sola viraj.
    private const string Layout = "SSRSSRSS";
    private static readonly Vector2 LayoutStart = new Vector2(-20f, -30f);
    private static readonly Vector3 LayoutHeading = Vector3.forward;

    private const string PackFolder = "Assets/ThirdParty/Quaternius/TrainPack";
    private const string LocomotivePath = PackFolder + "/CargoTrain_Front.fbx";
    private const string StraightPath = PackFolder + "/RailwayTrack_Straight.fbx";
    private const string CurvePath = PackFolder + "/RailwayTrack_Curve.fbx";

    private const string MaterialFolder = "Assets/Train/Materials";
    private const string VisionShaderPath = "Assets/Train/TrainVision.shader";
    private const string VisionFeatureName = "Train Vision";

    private const float PieceLength = 18f;     // duz parcanin boyu
    private const float CurveRadius = 18f;     // viraj parcasinin orta hat yaricapi
    private const float RailBottom = 0.0715f;  // ray modelinin tabani, pivotun altinda
    private const float RailTop = 0.143f;      // tekerlerin bastigi yukseklik (zeminden)
    private const float WaypointSpacing = 3f;

    // Cargo Train front olculeri (model +X'e bakiyor, 10.35 x 2.68 x 2.15 m).
    private static readonly Vector3 BodyCenter = new Vector3(0f, 1.34f, 0.05f);
    private static readonly Vector3 BodySize = new Vector3(2.15f, 2.68f, 10.35f);
    private static readonly Vector3 DriverSeat = new Vector3(0f, 1.1f, 4.0f);
    private const float StartDistance = 10f;

    [MenuItem("Tools/Train/Build Railway")]
    private static void BuildRailwayMenu()
    {
        Selection.activeGameObject = BuildRailway().gameObject;
    }

    [MenuItem("Tools/Train/Create Train")]
    private static void CreateTrain()
    {
        var route = Object.FindFirstObjectByType<TrainRoute>();
        if (route == null)
            route = BuildRailway();

        var train = new GameObject("Train");
        Undo.RegisterCreatedObjectUndo(train, "Create Train");
        train.AddComponent<Rigidbody>().isKinematic = true;
        var body = train.AddComponent<BoxCollider>();
        body.center = BodyCenter;
        body.size = BodySize;

        var controller = train.AddComponent<TrainController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("route").objectReferenceValue = route;
        serialized.FindProperty("startDistance").floatValue = StartDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LocomotivePath));
        model.name = "Model";
        model.transform.SetParent(train.transform, false);
        model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * model.transform.localRotation; // burun +Z'ye

        var driver = new GameObject("Driver").transform;
        driver.SetParent(train.transform, false);
        driver.localPosition = DriverSeat;

        Vector2 halfSize = new Vector2(BodySize.x / 2f, BodySize.z / 2f);
        train.AddComponent<TrainVision>().SetHullHalfSize(halfSize);
        train.AddComponent<TrainBoarding>().Configure(driver, halfSize);

        route.Build();
        route.Evaluate(StartDistance, out Vector3 position, out Vector3 forward);
        train.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, Vector3.up));

        EnsureVisionRendererFeature();
        Selection.activeGameObject = train;
    }

    private static TrainRoute BuildRailway()
    {
        var route = Object.FindFirstObjectByType<TrainRoute>();
        if (route == null)
        {
            var routeObject = new GameObject("TrainRoute");
            Undo.RegisterCreatedObjectUndo(routeObject, "Build Railway");
            route = routeObject.AddComponent<TrainRoute>();
        }
        else
        {
            for (int i = route.transform.childCount - 1; i >= 0; i--)
                Undo.DestroyObjectImmediate(route.transform.GetChild(i).gameObject);
        }

        var oldRails = GameObject.Find("Railway");
        if (oldRails != null)
            Undo.DestroyObjectImmediate(oldRails);
        var rails = new GameObject("Railway").transform;
        Undo.RegisterCreatedObjectUndo(rails.gameObject, "Build Railway");

        var straight = AssetDatabase.LoadAssetAtPath<GameObject>(StraightPath);
        var curve = AssetDatabase.LoadAssetAtPath<GameObject>(CurvePath);

        var centerLine = new List<Vector3>();
        Vector3 position = new Vector3(LayoutStart.x, 0f, LayoutStart.y);
        Vector3 heading = LayoutHeading;
        centerLine.Add(position);

        foreach (char piece in Layout)
        {
            if (piece == 'S')
            {
                // Modelin orta hatti yerel X ekseninde, pivot ortada.
                Place(rails, straight, position + heading * (PieceLength / 2f), Yaw(heading, Vector3.right));
                Vector3 end = position + heading * PieceLength;
                AddLine(centerLine, position, end);
                position = end;
                continue;
            }

            // Viraj modeli: merkez (-18, 0, -18), uclar (0, 0, -18) +Z yonunde ve (-18, 0, 0) -X yonunde.
            // A'dan B'ye gidis sola, B'den A'ya gidis saga donustur.
            bool right = piece == 'R';
            Quaternion yaw = right
                ? Yaw(heading, Vector3.right)
                : Yaw(heading, Vector3.forward);
            Vector3 entry = right ? new Vector3(-CurveRadius, 0f, 0f) : new Vector3(0f, 0f, -CurveRadius);
            Vector3 origin = position - yaw * entry;
            Place(rails, curve, origin, yaw);

            Vector3 center = origin + yaw * new Vector3(-CurveRadius, 0f, -CurveRadius);
            float turn = right ? 90f : -90f;
            int steps = Mathf.CeilToInt(CurveRadius * Mathf.PI / 2f / WaypointSpacing);
            for (int s = 1; s <= steps; s++)
                centerLine.Add(center + Quaternion.AngleAxis(turn * s / steps, Vector3.up) * (position - center));
            position = centerLine[centerLine.Count - 1];
            heading = Quaternion.AngleAxis(turn, Vector3.up) * heading;
        }

        // Raylar ve rota zemine oturur.
        foreach (Transform rail in rails)
            rail.position += Vector3.up * (GroundHeight(rail.position) + RailBottom);
        for (int i = 0; i < centerLine.Count; i++)
        {
            var waypoint = new GameObject($"Waypoint {i}");
            waypoint.transform.SetParent(route.transform, false);
            Vector3 point = centerLine[i];
            waypoint.transform.position = new Vector3(point.x, GroundHeight(point) + RailTop, point.z);
        }
        route.Build();
        return route;
    }

    private static void AddLine(List<Vector3> points, Vector3 from, Vector3 to)
    {
        int steps = Mathf.CeilToInt(Vector3.Distance(from, to) / WaypointSpacing);
        for (int s = 1; s <= steps; s++)
            points.Add(Vector3.Lerp(from, to, (float)s / steps));
    }

    // Y ekseni etrafinda, modelin 'axis' eksenini 'heading' yonune ceviren donus.
    private static Quaternion Yaw(Vector3 heading, Vector3 axis)
    {
        return Quaternion.LookRotation(heading) * Quaternion.Inverse(Quaternion.LookRotation(axis));
    }

    private static void Place(Transform parent, GameObject prefab, Vector3 position, Quaternion yaw)
    {
        var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        piece.transform.SetPositionAndRotation(position, yaw * piece.transform.localRotation);
    }

    [MenuItem("Tools/Train/Add Vision Pass To Renderer")]
    private static void EnsureVisionRendererFeature()
    {
        var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null)
        {
            Debug.LogWarning("TrainSetupMenu: URP bulunamadi, gorus gecisi eklenmedi.");
            return;
        }

        var shader = AssetDatabase.LoadAssetAtPath<Shader>(VisionShaderPath);
        var material = GetMaterial("TrainVision", shader);

        var rendererList = new SerializedObject(pipeline).FindProperty("m_RendererDataList");
        for (int i = 0; i < rendererList.arraySize; i++)
        {
            var data = rendererList.GetArrayElementAtIndex(i).objectReferenceValue as UniversalRendererData;
            if (data == null || data.rendererFeatures.Exists(f => f != null && f.name == VisionFeatureName))
                continue;

            var feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            feature.name = VisionFeatureName;
            feature.passMaterial = material;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.fetchColorBuffer = false;
            feature.requirements = ScriptableRenderPassInput.Depth;
            AssetDatabase.AddObjectToAsset(feature, data);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);

            var serializedData = new SerializedObject(data);
            var features = serializedData.FindProperty("m_RendererFeatures");
            var map = serializedData.FindProperty("m_RendererFeatureMap");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            serializedData.ApplyModifiedPropertiesWithoutUndo();
            data.SetDirty();
            EditorUtility.SetDirty(data);
            Debug.Log($"TrainSetupMenu: '{VisionFeatureName}' gecisi {data.name} renderer'ina eklendi.", data);
        }
        AssetDatabase.SaveAssets();
    }

    private static Material GetMaterial(string name, Shader shader)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Train", "Materials");

        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(shader);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    // Raylarin, trenin ve oyuncunun haric tutuldugu zemin yuksekligi.
    private static float GroundHeight(Vector3 point)
    {
        var origin = new Vector3(point.x, 100f, point.z);
        float height = float.NegativeInfinity;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 200f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<TrainController>() != null || hit.collider.GetComponentInParent<PlayerMovement>() != null)
                continue;
            height = Mathf.Max(height, hit.point.y);
        }
        return float.IsNegativeInfinity(height) ? 0f : height;
    }
}
