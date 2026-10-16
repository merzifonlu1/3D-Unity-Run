using UnityEditor;
using UnityEngine;

// Assets/Player/Animations/Mixamo klasorune atilan Mixamo FBX'lerini (Without Skin)
// Humanoid olarak alir ve dongulu, kok hareketi poza gomulu klip olarak ayarlar.
// Hareketi Rigidbody yaptigi icin klipler karakteri yerinden oynatmaz.
public class MixamoClipImporter : AssetPostprocessor
{
    private const string Folder = "Assets/Player/Animations/Mixamo/";

    private void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Folder))
            return;

        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
    }

    private void OnPreprocessAnimation()
    {
        if (!assetPath.StartsWith(Folder))
            return;

        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        int at = name.IndexOf('@');
        if (at >= 0)
            name = name.Substring(at + 1);

        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = name;
            clips[i].loopTime = true;
            clips[i].lockRootRotation = true;
            clips[i].keepOriginalOrientation = true;
            clips[i].lockRootHeightY = true;
            clips[i].keepOriginalPositionY = true;
            clips[i].lockRootPositionXZ = true;
            clips[i].keepOriginalPositionXZ = false;
        }
        importer.clipAnimations = clips;
    }
}
