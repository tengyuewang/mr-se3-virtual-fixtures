using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class ReleaseAudit
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ApkPath = "Build/MR-SE3-Path-Interface.apk";

    [MenuItem("Tools/Release/Validate Project")]
    public static void ValidateProject()
    {
        List<string> errors = new List<string>();
        RequireAsset(ScenePath, errors);
        RequireAsset("Assets/Resources/ROSConnectionPrefab.prefab", errors);
        RequireAsset("Assets/Scripts/PathRecorder.cs", errors);
        RequireAsset("Assets/Scripts/CurvedLineFitting.cs", errors);
        RequireAsset("Assets/Scripts/LaplacianEditing.cs", errors);

        if (errors.Count == 0)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                FindMissingScripts(root.transform, errors);
        }

        if (errors.Count > 0)
            throw new InvalidOperationException("Release validation failed:\n- " + string.Join("\n- ", errors));

        Debug.Log("Release validation passed: required assets exist and the main scene has no missing scripts.");
    }

    [MenuItem("Tools/Release/Build Quest APK")]
    public static void BuildQuestApk()
    {
        ValidateProject();
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath) ?? "Build");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Android build failed: {report.summary.result}");

        Debug.Log($"Quest APK built at {Path.GetFullPath(ApkPath)}");
    }

    private static void RequireAsset(string path, List<string> errors)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null)
            errors.Add($"Missing required asset: {path}");
    }

    private static void FindMissingScripts(Transform transform, List<string> errors)
    {
        int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
        if (missingCount > 0)
            errors.Add($"{GetHierarchyPath(transform)} has {missingCount} missing script(s)");

        for (int i = 0; i < transform.childCount; i++)
            FindMissingScripts(transform.GetChild(i), errors);
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
