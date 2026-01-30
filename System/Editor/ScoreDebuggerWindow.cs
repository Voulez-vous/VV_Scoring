using UnityEditor;
using UnityEngine;
using VV.Scoring.Debugger;

namespace VV.Scoring.Editor
{
    public class ScoreDebuggerWindow : EditorWindow
    {
        private ScoreDebugger debugger;
        private SerializedObject serializedDebugger;

        [MenuItem("VV/ScoringSystem/Debugger")]
        public static void ShowWindow()
        {
            GetWindow<ScoreDebuggerWindow>("Score Debugger");
        }

        private void OnEnable()
        {
            // Load or create instance
            if (debugger == null)
            {
                debugger = CreateInstance<ScoreDebugger>();
            }

            serializedDebugger = new SerializedObject(debugger);
            
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }
        
        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        private void OnGUI()
        {
            serializedDebugger.Update();

            SerializedProperty iterator = serializedDebugger.GetIterator();
            iterator.NextVisible(true);

            while (iterator.NextVisible(false))
            {
                EditorGUILayout.PropertyField(iterator, true);
            }

            serializedDebugger.ApplyModifiedProperties();
        }
        
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            // Close window when leaving play mode (stopping the player)
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Close();
            }
        }
    }
}