using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

public class FullShaderVariantCollector : EditorWindow, IPreprocessBuildWithReport
{
    #region Fields

    /// Default asset path where the ShaderVariantCollection is stored.
    private static string _collectionPath = "Assets/AllGameShaders.shadervariants";
    private static readonly List<string> SearchRoots = new List<string> { "Assets/_Assets" };

    private Vector2 _scroll;
    public int callbackOrder => 0;

    #endregion

    #region Menu

    /// Adds a custom editor menu entry under Jinnx/Tools.
    [MenuItem("Jinnx/Tools/Shader Variant Collector")]
    public static void ShowWindow()
    {
        GetWindow<FullShaderVariantCollector>("Shader Variant Collector");
    }

    #endregion
    
    #region GUI

    /// Draws the custom editor window GUI.
    private void OnGUI()
        {
            EditorGUILayout.LabelField("Output Path", EditorStyles.boldLabel);
            _collectionPath = EditorGUILayout.TextField(_collectionPath);

            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Search Root Folders", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Only assets inside these folders will be scanned.\nShaders referenced by those assets are included automatically.", MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(120));
            for (int i = 0; i < SearchRoots.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();

                SearchRoots[i] = EditorGUILayout.TextField(SearchRoots[i]);

                if (GUILayout.Button("...", GUILayout.Width(30)))
                {
                    string selected = EditorUtility.OpenFolderPanel("Select Root Folder", "Assets", "");
                    if (!string.IsNullOrEmpty(selected) && selected.StartsWith(Application.dataPath))
                    {
                        string relativePath = "Assets" + selected.Substring(Application.dataPath.Length);
                        SearchRoots[i] = relativePath;
                    }
                    else if (!string.IsNullOrEmpty(selected))
                    {
                        Debug.LogWarning("Folder must be inside the Assets folder.");
                    }
                }

                if (GUILayout.Button("-", GUILayout.Width(25)))
                {
                    SearchRoots.RemoveAt(i);
                    GUIUtility.ExitGUI();
                }

                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("+ Add Folder"))
            {
                SearchRoots.Add("Assets");
            }

            EditorGUILayout.Space(20);

            if (GUILayout.Button("Build Collection (Assets Only)"))
            {
                BuildCollection(_collectionPath, SearchRoots.ToArray());
            }

            if (GUILayout.Button("Editor Warmup Test (Play Mode)"))
            {
                TestWarmup(_collectionPath);
            }
        }

    #endregion

    #region Build Pipeline Hook

    /// Runs automatically before every build. Ensures the shader collection is rebuilt.
    public void OnPreprocessBuild(BuildReport report)
    {
        Debug.Log("Building ShaderVariantCollection before build...");
        BuildCollection(_collectionPath, SearchRoots.ToArray());
    }

    #endregion

    #region Collection Builder

    /// Creates or rebuilds a ShaderVariantCollection from the project.
        private static void BuildCollection(string path, string[] searchFolders)
        {
            ShaderVariantCollection svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
            if (svc == null)
            {
                svc = new ShaderVariantCollection();
                AssetDatabase.CreateAsset(svc, path);
            }

            svc.Clear();
            HashSet<string> added = new HashSet<string>();
            HashSet<Shader> usedShaders = new HashSet<Shader>();
            int variantCount = 0;

            // === Scan Materials ===
            string[] matGuids = AssetDatabase.FindAssets("t:Material", searchFolders);
            foreach (var guid in matGuids)
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                AddMaterialVariants(mat, svc, added, ref variantCount);
                if (mat != null && mat.shader != null) usedShaders.Add(mat.shader);
            }

            // === Scan Prefabs ===
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", searchFolders);
            foreach (var guid in prefabGuids)
            {
                string pathPrefab = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathPrefab);
                if (prefab == null) continue;

                ScanGameObject(prefab, svc, added, usedShaders, ref variantCount);
            }

            // === Add "raw" shaders referenced by mats ===
            foreach (var shader in usedShaders)
            {
                var variant = new ShaderVariantCollection.ShaderVariant
                {
                    shader = shader,
                    passType = PassType.Normal,
                    keywords = Array.Empty<string>()
                };

                string key = shader.name + "_Normal";
                if (!added.Contains(key))
                {
                    svc.Add(variant);
                    added.Add(key);
                    variantCount++;
                }
            }

            EditorUtility.SetDirty(svc);
            AssetDatabase.SaveAssets();

            Debug.Log($"ShaderVariantCollection built with {variantCount} variants across {svc.shaderCount} shaders at {path}");
        }

    #endregion

    #region Helpers

    /// Scan and Add into collection.
    
    private static void ScanGameObject(GameObject go, ShaderVariantCollection svc, HashSet<string> added, HashSet<Shader> usedShaders, ref int variantCount)
    {
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var mat in renderer.sharedMaterials)
            {
                AddMaterialVariants(mat, svc, added, ref variantCount);
                if (mat != null && mat.shader != null) usedShaders.Add(mat.shader);
            }
        }

        foreach (var ps in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            foreach (var mat in ps.sharedMaterials)
            {
                AddMaterialVariants(mat, svc, added, ref variantCount);
                if (mat != null && mat.shader != null) usedShaders.Add(mat.shader);
            }
        }

#if UNITY_2019_3_OR_NEWER
            foreach (var vfx in go.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>(true))
            {
                var so = new SerializedObject(vfx);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                        prop.objectReferenceValue is Material m)
                    {
                        AddMaterialVariants(m, svc, added, ref variantCount);
                        if (m.shader != null) usedShaders.Add(m.shader);
                    }
                }
            }
#endif
    }
    
    private static void AddMaterialVariants(Material mat, ShaderVariantCollection svc, HashSet<string> added, ref int variantCount)
    {
        if (mat == null || mat.shader == null) return;

        string[] keywords = mat.shaderKeywords;

        var variant = new ShaderVariantCollection.ShaderVariant
        {
            shader = mat.shader,
            passType = PassType.Normal,
            keywords = keywords
        };

        string key = mat.shader.name + "_Normal_" + string.Join("_", keywords);
        if (!added.Contains(key))
        {
            svc.Add(variant);
            added.Add(key);
            variantCount++;
        }
    }

    #endregion
    
    #region Warmup

    /// Editor test. Warms up the ShaderVariantCollection immediately during Play Mode. Useful to confirm hitching is removed without building the game.
    private static void TestWarmup(string path)
    {
        ShaderVariantCollection svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        if (svc == null)
        {
            Debug.LogWarning("No ShaderVariantCollection found. Run Build Collection first.");
            return;
        }

        int before = svc.variantCount;
        svc.WarmUp();
        Debug.Log($"[Editor Warmup Test] Warmed up {before} shader variants.");
    }

    #endregion
}