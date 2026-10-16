using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Zombie Apocalypse Kit'teki tufekten oyuncunun alip ates edebilecegi silahi kurar.
// M4A4 prefab'i kalip olarak kullanilir (M4A4Script, ses, trigger, Rigidbody, efektler),
// sadece model ve noktalar (el tutma, namlu) yeni tufege gore yerlestirilir.
// WeaponEquip cocuk sirasina bagli: 0 sol el, 1 sag el.
public static class WeaponSetup
{
    private const string Template = "Assets/M4A4.prefab";
    private const string RifleModel = "Assets/ThirdParty/Quaternius/ZombieApocalypseKit/Weapons/FBX/Rifle.fbx";
    private const string PrefabPath = "Assets/Weapons/SurvivorRifle.prefab";

    // Tufek modeli +Z'ye bakar; silah koku ise namluyu -X'e cevirir (nisan kisiti boyle kurulu).
    // Model olculeri (model birimi): kabza (0, 0, 0), on el (0, 0.13, 0.56), namlu ucu (0, 0.21, 1.02).
    private const float Scale = 0.7f;
    private const float GripX = -0.34f;   // sag elin yeri, M4A4 ile ayni
    private const float BoreY = 0.21f;    // namlu ekseni koke gore y = 0 olur

    [MenuItem("Tools/Weapons/Build Survivor Rifle")]
    public static void BuildRifle()
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Template));
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "SurvivorRifle";
        try
        {
            Object.DestroyImmediate(root.transform.Find("M4A4weapon").gameObject);

            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RifleModel), root.transform);
            model.name = "RifleModel";
            model.transform.localRotation = Quaternion.Euler(0f, -90f, 0f) * model.transform.localRotation;
            model.transform.localScale *= Scale;
            model.transform.localPosition = new Vector3(GripX, -BoreY * Scale, 0f);
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = root.layer;

            Place(root, "LeftHandPlace", ModelPoint(0.56f, 0.13f));
            Place(root, "RightHandPlace", ModelPoint(0f, 0.03f));
            Place(root, "ShootPoint", ModelPoint(1.04f, BoreY));
            Place(root, "MuzzleFlash01", ModelPoint(1.06f, BoreY));

            var trigger = root.GetComponent<SphereCollider>();
            if (trigger != null)
                trigger.center = new Vector3(GripX - 0.1f, 0f, 0f);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
        Debug.Log("Silah prefab'i olusturuldu: " + PrefabPath);
    }

    // Tufek modelindeki bir noktanin (ileri z, yukari y) silah kokundeki yeri.
    private static Vector3 ModelPoint(float forward, float up)
    {
        return new Vector3(GripX - forward * Scale, (up - BoreY) * Scale, 0f);
    }

    private static void Place(GameObject root, string child, Vector3 position)
    {
        var t = root.transform.Find(child);
        if (t != null)
            t.localPosition = position;
        else
            Debug.LogWarning("Silah noktasi bulunamadi: " + child);
    }

    // Sahnedeki M4A4'leri kapatir (silinmez) ve ayni yerlere yeni tufegi koyar.
    [MenuItem("Tools/Weapons/Replace M4A4 Pickups")]
    public static void ReplacePickups()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
            BuildRifle();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

        foreach (var old in GameObject.FindGameObjectsWithTag("Weapon"))
        {
            if (old.GetComponent<M4A4Script>() == null || old.name.StartsWith("SurvivorRifle") || old.transform.parent != null)
                continue;
            var rifle = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.scene);
            Undo.RegisterCreatedObjectUndo(rifle, "Place rifle");
            // Yere yan yatmis halde, zeminin hemen ustunde dursun; alinana kadar dusmesin.
            Vector3 position = old.transform.position;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                position.y = hit.point.y + 0.06f;
            rifle.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, old.transform.eulerAngles.y, 90f));
            rifle.GetComponent<Rigidbody>().isKinematic = true;

            Undo.RecordObject(old, "Disable M4A4");
            old.SetActive(false);
            old.name = old.name + " (kenarda)";
            EditorSceneManager.MarkSceneDirty(old.scene);
        }
    }
}
