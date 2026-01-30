using System.Collections.Generic;
using UnityEngine.UIElements;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VV.Scoring.Settings
{
#if UNITY_EDITOR
    public class ScoreSettingsProvider : SettingsProvider
    {
        private SerializedObject settings;
        
        public ScoreSettingsProvider(string path, SettingsScope scopes, IEnumerable<string> keywords = null) 
            : base(path, scopes, keywords)
        { }
        
        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            // This function is called when the user clicks on the MyCustom element in the Settings window.
            settings = ScoreSettings.GetSerializedSettings();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.PropertyField(settings.FindProperty("scoreConfigs"));
            
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Register the SettingsProvider
        /// </summary>
        /// <returns></returns>
        [SettingsProvider]
        public static SettingsProvider CreateCustomSettingsProvider()
        {
            // Settings Asset doesn't exist yet; no need to display anything in the Settings window.
            return new ScoreSettingsProvider("Project/VV/Scoring", SettingsScope.Project);
        }
    }
#endif
}