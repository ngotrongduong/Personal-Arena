using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace PersonalArena.View.Editor
{
    public static class PlaySceneBuilder
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = SceneFolder + "/ArenaPlay.unity";
        public const string WatchScenePath = SceneFolder + "/ArenaWatch.unity";

        [MenuItem("Personal Arena/Build Play Scene")]
        public static void Build()
        {
            Run(() =>
            {
                BuildScene(ScenePath, typeof(KeyboardArenaController));
                SetFirstBuildScene(ScenePath);
                Debug.Log("Built Personal Arena play scene at " + ScenePath);
            });
        }

        [MenuItem("Personal Arena/Build Watch AI Scene")]
        public static void BuildWatch()
        {
            Run(() =>
            {
                BuildScene(WatchScenePath, typeof(AiArenaController));
                Debug.Log("Built Personal Arena watch-AI scene at " + WatchScenePath);
            });
        }

        private static void Run(Action build)
        {
            try
            {
                build();
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(0);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode)
                {
                    EditorApplication.Exit(1);
                    return;
                }
                throw;
            }
        }

        internal static void BuildScene(string scenePath, Type controllerType)
        {
            EnsureSceneFolder();
            KayKitArtSetBuilder.EnsureArtSet();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a scene unloads unused assets, which would drop an earlier reference.
            ArenaArtSet artSet = AssetDatabase.LoadAssetAtPath<ArenaArtSet>(KayKitArtSetBuilder.AssetPath);

            // Moody floating-arena light: a cool moon-like key plus flat ambient; braziers add warm pools at runtime.
            // ArenaStage retunes the fog every frame from the camera distance so the abyss always fades to violet.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.28f, 0.28f, 0.4f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.035f, 0.08f);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 75f;
            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.8f, 0.84f, 1f);
            light.intensity = 0.75f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.75f;
            lightObject.transform.rotation = Quaternion.Euler(52f, -28f, 0f);

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.depth = 0f;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<TopDownCamera>();

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            GameObject arenaObject = new GameObject("Arena");
            ArenaRenderer arenaRenderer = arenaObject.AddComponent<ArenaRenderer>();
            SerializedObject rendererObject = new SerializedObject(arenaRenderer);
            rendererObject.FindProperty("artSet").objectReferenceValue = artSet;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            if (artSet == null || new SerializedObject(arenaRenderer).FindProperty("artSet").objectReferenceValue == null)
            {
                throw new InvalidOperationException("Arena art set was not assigned to the renderer.");
            }
            arenaObject.AddComponent<ArenaHud>();
            arenaObject.AddComponent(controllerType);

            EditorSceneManager.SaveScene(scene, scenePath);
            EnsureAlwaysIncludedShader("Standard");
            EnsureAlwaysIncludedShader(FxAssets.ShaderName);
            Selection.activeGameObject = arenaObject;
            AssetDatabase.SaveAssets();
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }
        }

        // Materials are created at runtime with Shader.Find, so the shader must be forced into player builds.
        private static void EnsureAlwaysIncludedShader(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (shader == null || assets == null || assets.Length == 0)
            {
                Debug.LogError($"Could not add '{shaderName}' to Always Included Shaders.");
                return;
            }

            SerializedObject graphicsSettings = new SerializedObject(assets[0]);
            SerializedProperty shaders = graphicsSettings.FindProperty("m_AlwaysIncludedShaders");
            for (int i = 0; i < shaders.arraySize; i++)
            {
                if (shaders.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    return;
                }
            }

            int index = shaders.arraySize;
            shaders.InsertArrayElementAtIndex(index);
            shaders.GetArrayElementAtIndex(index).objectReferenceValue = shader;
            graphicsSettings.ApplyModifiedProperties();
        }

        private static void SetFirstBuildScene(string path)
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(existing.Length + 1)
            {
                new EditorBuildSettingsScene(path, true)
            };
            for (int i = 0; i < existing.Length; i++)
            {
                if (!string.Equals(existing[i].path, path, StringComparison.OrdinalIgnoreCase))
                {
                    scenes.Add(existing[i]);
                }
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
