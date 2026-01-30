using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using UnityEngine;
using VV.Utility;

namespace VV.Scoring.Data
{
    /// <summary>
    /// Single feature score configuration.
    /// </summary>
    [CreateAssetMenu(menuName = "VV/Scoring/ScoreConfigSO", fileName = "ScoreConfig")]
    public class ScoreConfigSO : ScriptableObject, IScorable
    {
        [Tooltip("Sets the score to an individual feature.")]
        [SerializeField] protected int score;
        [Tooltip("Identifiers for this score. Allows to apply filters. Will be added to the meta-data.")]
        [SerializeField] protected SerializedDictionary<string, string> ids;
        
        [SerializeField] private List<GeneratedFieldDataGetter> fieldDataGetters = new();

        public Dictionary<string, object> Ids
        {
            get
            {
                var result = new Dictionary<string, object>();
                foreach (var kvp in ids)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
            set
            {
                ids.Clear();
                foreach (var kvp in value)
                {
                    ids[kvp.Key] = kvp.Value?.ToString();
                }
            }
        }

        public int Score
        {
            get => score;
            set => score = value;
        }
    }
}