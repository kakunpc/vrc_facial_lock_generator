using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ExportPackage
{
    [MenuItem("kakunvr/ExportPackage")]
    public static void CreatePackage()
    {
        AssetDatabase.ExportPackage(new[]
        {
            "Packages/com.kakunvr.facial-lock-generator",
        }, "ExportedPackage.unitypackage", ExportPackageOptions.Recurse);
    }
}
