using System.Collections.Generic;
using System.Linq;
using AYellowpaper.SerializedCollections;
using DA_Assets.Extensions;
using UnityEngine;
using VV.Scoring.Data;
using VV.Utility.SerializedTools;

namespace VV.Scoring.Debugger
{
    /// <summary>
    /// Tool used to debug the scores stored in the ScoreManager.
    /// Current tools :
    /// - See all the scores stored
    /// - See scorees filtered by 
    /// </summary>
    public class ScoreDebugger : ScriptableObject
    {
#if UNITY_EDITOR
        [Header("Filtered Scores")]
        [SerializeField] private SerializedDictionary<string, SerializedValueWrapper> filter;
        [SerializeField, SerializeReference] private PaginatedSerializedList<ScoreData> filteredScores = new();

        [SerializeField, SerializeReference]
        private PaginatedSerializedList<ScoreData> scores = new();

        private void OnEnable()
        {
            scores = ScoreManager.GetAllScores();
            // subscribe only in editor
            ScoreManager.ScoreAdded += OnScoreAdded;
            ScoreManager.ScoreUpdated += OnScoreUpdated;
            ScoreManager.ScoreRemoved += OnScoreRemoved;
        }

        private void OnDisable()
        {
            filteredScores.Clear();
            scores.Clear();
            ScoreManager.ScoreAdded -= OnScoreAdded;
            ScoreManager.ScoreUpdated -= OnScoreUpdated;
            ScoreManager.ScoreRemoved -= OnScoreRemoved;
        }

        private void OnScoreAdded(ScoreData scoreData)
        {
            scores.Add(scoreData);
        }

        private void OnScoreUpdated(ScoreData scoreData, int diff)
        {
            int index = scores.FindIndex(s => s.Equals(scoreData));
            if (index >= 0)
                scores[index] = scoreData;
        }

        private void OnScoreRemoved(ScoreData scoreData)
        {
            scores.Remove(scoreData);
        }

        public void OnValidate()
        {
            UpdateFilter();
        }

        private void UpdateFilter()
        {
            filteredScores.Clear();
            
            if(filter.IsEmpty()) return;

            if (filter.Any(kv => string.IsNullOrEmpty(kv.Key) || kv.Value == null)) return;
            
            Dictionary<string, object> f = filter.ToDictionary(x => x.Key, x => x.Value.Value);

            filteredScores = ScoreManager.GetScores(f);
        }
#endif
    }
}