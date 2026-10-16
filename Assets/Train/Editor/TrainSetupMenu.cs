using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Tools > Train > Create Test Train: acik sahneye virajli bir test rotasi (varsa
// mevcut rota kullanilir) ve icinde gezilebilen, binilebilir bir lokomotif ekler.
// Lokomotif primitiflerden yapilmis gecici bir modeldir: kondukter kabini ve
// konsolu, yolcu koltuklari, iki yan kapi, camlar ve tavan.
// Ayrica URP renderer'a koltuga gore gorus alanini kapatan "Train Vision" gecisini ekler.
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

    private const string MaterialFolder = "Assets/Train/Materials";
    private const string VisionShaderPath = "Assets/Train/TrainVision.shader";
    private const string VisionFeatureName = "Train Vision";

    private const float CarLength = 12f;
    private const float CarWidth = 3.5f;
    private const float FloorHeight = 0.2f;
    private const float LowerWallTop = 1.3f;   // bu yukseklige kadar duvarlar hep gorunur ve carpisir
    private const float WindowTop = 2.4f;
    private const float WallTop = 3.0f;
    private const float RoofThickness = 0.2f;
    private const float Wall = 0.2f;
    private const float CabWallZ = 2.6f;       // kondukter kabini ile yolcu bolumu arasi
    private const float DoorWidth = 1.2f;
    private const float StartDistance = 28f;

    private static readonly List<Renderer> Cutaway = new List<Renderer>();

    [MenuItem("Tools/Train/Create Test Train")]
    private static void CreateTestTrain()
    {
        var route = Object.FindFirstObjectByType<TrainRoute>();
        if (route == null)
            route = CreateRoute();

        var train = new GameObject("Train");
        Undo.RegisterCreatedObjectUndo(train, "Create Test Train");
        train.transform.position = route.transform.GetChild(0).position;
        train.AddComponent<Rigidbody>().isKinematic = true;
        var controller = train.AddComponent<TrainController>();
        var serialized = new SerializedObject(controller);
        serialized.FindProperty("route").objectReferenceValue = route;
        serialized.FindProperty("startDistance").floatValue = StartDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Cutaway.Clear();
        BuildLocomotive(train.transform, out Transform[] doors);

        var vision = train.AddComponent<TrainVision>();
        vision.SetHullHalfSize(new Vector2(CarWidth / 2f, CarLength / 2f));
        var boarding = train.AddComponent<TrainBoarding>();
        boarding.Configure(doors, Cutaway.ToArray(), FloorHeight);

        EnsureVisionRendererFeature();
        Selection.activeGameObject = train;
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
        var material = GetMaterial("TrainVision", Color.black, shader);

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

    private static TrainRoute CreateRoute()
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
        return route;
    }

    private static void BuildLocomotive(Transform train, out Transform[] doors)
    {
        var body = GetMaterial("TrainBody", new Color(0.55f, 0.12f, 0.1f));
        var trim = GetMaterial("TrainTrim", new Color(0.18f, 0.18f, 0.2f));
        var floor = GetMaterial("TrainFloor", new Color(0.42f, 0.3f, 0.2f));
        var glass = GetMaterial("TrainGlass", new Color(0.55f, 0.75f, 0.85f));
        var seatMaterial = GetMaterial("TrainSeat", new Color(0.15f, 0.3f, 0.55f));
        var console = GetMaterial("TrainConsole", new Color(0.25f, 0.27f, 0.3f));
        var door = GetMaterial("TrainDoor", new Color(0.85f, 0.65f, 0.15f));
        var lamp = GetMaterial("TrainLamp", new Color(1f, 0.95f, 0.6f));

        float halfW = CarWidth / 2f;
        float halfL = CarLength / 2f;
        float sideX = halfW - Wall / 2f;
        float frontZ = halfL - Wall / 2f;

        var shell = Group(train, "Body");
        Box(shell, "Floor", new Vector3(0f, FloorHeight / 2f, 0f), new Vector3(CarWidth, FloorHeight, CarLength), floor);

        // Alt duvarlar: hep gorunur, oyuncuyu icerde tutar.
        float lowerH = LowerWallTop - FloorHeight;
        float lowerY = FloorHeight + lowerH / 2f;
        foreach (float side in new[] { -1f, 1f })
        {
            string name = side < 0 ? "Left" : "Right";
            Box(shell, $"Wall {name} Lower", new Vector3(side * sideX, lowerY, 0f), new Vector3(Wall, lowerH, CarLength), body);
        }
        Box(shell, "Wall Front Lower", new Vector3(0f, lowerY, frontZ), new Vector3(CarWidth, lowerH, Wall), body);
        Box(shell, "Wall Back Lower", new Vector3(0f, lowerY, -frontZ), new Vector3(CarWidth, lowerH, Wall), body);

        // Ust duvarlar, camlar ve tavan: oyuncu icerdeyken gizlenir, carpismaz.
        var upper = Group(shell, "Upper (hidden inside)");
        float windowH = WindowTop - LowerWallTop;
        float windowY = LowerWallTop + windowH / 2f;
        float topH = WallTop - WindowTop;
        float topY = WindowTop + topH / 2f;
        foreach (float side in new[] { -1f, 1f })
        {
            string name = side < 0 ? "Left" : "Right";
            Box(upper, $"Wall {name} Top", new Vector3(side * sideX, topY, 0f), new Vector3(Wall, topH, CarLength), body, cutaway: true);
            // Pencere araliklari: kabin camlari, kapi, yolcu camlari.
            SideBand(upper, name, side * sideX, windowY, windowH, body, glass, new[]
            {
                (-halfL, -4.2f, false),
                (-4.2f, -1.4f, true),   // yolcu cami
                (-1.4f, -DoorWidth / 2f, false),
                (-DoorWidth / 2f, DoorWidth / 2f, false), // kapi (ayri parca)
                (DoorWidth / 2f, 3.0f, false),
                (3.0f, 5.0f, true),     // kabin yan cami
                (5.0f, halfL, false),
            });
        }
        // On cam (genis) ve arka cam.
        Box(upper, "Front Pillar Left", new Vector3(-halfW + 0.25f, windowY, frontZ), new Vector3(0.5f, windowH, Wall), body, cutaway: true);
        Box(upper, "Front Pillar Right", new Vector3(halfW - 0.25f, windowY, frontZ), new Vector3(0.5f, windowH, Wall), body, cutaway: true);
        Box(upper, "Windshield", new Vector3(0f, windowY, frontZ), new Vector3(CarWidth - 1f, windowH, 0.08f), glass, cutaway: true);
        Box(upper, "Wall Front Top", new Vector3(0f, topY, frontZ), new Vector3(CarWidth, topH, Wall), body, cutaway: true);
        Box(upper, "Back Pillar Left", new Vector3(-halfW + 0.6f, windowY, -frontZ), new Vector3(1.2f, windowH, Wall), body, cutaway: true);
        Box(upper, "Back Pillar Right", new Vector3(halfW - 0.6f, windowY, -frontZ), new Vector3(1.2f, windowH, Wall), body, cutaway: true);
        Box(upper, "Rear Window", new Vector3(0f, windowY, -frontZ), new Vector3(CarWidth - 2.4f, windowH, 0.08f), glass, cutaway: true);
        Box(upper, "Wall Back Top", new Vector3(0f, topY, -frontZ), new Vector3(CarWidth, topH, Wall), body, cutaway: true);
        Box(upper, "Roof", new Vector3(0f, WallTop + RoofThickness / 2f, 0f), new Vector3(CarWidth + 0.2f, RoofThickness, CarLength + 0.2f), trim, cutaway: true);
        Box(upper, "Headlight", new Vector3(0f, WallTop - 0.25f, halfL + 0.02f), new Vector3(0.6f, 0.3f, 0.06f), lamp, cutaway: true);

        // Kapilar: dis yuzeye renkli panel; ileri yonu trenin disina bakar.
        var doorList = new List<Transform>();
        foreach (float side in new[] { -1f, 1f })
        {
            string name = side < 0 ? "Left" : "Right";
            var doorPoint = new GameObject($"Door {name}").transform;
            doorPoint.SetParent(train, false);
            doorPoint.localPosition = new Vector3(side * halfW, 0f, 0f);
            doorPoint.localRotation = Quaternion.LookRotation(new Vector3(side, 0f, 0f));
            doorList.Add(doorPoint);

            float panelX = side * (halfW + 0.02f);
            Box(shell, $"Door {name} Panel Lower", new Vector3(panelX, lowerY, 0f), new Vector3(0.05f, lowerH, DoorWidth), door, collider: false);
            Box(upper, $"Door {name} Panel Upper", new Vector3(panelX, windowY, 0f), new Vector3(0.05f, windowH, DoorWidth), door, cutaway: true);
            // Ic tarafta kapi oldugunu gosteren isaret.
            Box(shell, $"Door {name} Mat", new Vector3(side * (halfW - 0.55f), FloorHeight + 0.01f, 0f), new Vector3(0.7f, 0.02f, DoorWidth), door, collider: false);
        }
        doors = doorList.ToArray();

        // Kondukter kabini ile yolcu bolumunu ayiran bolme (ortasi gecit).
        var interior = Group(train, "Interior");
        float partitionW = (CarWidth - 2f * Wall - 1.2f) / 2f;
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (0.6f + partitionW / 2f);
            Box(interior, $"Cab Partition {(side < 0 ? "Left" : "Right")}", new Vector3(x, lowerY, CabWallZ), new Vector3(partitionW, lowerH, 0.12f), trim);
            Box(upper, $"Cab Partition Top {(side < 0 ? "Left" : "Right")}", new Vector3(x, (LowerWallTop + WallTop) / 2f, CabWallZ), new Vector3(partitionW, WallTop - LowerWallTop, 0.12f), trim, cutaway: true);
        }

        // Kondukter konsolu.
        Box(interior, "Console", new Vector3(0f, 0.7f, 5.3f), new Vector3(CarWidth - 2f * Wall, 1.0f, 0.8f), console);
        Box(interior, "Console Panel", new Vector3(0f, 1.22f, 5.05f), new Vector3(1.6f, 0.04f, 0.4f), lamp, collider: false);
        Box(interior, "Throttle Lever", new Vector3(0.55f, 1.4f, 5.0f), new Vector3(0.08f, 0.4f, 0.08f), trim, collider: false);

        // Koltuklar. Aci: 0 = on, 90 = sag, -90 = sol, 180 = arka.
        AddSeat(interior, seatMaterial, "Kondukter koltugu", true, new Vector3(0f, 0f, 3.9f), 0f, 50f, new[]
        {
            new TrainSeat.ViewSector(0f, 70f),     // on cam
            new TrainSeat.ViewSector(-62f, 22f),   // sol kabin cami (kismi)
            new TrainSeat.ViewSector(62f, 22f),    // sag kabin cami (kismi)
        });
        AddSeat(interior, seatMaterial, "Sol pencere koltugu", false, new Vector3(-1.05f, 0f, -2.4f), 0f, 35f, new[]
        {
            new TrainSeat.ViewSector(-90f, 60f),
        });
        AddSeat(interior, seatMaterial, "Sag pencere koltugu", false, new Vector3(1.05f, 0f, -2.4f), 0f, 35f, new[]
        {
            new TrainSeat.ViewSector(90f, 60f),
        });
        AddSeat(interior, seatMaterial, "Arka cam koltugu", false, new Vector3(0f, 0f, -4.6f), 180f, 35f, new[]
        {
            new TrainSeat.ViewSector(180f, 50f),
        });
    }

    // Yan duvarin pencere bandi: araliklar sirayla dolu duvar ya da cam.
    private static void SideBand(Transform parent, string side, float x, float y, float height, Material wall, Material glass, (float from, float to, bool window)[] spans)
    {
        foreach (var span in spans)
        {
            float length = span.to - span.from;
            float z = (span.from + span.to) / 2f;
            if (Mathf.Abs(z) < 0.01f && length <= DoorWidth + 0.01f)
                continue; // kapi araligi
            if (span.window)
                Box(parent, $"Window {side} {z:0.0}", new Vector3(x, y, z), new Vector3(0.08f, height, length), glass, cutaway: true);
            else
                Box(parent, $"Wall {side} {z:0.0}", new Vector3(x, y, z), new Vector3(Wall, height, length), wall, cutaway: true);
        }
    }

    // Oturma noktasi yerde; oyuncu buraya yerlesir, koltuk arkasinda kalir.
    private static void AddSeat(Transform parent, Material material, string name, bool driver, Vector3 sitPosition, float yaw, float viewDistance, TrainSeat.ViewSector[] sectors)
    {
        var seat = new GameObject(name).transform;
        seat.SetParent(parent, false);
        seat.localPosition = sitPosition + new Vector3(0f, FloorHeight, 0f);
        seat.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Box(seat, "Cushion", new Vector3(0f, 0.25f, -0.35f), new Vector3(0.65f, 0.12f, 0.55f), material);
        Box(seat, "Base", new Vector3(0f, 0.1f, -0.35f), new Vector3(0.45f, 0.2f, 0.45f), material, collider: false);
        Box(seat, "Backrest", new Vector3(0f, 0.65f, -0.62f), new Vector3(0.65f, 0.75f, 0.1f), material);

        var component = seat.gameObject.AddComponent<TrainSeat>();
        component.Configure(name, driver, seat, sectors, viewDistance);
    }

    private static Transform Group(Transform parent, string name)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool collider = true, bool cutaway = false)
    {
        var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = size;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        if (!collider || cutaway)
            Object.DestroyImmediate(part.GetComponent<BoxCollider>());
        if (cutaway)
            Cutaway.Add(renderer);
    }

    private static Material GetMaterial(string name, Color color, Shader shader = null)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Train", "Materials");

        string path = $"{MaterialFolder}/{name}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        material = new Material(shader != null ? shader : Shader.Find("Universal Render Pipeline/Lit"));
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static Vector3 GroundPoint(Vector2 xz)
    {
        var origin = new Vector3(xz.x, 100f, xz.y);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 200f))
            return hit.point;
        return new Vector3(xz.x, 0f, xz.y);
    }
}
