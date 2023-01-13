using System.Collections.Generic;
using UnityEditor;

public class PackageExporter
{
    // Packages�ȉ����w�肷��ꍇ�t�H���_�p�X�ł͂Ȃ� Packages/{�p�b�P�[�W��} �Ȃ̂Œ���
    private static readonly string _packagePath = "Packages/com.qoopen.particlephysics";
    private static readonly string _fileName = "ParticlePhysics";

    [MenuItem("Tools/ExportPackage")]
    // �K��static�ɂ���
    private static void Export()
    {
        // �o�̓t�@�C����
        var exportPath = $"./{_fileName}.unitypackage";

        var exportedPackageAssetList = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("", new[] { _packagePath }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            exportedPackageAssetList.Add(path);
        }

        AssetDatabase.ExportPackage(
            exportedPackageAssetList.ToArray(),
            exportPath,
            ExportPackageOptions.Recurse);
    }
}