using ArcadeRoom.Putting;
using UnityEditor;
using UnityEngine;

namespace ArcadeRoom.Editor.Putting
{
    [CustomEditor(typeof(PuttingStationController))]
    public sealed class PuttingStationControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            var station = (PuttingStationController)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Putting Canvas"))
                {
                    Undo.RecordObject(station, "Build Putting Canvas");
                    station.BuildScoreboardNow();
                    EditorUtility.SetDirty(station);
                    MarkSceneDirty();
                }

                if (GUILayout.Button("Configure Hole Triggers"))
                {
                    Undo.RecordObject(station, "Configure Putting Hole Triggers");
                    station.ConfigureHoleTriggersNow();
                    EditorUtility.SetDirty(station);
                    MarkSceneDirty();
                }
            }
        }

        private static void MarkSceneDirty()
        {
            if (Application.isPlaying)
            {
                return;
            }

            if (UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            }
        }
    }
}
