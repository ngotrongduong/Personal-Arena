using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersonalArena.ML.Editor
{
    public static class TrainingSceneBuilder
    {
        public const string TrainingScenePath = "Assets/Scenes/Training.unity";

        [MenuItem("Personal Arena/Build Training Scene")]
        public static void Build()
        {
            string sceneDirectory = Path.Combine(Application.dataPath, "Scenes");
            if (!Directory.Exists(sceneDirectory))
            {
                Directory.CreateDirectory(sceneDirectory);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject cameraObject = new GameObject("Training Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(0f, 20f, -20f),
                Quaternion.Euler(35f, 0f, 0f));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;

            GameObject lightObject = new GameObject("Directional Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject hostObject = new GameObject("Training Arena Host");
            hostObject.AddComponent<TrainingArenaHost>();

            EditorSceneManager.SaveScene(scene, TrainingScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Saved training scene to {TrainingScenePath}.");
        }
    }
}
