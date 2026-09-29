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

        [MenuItem("Personal Arena/Build Play Scene")]
        public static void Build()
        {
            try
            {
                BuildScene();
                Debug.Log("Built Personal Arena play scene at " + ScenePath);
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

        private static void BuildScene()
        {
            EnsureSceneFolder();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

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
            arenaObject.AddComponent<ArenaRenderer>();
            arenaObject.AddComponent<ArenaHud>();
            arenaObject.AddComponent<KeyboardArenaController>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SetFirstBuildScene(ScenePath);
            EnsureAlwaysIncludedShader("Standard");
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
