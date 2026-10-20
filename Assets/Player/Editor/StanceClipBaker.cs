using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Banana Man icin eksik durus animasyonlarini (comelme, oturma) uretir.
// Pozlar kemik yonleri olarak tanimlanir (karakter +Z'ye bakar, +X sagdir),
// HumanPoseHandler ile kas degerlerine cevrilir ve humanoid .anim olarak kaydedilir.
// Bunlar gecici kliplerdir; Mixamo'dan gercek klipler indirilince Animator'de
// ayni state'lerin Motion'i degistirilmesi yeterli.
public static class StanceClipBaker
{
    private const string OutputFolder = "Assets/Player/Animations/Stances";
    private const string AvatarModel = "Assets/Plugins/Banana Yellow Games/Characters/Banana Man/Banana Man.fbx";
    private const string BaseClip = "Assets/Player/Animations/Idle (1).anim";

    // Bir kemik parcasinin (kemik -> cocuk kemik) hedef yonu.
    // pitch: yan gorunumde aci. Bacak/kol icin 0 = asagi, +90 = ileri yatay, -90 = geri yatay.
    // Govde icin (up = true) 0 = yukari, +90 = ileri yatay.
    private struct Segment
    {
        public HumanBodyBones bone;
        public HumanBodyBones child;
        public float pitch;
        public bool up;
        public float side;   // disa dogru ek aci (derece)

        public Segment(HumanBodyBones bone, HumanBodyBones child, float pitch, bool up = false, float side = 0f)
        {
            this.bone = bone;
            this.child = child;
            this.pitch = pitch;
            this.up = up;
            this.side = side;
        }
    }

    private class Pose
    {
        public float bodyPitch;           // tum vucudu one yatirma (one yatirma)
        public readonly List<Segment> segments = new List<Segment>();
        public Vector3? hipsTarget;       // kalcanin koke gore hedef konumu (oturma)
        public float groundClearance = 0f; // en alttaki kemigin yerden yuksekligi
        public bool keepFeetOnGround = true;

        public Pose Seg(HumanBodyBones bone, HumanBodyBones child, float pitch, bool up = false, float side = 0f)
        {
            segments.Add(new Segment(bone, child, pitch, up, side));
            return this;
        }
    }

    [MenuItem("Tools/Player/Bake Stance Clips")]
    public static void BakeAll()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
            AssetDatabase.CreateFolder("Assets/Player/Animations", "Stances");

        var model = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarModel);
        var instance = UnityEngine.Object.Instantiate(model);
        instance.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            var animator = instance.GetComponent<Animator>();
            var handler = new HumanPoseHandler(animator.avatar, instance.transform);
            var basePose = ReadPose(AssetDatabase.LoadAssetAtPath<AnimationClip>(BaseClip), 0f);

            Save("CrouchIdle", Bake(animator, handler, basePose, 2f, 5, CrouchIdle));
            Save("CrouchWalk", Bake(animator, handler, basePose, 1.1f, 12, CrouchWalk));
            Save("SitIdle", Bake(animator, handler, basePose, 3f, 5, SitIdle));
            handler.Dispose();
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Stance clips baked into " + OutputFolder);
    }

    // ---------------------------------------------------------------- Pozlar
    // t: 0..1 dongu zamani

    private static Pose CrouchIdle(float t)
    {
        float breath = Mathf.Sin(t * Mathf.PI * 2f);
        var p = new Pose();
        CrouchTorso(p, 22f + breath * 1.5f);
        CrouchLeg(p, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, 68f, -38f);
        CrouchLeg(p, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 64f, -34f);
        RestArms(p, 12f, 32f + breath * 3f);
        return p;
    }

    private static Pose CrouchWalk(float t)
    {
        float s = Mathf.Sin(t * Mathf.PI * 2f);
        float c = Mathf.Cos(t * Mathf.PI * 2f);
        var p = new Pose();
        CrouchTorso(p, 24f);
        // Ileri giden bacak dizden daha cok bukulur (c > 0 iken sol bacak one gelir).
        CrouchLeg(p, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes,
            58f + 18f * s, -30f + 14f * s - 12f * Mathf.Max(0f, c));
        CrouchLeg(p, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes,
            58f - 18f * s, -30f - 14f * s - 12f * Mathf.Max(0f, -c));
        p.Seg(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 15f - 18f * s, side: 8f);
        p.Seg(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 55f - 10f * s);
        p.Seg(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 15f + 18f * s, side: 8f);
        p.Seg(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 55f + 10f * s);
        return p;
    }

    private static Pose SitIdle(float t)
    {
        float breath = Mathf.Sin(t * Mathf.PI * 2f);
        var p = new Pose { keepFeetOnGround = false, hipsTarget = new Vector3(0f, 0.42f, -0.30f) };
        p.Seg(HumanBodyBones.Spine, HumanBodyBones.Chest, -4f + breath, up: true);
        p.Seg(HumanBodyBones.Chest, HumanBodyBones.UpperChest, -2f, up: true);
        p.Seg(HumanBodyBones.UpperChest, HumanBodyBones.Neck, 0f, up: true);
        p.Seg(HumanBodyBones.Neck, HumanBodyBones.Head, 8f, up: true);
        foreach (bool left in new[] { true, false })
        {
            var upper = left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg;
            var lower = left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg;
            var foot = left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
            var toes = left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes;
            p.Seg(upper, lower, 95f, side: 6f);
            p.Seg(lower, foot, 30f);
            p.Seg(foot, toes, 90f);
        }
        // Eller dizlerin uzerinde.
        p.Seg(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, 8f, side: 6f);
        p.Seg(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, 58f);
        p.Seg(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, 8f, side: 6f);
        p.Seg(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, 58f);
        return p;
    }

    private static void CrouchTorso(Pose p, float lean)
    {
        p.Seg(HumanBodyBones.Spine, HumanBodyBones.Chest, lean, up: true);
        p.Seg(HumanBodyBones.Chest, HumanBodyBones.UpperChest, lean * 0.8f, up: true);
        p.Seg(HumanBodyBones.UpperChest, HumanBodyBones.Neck, lean * 0.4f, up: true);
        p.Seg(HumanBodyBones.Neck, HumanBodyBones.Head, 5f, up: true);
    }

    private static void CrouchLeg(Pose p, HumanBodyBones upper, HumanBodyBones lower, HumanBodyBones foot, HumanBodyBones toes, float thigh, float shin)
    {
        p.Seg(upper, lower, thigh, side: 10f);
        p.Seg(lower, foot, shin);
        p.Seg(foot, toes, 85f);
    }

    private static void RestArms(Pose p, float upper, float fore)
    {
        p.Seg(HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, upper, side: 10f);
        p.Seg(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, fore);
        p.Seg(HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, upper, side: 10f);
        p.Seg(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, fore);
    }

    // ---------------------------------------------------------------- Pisirme

    private struct RawPose
    {
        public Vector3 bodyPosition;
        public Quaternion bodyRotation;
        public float[] muscles;
    }

    private static RawPose ReadPose(AnimationClip clip, float time)
    {
        var raw = new RawPose { muscles = new float[HumanTrait.MuscleCount] };
        var bindings = AnimationUtility.GetCurveBindings(clip).ToDictionary(b => b.propertyName);
        float Get(string name) => bindings.TryGetValue(name, out var b) ? AnimationUtility.GetEditorCurve(clip, b).Evaluate(time) : 0f;
        raw.bodyPosition = new Vector3(Get("RootT.x"), Get("RootT.y"), Get("RootT.z"));
        raw.bodyRotation = new Quaternion(Get("RootQ.x"), Get("RootQ.y"), Get("RootQ.z"), Get("RootQ.w")).normalized;
        for (int i = 0; i < HumanTrait.MuscleCount; i++)
            raw.muscles[i] = Get(CurveName(i));
        return raw;
    }

    private static AnimationClip Bake(Animator animator, HumanPoseHandler handler, RawPose basePose, float length, int samples, Func<float, Pose> poseAt)
    {
        var clip = new AnimationClip { frameRate = 30f };
        var curves = new AnimationCurve[HumanTrait.MuscleCount];
        for (int i = 0; i < curves.Length; i++)
            curves[i] = new AnimationCurve();
        var rootT = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
        var rootQ = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };

        Transform root = animator.transform;
        for (int k = 0; k <= samples; k++)
        {
            float phase = (float)(k % samples) / samples;
            var pose = poseAt(phase);

            // Taban pozdan basla; govdeyi dik ve koke gore ortala.
            var human = new HumanPose
            {
                bodyPosition = new Vector3(0f, basePose.bodyPosition.y, 0f),
                bodyRotation = Quaternion.identity,
                muscles = (float[])basePose.muscles.Clone()
            };
            handler.SetHumanPose(ref human);

            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            if (pose.bodyPitch != 0f)
                hips.rotation = Quaternion.AngleAxis(pose.bodyPitch, Vector3.right) * hips.rotation;

            float groundBefore = LowestPoint(animator, feetOnly: true);
            foreach (var seg in pose.segments)
                Aim(animator, seg);

            // Kalcayi yerine indir.
            Vector3 shift;
            if (pose.hipsTarget.HasValue)
            {
                shift = root.TransformPoint(pose.hipsTarget.Value) - hips.position;
            }
            else
            {
                float lowest = LowestPoint(animator, feetOnly: pose.keepFeetOnGround);
                float target = pose.keepFeetOnGround ? groundBefore : root.position.y + pose.groundClearance;
                shift = new Vector3(0f, target - lowest, 0f);
                if (pose.bodyPitch != 0f)
                {
                    // Yuzustu: vucudun ortasini koke getir.
                    Vector3 head = animator.GetBoneTransform(HumanBodyBones.Head).position;
                    Vector3 feet = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) * 0.5f;
                    shift.z = root.position.z - (head.z + feet.z) * 0.5f;
                }
            }

            handler.GetHumanPose(ref human);
            human.bodyPosition += root.InverseTransformVector(shift) / animator.humanScale;

            float time = phase * length;
            if (k == samples)
                time = length;
            for (int i = 0; i < curves.Length; i++)
                curves[i].AddKey(time, human.muscles[i]);
            for (int a = 0; a < 3; a++)
                rootT[a].AddKey(time, human.bodyPosition[a]);
            for (int a = 0; a < 4; a++)
                rootQ[a].AddKey(time, human.bodyRotation[a]);
        }

        for (int i = 0; i < curves.Length; i++)
            clip.SetCurve("", typeof(Animator), CurveName(i), Smooth(curves[i]));
        string[] axes = { "x", "y", "z", "w" };
        for (int a = 0; a < 3; a++)
            clip.SetCurve("", typeof(Animator), "RootT." + axes[a], Smooth(rootT[a]));
        for (int a = 0; a < 4; a++)
            clip.SetCurve("", typeof(Animator), "RootQ." + axes[a], Smooth(rootQ[a]));

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlendOrientation = true;
        settings.loopBlendPositionY = true;
        settings.loopBlendPositionXZ = true;
        settings.keepOriginalOrientation = true;
        settings.keepOriginalPositionY = true;
        settings.keepOriginalPositionXZ = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static void Aim(Animator animator, Segment seg)
    {
        Transform bone = animator.GetBoneTransform(seg.bone);
        Transform child = animator.GetBoneTransform(seg.child);
        if (seg.child == HumanBodyBones.UpperChest && child == null)
            child = animator.GetBoneTransform(HumanBodyBones.Neck);
        if (bone == null || child == null)
            return;

        Vector3 current = child.position - bone.position;
        Vector3 baseDir = seg.up ? Vector3.up : Vector3.down;
        float pitch = seg.up ? seg.pitch : -seg.pitch;
        Vector3 sagittal = Quaternion.AngleAxis(pitch, Vector3.right) * baseDir;

        // Yan acikligi koru, uzerine disa dogru ek aci ekle.
        float outward = Mathf.Sign(bone.position.x - animator.GetBoneTransform(HumanBodyBones.Hips).position.x);
        if (Mathf.Abs(bone.position.x - animator.GetBoneTransform(HumanBodyBones.Hips).position.x) < 0.02f)
            outward = 0f;
        float keepX = Mathf.Clamp(current.normalized.x, -0.5f, 0.5f);
        Vector3 target = sagittal * Mathf.Sqrt(1f - keepX * keepX) + Vector3.right * keepX;
        if (Mathf.Abs(target.z) > 0.7f)
            target = Quaternion.AngleAxis(outward * seg.side * Mathf.Sign(target.z), Vector3.up) * target;
        else
            target = Quaternion.AngleAxis(outward * seg.side * Mathf.Sign(-target.y), Vector3.forward) * target;

        bone.rotation = Quaternion.FromToRotation(current, target) * bone.rotation;
    }

    private static float LowestPoint(Animator animator, bool feetOnly)
    {
        var bones = feetOnly
            ? new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes }
            : (HumanBodyBones[])Enum.GetValues(typeof(HumanBodyBones));
        float lowest = float.MaxValue;
        foreach (var b in bones)
        {
            if (b == HumanBodyBones.LastBone)
                continue;
            Transform t = animator.GetBoneTransform(b);
            if (t != null)
                lowest = Mathf.Min(lowest, t.position.y);
        }
        return lowest;
    }

    private static AnimationCurve Smooth(AnimationCurve curve)
    {
        for (int i = 0; i < curve.length; i++)
            curve.SmoothTangents(i, 0f);
        return curve;
    }

    // HumanTrait kas adini klipteki egri adina cevirir ("Left Thumb 1 Stretched" -> "LeftHand.Thumb.1 Stretched").
    private static string CurveName(int muscle)
    {
        string name = HumanTrait.MuscleName[muscle];
        string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        var parts = name.Split(' ');
        if (parts.Length >= 3 && (parts[0] == "Left" || parts[0] == "Right") && fingers.Contains(parts[1]))
        {
            return parts[0] + "Hand." + parts[1] + "." + string.Join(" ", parts.Skip(2));
        }
        return name;
    }

    private static void Save(string name, AnimationClip clip)
    {
        string path = OutputFolder + "/" + name + ".anim";
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        clip.name = name;
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            existing.name = name;
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(clip, path);
        }
    }
}
