using System.Collections.Generic;
using UnityEngine;
using VV.Scoring.Data;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VV.Scoring.Settings
{
    public class ScoreSettings : ScriptableObject
    {
        public static string SettingsName => "ScoreSettings";
        public static string SettingsPath => $"Assets/Resources/{SettingsName}.asset";
        
        public List<ScoreConfigSO> scoreConfigs;

#if UNITY_EDITOR
        private static ScoreSettings GetOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ScoreSettings>(SettingsPath);
            
            if (settings != null) return settings;
            
            settings = CreateInstance<ScoreSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();

            return settings;
        }

        public static SerializedObject GetSerializedSettings()
        {
            return new SerializedObject(GetOrCreateSettings());
        }
#endif
    }
}