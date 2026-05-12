using ArcadeRoom.YoTyan;
using UnityEditor;
using UnityEngine;

namespace ArcadeRoom.Editor.YoTyan
{
    [CustomEditor(typeof(YoTyanStationController))]
    public class YoTyanStationControllerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

            var station = (YoTyanStationController)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Assign Home Spawn Points"))
                {
                    Undo.RecordObject(station, "Assign YoTyan Home Spawn Points");
                    station.AssignHomeSpawnPoints();
                    EditorUtility.SetDirty(station);
                    EditorSceneMarkDirty();
                }

                if (GUILayout.Button("Refresh Spawn Point Lists"))
                {
                    Undo.RecordObject(station, "Refresh YoTyan Spawn Point Lists");
                    station.RefreshSpawnPointLists();
                    EditorUtility.SetDirty(station);
                    EditorSceneMarkDirty();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Build Scoreboard UI"))
                {
                    station.BuildScoreboardNow();
                    EditorUtility.SetDirty(station);
                    EditorSceneMarkDirty();
                }

                if (GUILayout.Button("Spawn Balls Now"))
                {
                    station.SpawnBallsNow();
                    EditorUtility.SetDirty(station);
                    EditorSceneMarkDirty();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset Spawned Balls To Home"))
                {
                    station.ResetAllBallsToHome();
                    EditorUtility.SetDirty(station);
                    EditorSceneMarkDirty();
                }
            }
        }

        private static void EditorSceneMarkDirty()
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
