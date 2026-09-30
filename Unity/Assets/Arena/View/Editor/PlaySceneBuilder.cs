using System;
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
        public const string WatchScenePath = SceneFolder + "/ArenaWatch.unity";

        [MenuItem("Personal Arena/Build Watch AI Scene")]
        public static void BuildWatch()
        {
            Run(() =>
            {
                BuildSurvivorScene(WatchScenePath);
                Debug.Log("Built Personal Arena watch-AI (survivor) scene at " + WatchScenePath);
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

        /// <summary>Builds the survivor "Watch AI" scene: moonlit graveyard, follow camera, renderer, HUD and controller.</summary>
        internal static void BuildSurvivorScene(string scenePath)
        {
            EnsureSceneFolder();
            KayKitArtSetBuilder.EnsureArtSet();
            SurvivorArtSetBuilder.EnsureArtSet();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // Load after NewScene: opening a scene unloads unused assets, which would drop an earlier reference.
            ArenaArtSet artSet = AssetDatabase.LoadAssetAtPath<ArenaArtSet>(KayKitArtSetBuilder.AssetPath);
            SurvivorArtSet survivorArt = AssetDatabase.LoadAssetAtPath<SurvivorArtSet>(SurvivorArtSetBuilder.AssetPath);
            if (artSet == null || survivorArt == null)
            {
                throw new InvalidOperationException("Survivor art sets are missing.");
            }

            // Moonlit graveyard: cool key light, bluish ambient and a dark violet fog that hides the map edge.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.32f, 0.33f, 0.46f);
            // No daylight skybox: its reflections would grey out the dark night ground and props.
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 0.25f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.06f, 0.05f, 0.1f);
            RenderSettings.fogStartDistance = 34f;
            RenderSettings.fogEndDistance = 80f;
            GameObject lightObject = new GameObject("Moon Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.78f, 0.84f, 1f);
            light.intensity = 0.85f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -32f, 0f);

            GameObject survivorObject = new GameObject("Survivor");
            SurvivorRenderer survivorRenderer = survivorObject.AddComponent<SurvivorRenderer>();
            SerializedObject rendererObject = new SerializedObject(survivorRenderer);
            rendererObject.FindProperty("artSet").objectReferenceValue = artSet;
            rendererObject.FindProperty("survivorArt").objectReferenceValue = survivorArt;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            SurvivorHud hud = survivorObject.AddComponent<SurvivorHud>();
            SurvivorWatchController controller = survivorObject.AddComponent<SurvivorWatchController>();

            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.depth = 0f;
            cameraObject.AddComponent<AudioListener>();
            SurvivorCamera followCamera = cameraObject.AddComponent<SurvivorCamera>();
            SerializedObject cameraSettings = new SerializedObject(followCamera);
            cameraSettings.FindProperty("target").objectReferenceValue = survivorRenderer;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject controllerObject = new SerializedObject(controller);
            controllerObject.FindProperty("survivorRenderer").objectReferenceValue = survivorRenderer;
            controllerObject.FindProperty("hud").objectReferenceValue = hud;
            controllerObject.FindProperty("followCamera").objectReferenceValue = followCamera;
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();

            EditorSceneManager.SaveScene(scene, scenePath);
            EnsureAlwaysIncludedShader("Standard");
            EnsureAlwaysIncludedShader(FxAssets.ShaderName);
            Selection.activeGameObject = survivorObject;
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
    }
}
