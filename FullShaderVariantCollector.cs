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

    /// Required by IPreprocessBuildWithReport. Controls callback execution order.
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

        if (GUILayout.Button("Build Collection From Project Assets"))
        {
            BuildCollection(_collectionPath);
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
        BuildCollection(_collectionPath);
    }

    #endregion

    #region Collection Builder

    /// Creates or rebuilds a ShaderVariantCollection from all materials, prefabs, particle systems, VFX graphs, and shaders found in the project.
    private static void BuildCollection(string path)
    {
        ShaderVariantCollection svc = AssetDatabase.LoadAssetAtPath<ShaderVariantCollection>(path);
        if (svc == null)
        {
            svc = new ShaderVariantCollection();
            AssetDatabase.CreateAsset(svc, path);
        }

        svc.Clear();
        HashSet<string> added = new HashSet<string>();
        int variantCount = 0;

        /// --- Materials ---
        string[] matGuids = AssetDatabase.FindAssets("t:Material");
        foreach (var guid in matGuids)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            AddMaterialVariants(mat, svc, added, ref variantCount);
        }

        /// --- Prefabs ---
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in prefabGuids)
        {
            string pathPrefab = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pathPrefab);
            if (prefab == null) continue;

            // Renderers
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    AddMaterialVariants(mat, svc, added, ref variantCount);
                }
            }

            // Particle Systems
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                foreach (var mat in ps.sharedMaterials)
                {
                    AddMaterialVariants(mat, svc, added, ref variantCount);
                }
            }

            // VFX Graphs
#if UNITY_2019_3_OR_NEWER
            foreach (var vfx in prefab.GetComponentsInChildren<VisualEffect>(true))
            {
                var so = new SerializedObject(vfx);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (prop.propertyType == SerializedPropertyType.ObjectReference &&
                        prop.objectReferenceValue is Material m)
                    {
                        AddMaterialVariants(m, svc, added, ref variantCount);
                    }
                }
            }
#endif
        }

        /// --- Shaders (includes Shader Graphs) ---
        string[] shaderGuids = AssetDatabase.FindAssets("t:Shader");
        foreach (var guid in shaderGuids)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(AssetDatabase.GUIDToAssetPath(guid));
            if (shader == null) continue;

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

        Debug.Log(
            $"ShaderVariantCollection built with {variantCount} variants across {svc.shaderCount} shaders at {path}");
    }

    #endregion

    #region Helpers

    /// Adds a material’s shader variants (with keywords) into the collection.
    private static void AddMaterialVariants(Material mat, ShaderVariantCollection svc, HashSet<string> added,
        ref int variantCount)
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

    /// Editor-only test. Warms up the ShaderVariantCollection immediately during Play Mode. Useful to confirm hitching is removed without building the game.
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


/// # Shader Variant Collector (Unity Editor Tool)

// This editor utility collects **all shaders, materials, prefabs, particle systems, and VFX graphs** in the project into a single `ShaderVariantCollection` asset.  
// The collection ensures that required shader variants are precompiled and reduces runtime hitching due to on-demand shader compilation. Can definitely be improved, so feel free to change it to your liking or to fit your project/needs.

// ## Features
// - Collects shader variants from:
//   - Materials
//   - Prefabs (Renderers, ParticleSystems, VFX Graphs)
//   - Shader assets (including Shader Graphs)
// - Runs automatically before builds.
// - Provides an editor window for manual collection and warmup testing.

// <img width="370" height="424" alt="Shader Variant Window" src="https://github.com/user-attachments/assets/adb5b7e6-16b4-4eb2-89d7-9b5f56595fe9" />

// ## Usage

// 1. Open via menu: **Jinnx → Tools → Shader Variant Collector**.
// <img width="408" height="169" alt="Shader Variant" src="https://github.com/user-attachments/assets/c937177b-6543-4c2d-99c5-d957e325eca3" />

// 2. Set output path for the collection asset (default: `Assets/AllGameShaders.shadervariants`).

// 3. Click:
//    - **Build Collection From Project Assets** – creates/updates the collection.
//    - **Editor Warmup Test** – forces a warmup of all variants in Play Mode for hitch-testing.

// ## Build Integration
// - The collection automatically rebuilds before each build via `IPreprocessBuildWithReport`.

// ## Notes
// - Keep the collection path under `Assets/` so it is included in version control.
// - Run **Editor Warmup Test** to confirm shader variants are available and avoid runtime compilation spikes.