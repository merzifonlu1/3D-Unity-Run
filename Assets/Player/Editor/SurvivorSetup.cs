using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations.Rigging;

// Oyuncu karakterini Quaternius Zombie Apocalypse Kit'teki Shaun ile degistirir.
// Shaun Generic rig'lidir (Humanoid'e cevrilemiyor), bu yuzden Mixamo klipleri ona oturmaz;
// Animator tamamen Shaun'un kendi klipleriyle kurulur.
// Paket gercek bir comelme klibi icermiyor ("Duck" ellerle basi korumak). Comelme klipleri
// Shaun'un durus ve yuruyusunden uretilir: govde alcalir, bacaklar IK ile ayaga uzanir.
// Tools/Player/Survivor/Setup All hepsini sirayla yapar; tekrar calistirmak guvenlidir.
public static class SurvivorSetup
{
    private const string Model = "Assets/ThirdParty/Quaternius/ZombieApocalypseKit/Characters/FBX/Characters_Shaun.fbx";
    private const string Folder = "Assets/Player/Animations/Survivor";
    private const string ControllerPath = Folder + "/Survivor.controller";
    private const string MaskPath = Folder + "/SurvivorUpperBody.mask";
    private const string CharacterName = "Survivor (Shaun)";
    private const float CharacterScale = 1.15f;

    private static readonly string[] LoopingClips = { "Idle", "Idle_Gun", "Walk", "Walk_Gun", "Run", "Run_Gun" };
    // Model her silahi elinde tutar; oyuncu kendi M4A4'unu aldigi icin hepsi gizlenir.
    private static readonly string[] BuiltInWeapons = { "Axe", "Guitar", "Knife", "Pistol", "Rifle", "Shotgun", "SMG", "Spear", "WoodenBat_Barbed", "WoodenBat_Saw" };

    [MenuItem("Tools/Player/Survivor/Setup All")]
    public static void SetupAll()
    {
        BakeCrouchClips();
        BuildController();
        SwapPlayerCharacter();
    }

    // ------------------------------------------------------------------ Klipler
    [MenuItem("Tools/Player/Survivor/1 Bake Crouch Clips")]
    public static void BakeCrouchClips()
    {
        EnsureFolder();
        SetLooping();

        var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            Save(Bake(instance, Clip("Idle"), "CrouchIdle", 0.13f, 1f));
            Save(Bake(instance, Clip("Walk"), "CrouchWalk", 0.12f, 0.75f));
            Save(Bake(instance, Clip("Idle_Gun"), "CrouchIdle_Gun", 0.13f, 1f));
            Save(Bake(instance, Clip("Walk_Gun"), "CrouchWalk_Gun", 0.12f, 0.75f));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
    }

    private static void SetLooping()
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
        bool changed = importer.clipAnimations.Length == 0;
        foreach (var clip in clips)
        {
            bool loop = LoopingClips.Contains(ShortName(clip.name));
            if (clip.loopTime != loop)
            {
                clip.loopTime = loop;
                changed = true;
            }
        }
        if (!changed)
            return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    public static AnimationClip Clip(string shortName)
    {
        return AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>()
            .First(c => ShortName(c.name) == shortName && !c.name.StartsWith("__preview"));
    }

    private static string ShortName(string clipName)
    {
        int bar = clipName.LastIndexOf('|');
        return bar >= 0 ? clipName.Substring(bar + 1) : clipName;
    }

    // Kaynak klibi kare kare orneyip comelmis poza cevirir.
    // drop: govdenin alcalmasi (model birimi, karakter ~1.5), stride: adim uzunlugu carpani.
    private static AnimationClip Bake(GameObject instance, AnimationClip source, string name, float drop, float stride)
    {
        var root = instance.transform;
        var paths = AnimationUtility.GetCurveBindings(source).Select(b => b.path).Distinct().ToList();
        var bones = paths.Select(p => string.IsNullOrEmpty(p) ? root : root.Find(p)).ToList();

        const float fps = 30f;
        int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * fps) + 1);
        var keys = bones.Select(_ => new List<Keyframe>[7]).ToList();
        for (int b = 0; b < bones.Count; b++)
            for (int k = 0; k < 7; k++)
                keys[b][k] = new List<Keyframe>();
        var previous = new Quaternion[bones.Count];

        for (int f = 0; f < frames; f++)
        {
            float time = source.length * f / (frames - 1);
            source.SampleAnimation(instance, time);
            Crouch(root, drop, stride);

            for (int b = 0; b < bones.Count; b++)
            {
                var t = bones[b];
                if (t == null)
                    continue;
                Vector3 p = t.localPosition;
                Quaternion q = t.localRotation;
                if (f > 0 && Quaternion.Dot(previous[b], q) < 0f)
                    q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                previous[b] = q;
                float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w };
                for (int k = 0; k < 7; k++)
                    keys[b][k].Add(new Keyframe(time, values[k]));
            }
        }

        var clip = new AnimationClip { name = name, frameRate = fps };
        string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
        for (int b = 0; b < bones.Count; b++)
        {
            if (bones[b] == null)
                continue;
            for (int k = 0; k < 7; k++)
                clip.SetCurve(paths[b], typeof(Transform), props[k], new AnimationCurve(keys[b][k].ToArray()));
        }
        clip.EnsureQuaternionContinuity();
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    // Model +Z'ye bakar. Govdeyi indirir, one eger, ayaklari kalcaya yaklastirir ve bacaklari ayaklara uzatir.
    private static void Crouch(Transform root, float drop, float stride)
    {
        var body = Find(root, "Body");
        var hips = Find(root, "Hips");
        float hipZ = hips.position.z;

        var targets = new Dictionary<string, Vector3>();
        foreach (var side in new[] { "L", "R" })
        {
            var foot = Find(root, "Foot." + side);
            Vector3 p = foot.position;
            p.z = hipZ + (p.z - hipZ) * stride;
            foot.position = p;
            targets[side] = p;
        }

        body.position += Vector3.down * drop;

        var abdomen = Find(root, "Abdomen");
        abdomen.RotateAround(abdomen.position, Vector3.right, 18f);
        var head = Find(root, "Head");
        head.RotateAround(head.position, Vector3.right, -12f);

        foreach (var side in new[] { "L", "R" })
            SolveLeg(Find(root, "UpperLeg." + side), Find(root, "LowerLeg." + side), Find(root, "LowerLeg." + side + "_end"), targets[side]);
    }

    // Iki kemikli IK: diz, kaynak pozdaki diz yonunde (biraz one) kalir.
    private static void SolveLeg(Transform upper, Transform lower, Transform end, Vector3 target)
    {
        Vector3 a = upper.position, b = lower.position, c = end.position;
        float upperLength = (b - a).magnitude;
        float lowerLength = (c - b).magnitude;
        Vector3 toTarget = target - a;
        float reach = Mathf.Clamp(toTarget.magnitude, 0.001f, (upperLength + lowerLength) * 0.999f);
        Vector3 dir = toTarget.normalized;

        Vector3 pole = (b + Vector3.forward * 0.1f) - a;
        pole = Vector3.ProjectOnPlane(pole, dir).normalized;
        float cos = (upperLength * upperLength + reach * reach - lowerLength * lowerLength) / (2f * upperLength * reach);
        float angle = Mathf.Acos(Mathf.Clamp(cos, -1f, 1f));
        Vector3 knee = a + (dir * Mathf.Cos(angle) + pole * Mathf.Sin(angle)) * upperLength;

        upper.rotation = Quaternion.FromToRotation(b - a, knee - a) * upper.rotation;
        b = lower.position;
        c = end.position;
        lower.rotation = Quaternion.FromToRotation(c - b, a + dir * reach - b) * lower.rotation;
    }

    private static void Save(AnimationClip clip)
    {
        string path = Folder + "/" + clip.name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(clip, path);
        }
    }

    // ------------------------------------------------------------------ Animator
    // Katman sirasi eski controller ile ayni; kodun kullandigi indeksler degismez:
    // 0 silahli hareket, 1 nisan (ayakta), 2 silahsiz hareket (WeaponEquip agirligini ayarlar), 3 nisan (sadece ust govde).
    [MenuItem("Tools/Player/Survivor/2 Build Animator")]
    public static void BuildController()
    {
        EnsureFolder();
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Velocityx", AnimatorControllerParameterType.Float);
        controller.AddParameter("Velocityz", AnimatorControllerParameterType.Float);
        controller.AddParameter("MouseDirecx", AnimatorControllerParameterType.Float);
        controller.AddParameter("MouseDirecz", AnimatorControllerParameterType.Float);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Stance", AnimatorControllerParameterType.Int);
        controller.AddParameter("Seated", AnimatorControllerParameterType.Bool);

        var crouchIdle = Load("CrouchIdle");
        var crouchWalk = Load("CrouchWalk");
        var crouchIdleGun = Load("CrouchIdle_Gun");
        var crouchWalkGun = Load("CrouchWalk_Gun");

        controller.layers[0].stateMachine.name = "Base Layer";
        BuildLocomotion(controller, 0, Clip("Idle_Gun"), Clip("Walk_Gun"), Clip("Run_Gun"), crouchIdleGun, crouchWalkGun);

        controller.AddLayer("OnAim");
        Speed1D(controller, 1, "Aim", Clip("Idle_Gun"), Clip("Walk_Gun"), Clip("Run_Gun"));

        controller.AddLayer("Empty");
        BuildLocomotion(controller, 2, Clip("Idle"), Clip("Walk"), Clip("Run"), crouchIdle, crouchWalk);

        controller.AddLayer("OnAimUpper");
        Speed1D(controller, 3, "Aim Upper", Clip("Idle_Gun"), Clip("Walk_Gun"), Clip("Run_Gun"));

        var layers = controller.layers;
        layers[1].defaultWeight = 0f;
        layers[2].defaultWeight = 1f;
        layers[3].defaultWeight = 0f;
        layers[3].avatarMask = BuildUpperBodyMask();
        controller.layers = layers;

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
    }

    private static void BuildLocomotion(AnimatorController controller, int layer, Motion idle, Motion walk, Motion run, Motion crouchIdle, Motion crouchWalk)
    {
        var stand = Speed1D(controller, layer, "Stand", idle, walk, run);

        var crouch = controller.CreateBlendTreeInController("Crouch", out var crouchTree, layer);
        crouchTree.blendParameter = "Speed";
        crouchTree.useAutomaticThresholds = false;
        crouchTree.AddChild(crouchIdle, 0f);
        crouchTree.AddChild(crouchWalk, 1f);
        crouch.speed = 0.7f; // comelme hizi yuruyusun 0.4'u, adim da kisa

        // Surerken oyuncu gorunmuyor; paket oturma klibi icermedigi icin bekleme pozu kullanilir.
        var sm = controller.layers[layer].stateMachine;
        var sit = sm.AddState("Sit");
        sit.motion = idle;

        sm.defaultState = stand;
        var toCrouch = stand.AddTransition(crouch);
        Configure(toCrouch, 0.25f);
        toCrouch.AddCondition(AnimatorConditionMode.Equals, 1, "Stance");
        var toStand = crouch.AddTransition(stand);
        Configure(toStand, 0.25f);
        toStand.AddCondition(AnimatorConditionMode.Equals, 0, "Stance");
        var toSit = sm.AddAnyStateTransition(sit);
        Configure(toSit, 0.3f);
        toSit.canTransitionToSelf = false;
        toSit.AddCondition(AnimatorConditionMode.If, 0, "Seated");
        var fromSit = sit.AddTransition(stand);
        Configure(fromSit, 0.3f);
        fromSit.AddCondition(AnimatorConditionMode.IfNot, 0, "Seated");
    }

    private static AnimatorState Speed1D(AnimatorController controller, int layer, string name, Motion idle, Motion walk, Motion run)
    {
        var state = controller.CreateBlendTreeInController(name, out var tree, layer);
        tree.blendParameter = "Speed";
        tree.useAutomaticThresholds = false;
        tree.AddChild(idle, 0f);
        tree.AddChild(walk, 1f);
        tree.AddChild(run, 2f);
        return state;
    }

    private static void Configure(AnimatorStateTransition transition, float duration)
    {
        transition.hasExitTime = false;
        transition.duration = duration;
    }

    // Ust govde: Abdomen ve altindaki her sey (kollar, bas). Bacaklar ve govde kok kemigi alt katmandan gelir.
    private static AvatarMask BuildUpperBodyMask()
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
        var mask = new AvatarMask();
        mask.AddTransformPath(model.transform, true);
        for (int i = 0; i < mask.transformCount; i++)
            mask.SetTransformActive(i, mask.GetTransformPath(i).Contains("/Abdomen"));
        for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);

        AssetDatabase.DeleteAsset(MaskPath);
        AssetDatabase.CreateAsset(mask, MaskPath);
        return mask;
    }

    private static AnimationClip Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/" + name + ".anim");
    }

    // ------------------------------------------------------------------ Sahnedeki oyuncu
    [MenuItem("Tools/Player/Survivor/3 Swap Player Character")]
    public static void SwapPlayerCharacter()
    {
        var player = Object.FindFirstObjectByType<PlayerMovement>();
        if (player == null)
        {
            Debug.LogError("Sahnede PlayerMovement bulunamadi.");
            return;
        }
        var old = player.transform.GetChild(0);
        if (old.name == CharacterName)
        {
            Debug.Log("Oyuncu zaten Shaun; sadece rig baglantilari yenileniyor.");
            Wire(player, old.gameObject, old);
            return;
        }

        var survivor = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), player.transform);
        Undo.RegisterCreatedObjectUndo(survivor, "Swap player character");
        survivor.name = CharacterName;
        survivor.transform.SetSiblingIndex(0);
        survivor.transform.localPosition = new Vector3(0f, -0.9f, 0f);
        survivor.transform.localRotation = Quaternion.identity;
        survivor.transform.localScale = Vector3.one * CharacterScale;
        foreach (var t in survivor.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = player.gameObject.layer;
        foreach (var t in survivor.GetComponentsInChildren<Transform>(true))
        {
            if (BuiltInWeapons.Contains(t.name))
                t.gameObject.SetActive(false);
        }

        // Silah tutucular ve rig katmanlari eski modelden tasinir; konumlarini kisitlar belirler.
        foreach (var name in new[] { "WeaponHolder1", "WeaponHolder2", "-------RigLayers------" })
        {
            var child = old.Find(name);
            if (child != null)
                Undo.SetTransformParent(child, survivor.transform, "Move rig");
        }

        Wire(player, survivor, old);

        Undo.RecordObject(old.gameObject, "Disable old character");
        old.gameObject.SetActive(false);
        old.SetAsLastSibling();
        old.name = old.name + " (eski)";

        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log("Oyuncu karakteri Shaun ile degistirildi. Eski model devre disi birakildi: " + old.name);
    }

    private static void Wire(PlayerMovement player, GameObject survivor, Transform old)
    {
        var animator = survivor.GetComponent<Animator>();
        Undo.RecordObject(animator, "Animator");
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        if (survivor.GetComponent<AnimationHandler>() == null)
            Undo.AddComponent<AnimationHandler>(survivor);

        var rigs = survivor.GetComponentsInChildren<Rig>(true);
        var builder = survivor.GetComponent<RigBuilder>();
        if (builder == null)
            builder = Undo.AddComponent<RigBuilder>(survivor);
        Undo.RecordObject(builder, "RigBuilder");
        builder.layers.Clear();
        foreach (var rig in rigs)
            builder.layers.Add(new RigLayer(rig, true));

        var bones = survivor.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());

        // Govde/bas nisani: Banana Man'deki Spine 1/Spine 2/Head yerine Abdomen/Torso/Head.
        // Eksenler karakterin durus pozundan bulunur (Quaternius kemikleri farkli yone bakar).
        var posed = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
        posed.hideFlags = HideFlags.HideAndDontSave;
        Clip("Idle_Gun").SampleAnimation(posed, 0f);
        SetAim(rigs, "AimSpine1", bones["Abdomen"], Find(posed.transform, "Abdomen"), posed.transform);
        SetAim(rigs, "AimSpine2", bones["Torso"], Find(posed.transform, "Torso"), posed.transform);
        SetAim(rigs, "AimHead", bones["Head"], Find(posed.transform, "Head"), posed.transform);
        Object.DestroyImmediate(posed);

        // Tufek pozu sag omuzu izler.
        foreach (var pose in new[] { "M4A4Pose", "M4A4PoseAim" })
        {
            var position = Constraint<MultiPositionConstraint>(rigs, pose);
            if (position == null)
                continue;
            var so = new SerializedObject(position);
            so.FindProperty("m_Data.m_SourceObjects.m_Item0.transform").objectReferenceValue = bones["Shoulder.R"];
            so.ApplyModifiedProperties();
        }

        // El IK: bu modelde ayri el kemigi yok; parmak kokleri uc kemik.
        SetHand(rigs, "LeftHand", bones["UpperArm.L"], bones["LowerArm.L"], bones["Middle1.L"]);
        SetHand(rigs, "RightHand", bones["UpperArm.R"], bones["LowerArm.R"], bones["Index1.R"]);

        var weapons = player.GetComponent<WeaponEquip>();
        if (weapons != null)
        {
            Undo.RecordObject(weapons, "WeaponEquip");
            weapons.Anim = animator;
            weapons.rigBuilder = builder;
        }

        // Eski modelin rig'i artik baska modelin katmanlarini gostermesin.
        var oldBuilder = old != null && old != survivor.transform ? old.GetComponent<RigBuilder>() : null;
        if (oldBuilder != null)
        {
            Undo.RecordObject(oldBuilder, "Old RigBuilder");
            oldBuilder.layers.Clear();
        }
        EditorUtility.SetDirty(survivor);
    }

    private static void SetAim(Rig[] rigs, string name, Transform bone, Transform posedBone, Transform character)
    {
        var aim = Constraint<MultiAimConstraint>(rigs, name);
        if (aim == null)
            return;
        var so = new SerializedObject(aim);
        so.FindProperty("m_Data.m_ConstrainedObject").objectReferenceValue = bone;
        so.FindProperty("m_Data.m_AimAxis").intValue = Axis(posedBone.InverseTransformDirection(character.forward));
        so.FindProperty("m_Data.m_UpAxis").intValue = Axis(posedBone.InverseTransformDirection(character.up));
        so.ApplyModifiedProperties();
    }

    // MultiAimConstraintData.Axis: X, X_NEG, Y, Y_NEG, Z, Z_NEG
    private static int Axis(Vector3 local)
    {
        float ax = Mathf.Abs(local.x), ay = Mathf.Abs(local.y), az = Mathf.Abs(local.z);
        if (ax >= ay && ax >= az)
            return local.x >= 0f ? 0 : 1;
        if (ay >= az)
            return local.y >= 0f ? 2 : 3;
        return local.z >= 0f ? 4 : 5;
    }

    private static void SetHand(Rig[] rigs, string name, Transform root, Transform mid, Transform tip)
    {
        var ik = Constraint<TwoBoneIKConstraint>(rigs, name);
        if (ik == null)
            return;
        var so = new SerializedObject(ik);
        so.FindProperty("m_Data.m_Root").objectReferenceValue = root;
        so.FindProperty("m_Data.m_Mid").objectReferenceValue = mid;
        so.FindProperty("m_Data.m_Tip").objectReferenceValue = tip;
        // Tutma noktalarinin yonu Mixamo parmak kemiklerine gore ayarliydi; bu modelde sadece konum kullanilir.
        so.FindProperty("m_Data.m_TargetRotationWeight").floatValue = 0f;
        so.ApplyModifiedProperties();
    }

    private static T Constraint<T>(Rig[] rigs, string name) where T : Component
    {
        foreach (var rig in rigs)
        {
            foreach (var c in rig.GetComponentsInChildren<T>(true))
            {
                if (c.name == name)
                    return c;
            }
        }
        Debug.LogWarning("Rig kisiti bulunamadi: " + name);
        return null;
    }

    private static Transform Find(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name)
                return t;
        }
        return null;
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Player/Animations", "Survivor");
    }
}
