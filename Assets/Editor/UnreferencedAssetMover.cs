using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Finds unreferenced assets in a selected folder and moves them to an "_Unreferenced" subfolder.
/// Sprites that only reference themselves (via atlas / importer metadata) are treated as unreferenced.
/// </summary>
public class UnreferencedAssetMover : EditorWindow
{
    // ──────────────────────────────────────────────
    // Settings
    // ──────────────────────────────────────────────
    private string _targetFolder = "Assets";          // folder to scan
    private bool   _recursive    = true;              // scan sub-folders too
    private bool   _dryRun       = true;              // preview before moving
    private Vector2 _scroll;

    // Results
    private List<string> _unreferenced = new();
    private bool _scanned = false;

    // ──────────────────────────────────────────────
    // Menu entry
    // ──────────────────────────────────────────────
    [MenuItem("Tools/Unreferenced Asset Mover")]
    public static void ShowWindow()
    {
        GetWindow<UnreferencedAssetMover>("Unreferenced Assets");
    }

    // ──────────────────────────────────────────────
    // GUI
    // ──────────────────────────────────────────────
    private void OnGUI()
    {
        GUILayout.Label("Unreferenced Asset Finder & Mover", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Folder picker
        EditorGUILayout.BeginHorizontal();
        _targetFolder = EditorGUILayout.TextField("Target Folder", _targetFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string picked = EditorUtility.OpenFolderPanel("Select folder", "Assets", "");
            if (!string.IsNullOrEmpty(picked))
            {
                // Convert absolute path → project-relative
                if (picked.StartsWith(Application.dataPath))
                    _targetFolder = "Assets" + picked.Substring(Application.dataPath.Length);
            }
        }
        EditorGUILayout.EndHorizontal();

        _recursive = EditorGUILayout.Toggle("Recursive", _recursive);
        _dryRun    = EditorGUILayout.Toggle("Dry Run (preview only)", _dryRun);

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan for Unreferenced Assets"))
        {
            _unreferenced = FindUnreferenced(_targetFolder, _recursive);
            _scanned = true;
        }

        if (!_scanned) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Found {_unreferenced.Count} unreferenced asset(s):", EditorStyles.boldLabel);

        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(300));
        foreach (var path in _unreferenced)
            EditorGUILayout.LabelField(path, EditorStyles.miniLabel);
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();

        if (_unreferenced.Count == 0) return;

        string label = _dryRun
            ? "Simulate Move to _Unreferenced/"
            : "Move to _Unreferenced/";

        if (GUILayout.Button(label))
            MoveAssets(_unreferenced, _dryRun);
    }

    // ──────────────────────────────────────────────
    // Core: find unreferenced assets
    // ──────────────────────────────────────────────
    private static List<string> FindUnreferenced(string folder, bool recursive)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            Debug.LogError($"[UnreferencedAssetMover] Invalid folder: {folder}");
            return new List<string>();
        }

        // 1. Collect candidate asset paths inside the target folder
        SearchOption searchOpt = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        string absFolder = Path.Combine(Application.dataPath,
                                        folder.Replace("Assets/", "").Replace("Assets", ""));

        string[] candidatePaths = Directory
            .GetFiles(absFolder, "*", searchOpt)
            .Where(f => !f.EndsWith(".meta"))
            .Select(f => "Assets" + f.Replace(Application.dataPath, "").Replace("\\", "/"))
            .ToArray();

        if (candidatePaths.Length == 0)
        {
            Debug.Log("[UnreferencedAssetMover] No assets found in folder.");
            return new List<string>();
        }

        // 2. Build a set of candidate GUIDs
        var candidateGuids = new HashSet<string>();
        foreach (var path in candidatePaths)
        {
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (!string.IsNullOrEmpty(guid))
                candidateGuids.Add(guid);
        }

        // 3. Collect ALL asset paths in the project (for dependency scanning)
        string[] allProjectGuids = AssetDatabase.FindAssets("", new[] { "Assets" });

        // 4. For every project asset NOT in our candidate set, gather its dependencies
        //    and record which candidate GUIDs are referenced.
        var referenced = new HashSet<string>();

        int total = allProjectGuids.Length;
        for (int i = 0; i < total; i++)
        {
            string guid = allProjectGuids[i];

            // Skip candidates themselves — we only want external references
            if (candidateGuids.Contains(guid)) continue;

            string path = AssetDatabase.GUIDToAssetPath(guid);

            EditorUtility.DisplayProgressBar(
                "Scanning references",
                $"{i}/{total} — {Path.GetFileName(path)}",
                (float)i / total);

            string[] deps = AssetDatabase.GetDependencies(path, false);
            foreach (var dep in deps)
            {
                string depGuid = AssetDatabase.AssetPathToGUID(dep);
                if (candidateGuids.Contains(depGuid))
                    referenced.Add(depGuid);
            }
        }

        EditorUtility.ClearProgressBar();

        // 5. Unreferenced = candidates whose GUID was never found externally
        var unreferenced = candidatePaths
            .Where(p =>
            {
                string guid = AssetDatabase.AssetPathToGUID(p);
                return !string.IsNullOrEmpty(guid) && !referenced.Contains(guid);
            })
            .OrderBy(p => p)
            .ToList();

        Debug.Log($"[UnreferencedAssetMover] Scanned {total} project assets. " +
                  $"Candidates: {candidatePaths.Length} | Unreferenced: {unreferenced.Count}");

        return unreferenced;
    }

    // ──────────────────────────────────────────────
    // Move assets to _Unreferenced subfolder
    // ──────────────────────────────────────────────
    private static void MoveAssets(List<string> paths, bool dryRun)
    {
        if (paths.Count == 0) return;

        // Determine the common root folder for the first asset
        // (all should be under the same scanned folder)
        string firstDir  = Path.GetDirectoryName(paths[0]).Replace("\\", "/");
        string destFolder = firstDir + "/_Unreferenced";

        if (!dryRun && !AssetDatabase.IsValidFolder(destFolder))
        {
            AssetDatabase.CreateFolder(firstDir, "_Unreferenced");
            AssetDatabase.Refresh();
        }

        int moved = 0;
        foreach (var srcPath in paths)
        {
            string srcDir = Path.GetDirectoryName(srcPath).Replace("\\", "/");
            string target = srcDir + "/_Unreferenced/" + Path.GetFileName(srcPath);

            if (dryRun)
            {
                Debug.Log($"[DRY RUN] Would move: {srcPath}  →  {target}");
                moved++;
                continue;
            }

            // Each asset may live in a different subfolder → ensure dest exists
            string localDest = srcDir + "/_Unreferenced";
            if (!AssetDatabase.IsValidFolder(localDest))
                AssetDatabase.CreateFolder(srcDir, "_Unreferenced");

            string error = AssetDatabase.MoveAsset(srcPath, target);
            if (string.IsNullOrEmpty(error))
            {
                moved++;
                Debug.Log($"[UnreferencedAssetMover] Moved: {srcPath}  →  {target}");
            }
            else
            {
                Debug.LogWarning($"[UnreferencedAssetMover] Failed to move {srcPath}: {error}");
            }
        }

        if (!dryRun) AssetDatabase.Refresh();

        string modeLabel = dryRun ? "simulated" : "moved";
        Debug.Log($"[UnreferencedAssetMover] {modeLabel.ToUpper()} {moved}/{paths.Count} asset(s).");
    }
}