using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;
using VV.Scoring.Data;

namespace VV.Scoring
{
    public enum ScoreFilterMode
    {
        Intersection,
        Union,
        SymmetricDifference,
        RelativeComplement,
        AbsoluteComplement
    }

    public static class ScoreManager
    {
        /// <summary>
        /// Dictionary containing scores.
        /// Identified by ScoreData's GUID for O(1) search.
        /// </summary>
        private static readonly Dictionary<Guid, ScoreData> scoresById = new();

        /// <summary>
        /// Optional indexes for fast filtering: key -> (value -> set of ids)
        /// Maintains simple inverted index for frequently queried metadata keys.
        /// Example : If the keysToIndex contains the index "Feature", then you can do ;
        /// indexes["Feature"]["Collectable"] to find every data containing these key value pair in their metaData.
        /// Limits : Only used for the Intersection search mode
        /// </summary>
        private static readonly Dictionary<string, Dictionary<string, HashSet<Guid>>> indexes = new();

        /// <summary>
        /// Optional key to index set.
        /// Configure which metadata keys to index for faster queries.
        /// Example : If the keysToIndex contains the index "Feature",
        /// then every data containing this key will be stored in the indexes Dictionary.
        /// </summary>
        private static readonly HashSet<string> keysToIndex = new();

        #region Events

        public static event UnityAction<ScoreData> ScoreAdded;
        public static event UnityAction<ScoreData, int> ScoreUpdated;
        public static event UnityAction<ScoreData> ScoreRemoved;

        #endregion

        #region CRUD

        /// <summary>
        /// Clears all scores and related indexes.
        /// </summary>
        public static void ClearAll()
        {
            scoresById.Clear();
            indexes.Clear();
        }

        public static bool Exists(Guid id) => scoresById.ContainsKey(id);

        public static bool Exists(ScoreData scoreData) => scoreData != null && scoresById.ContainsKey(scoreData.Id);
        
        public static bool Exists(Dictionary<string, object> filter)
        {
            return GetScores(filter).Count > 0;
        }

        public static void AddScore(ScoreData scoreData)
        {
            if (scoreData == null) return;
            if (!scoresById.TryAdd(scoreData.Id, scoreData)) return;

            IndexAdd(scoreData);
            ScoreAdded?.Invoke(scoreData);
        }

        /// <summary>
        /// Add or update using id equality or using filters (if provided).
        /// </summary>
        public static void AddOrUpdateScore(ScoreData scoreData, Dictionary<string, object> filters = null,
            ScoreMergingRule updateMergingRule = ScoreMergingRule.OverrideAll)
        {
            if (scoreData == null) return;

            if (scoresById.TryGetValue(scoreData.Id, out ScoreData existing))
            {
                // Update by id
                UpdateScoreInternal(existing, scoreData, updateMergingRule);
                return;
            }

            if (filters is { Count: > 0 })
            {
                var found = GetScores(filters, ScoreFilterMode.Intersection);
                if (found.Count > 0)
                {
                    ScoreData first = found[0];
                    UpdateScoreInternal(first, scoreData, updateMergingRule);
                    return;
                }
            }

            AddScore(scoreData);
        }

        public static void UpdateScore(ScoreData scoreData)
        {
            if (scoreData == null) return;
            if (!scoresById.TryGetValue(scoreData.Id, out var existing)) return;

            var delta = Math.Abs(scoreData.Score - existing.Score);

            // Replace scalar fields (keep identity)
            existing.Score = scoreData.Score;
            existing.Name = scoreData.Name;

            // Replace metadata (full override)
            // Remove old index references, then reindex
            IndexRemove(existing);
            existing.MetaData.AddRange(new Dictionary<string, object>(scoreData.MetaData.Data));
            IndexAdd(existing);

            ScoreUpdated?.Invoke(existing, delta);
        }

        private static void UpdateScoreInternal(ScoreData existing, ScoreData incoming, ScoreMergingRule mergingRule)
        {
            // Remove old index references before changing metadata
            IndexRemove(existing);

            existing.Merge(incoming, mergingRule);

            // Reindex
            IndexAdd(existing);

            ScoreUpdated?.Invoke(existing, Math.Abs(incoming.Score));
        }

        public static void RemoveScore(Guid id)
        {
            if (!scoresById.Remove(id, out ScoreData score)) return;
            IndexRemove(score);
            ScoreRemoved?.Invoke(score);
        }

        public static void RemoveScore(ScoreData scoreData)
        {
            if (scoreData == null) return;
            RemoveScore(scoreData.Id);
        }

        public static void RemoveScores(Dictionary<string, object> filter)
        {
            var toRemove = GetScores(filter, ScoreFilterMode.Intersection);
            foreach (ScoreData s in toRemove)
                RemoveScore(s.Id);
        }

        #endregion

        #region Indexing helpers

        public static void SetKeysToIndex(List<string> keys)
        {
            keysToIndex.Clear();
            foreach (var key in keys)
            {
                keysToIndex.Add(key);
            }
        }

        public static void AddKeyToIndex(string key)
        {
            keysToIndex.Add(key);
        }

        public static void RemoveKeyFromIndex(string key)
        {
            keysToIndex.Remove(key);
        }

        private static void IndexAdd(ScoreData score)
        {
            if (score?.MetaData == null) return;
            foreach (var key in keysToIndex)
            {
                if (!score.MetaData.TryGet<string>(key, out var valueStr))
                    continue;

                if (string.IsNullOrEmpty(valueStr)) continue;

                if (!indexes.TryGetValue(key, out var map))
                {
                    map = new Dictionary<string, HashSet<Guid>>();
                    indexes[key] = map;
                }

                if (!map.TryGetValue(valueStr, out var set))
                {
                    set = new HashSet<Guid>();
                    map[valueStr] = set;
                }

                set.Add(score.Id);
            }
        }

        private static void IndexRemove(ScoreData score)
        {
            if (score?.MetaData == null) return;
            foreach (var key in keysToIndex)
            {
                if (!score.MetaData.TryGet<string>(key, out var valueStr))
                    continue;

                if (!indexes.TryGetValue(key, out var map) || !map.TryGetValue(valueStr, out var set)) continue;
                set.Remove(score.Id);
                if (set.Count == 0)
                    map.Remove(valueStr);
            }
        }

        #endregion

        #region Querying / Filtering

        /// <summary>
        /// Generic internal enumerator over all ScoreData.
        /// Used for lazy iterations :
        /// Returns the ScoreData one by one.
        /// The search does not allocate the entire set, leading to a better efficiency for large amount of data.
        /// </summary>
        /// <returns></returns>
        private static IEnumerable<ScoreData> AllScoresEnum()
        {
            foreach (var kv in scoresById)
                yield return kv.Value;
        }

        /// <summary>
        /// Fast path: if all filters are indexed and using Intersection mode, compute set intersection.
        /// Otherwise fallback to scan.
        /// </summary>
        public static List<ScoreData> GetScores(Dictionary<string, object> filters, ScoreFilterMode mode = ScoreFilterMode.Intersection)
        {
            if (filters == null || filters.Count == 0)
                return new List<ScoreData>(scoresById.Values);

            // Try indexed fast-path for intersection with string values
            if (mode == ScoreFilterMode.Intersection)
            {
                var idsSets = new List<HashSet<Guid>>();
                bool canUseIndex = true;

                foreach (var f in filters)
                {
                    var key = f.Key;
                    var val = f.Value?.ToString();
                    if (val == null || !indexes.TryGetValue(key, out var map) || !map.TryGetValue(val, out var set))
                    {
                        canUseIndex = false;
                        break;
                    }

                    idsSets.Add(set);
                }

                if (canUseIndex && idsSets.Count > 0)
                {
                    // intersect the id sets (smallest first)
                    idsSets.Sort((a, b) => a.Count - b.Count);
                    var resultIds = new HashSet<Guid>(idsSets[0]);
                    for (int i = 1; i < idsSets.Count; i++)
                        resultIds.IntersectWith(idsSets[i]);

                    var result = new List<ScoreData>(resultIds.Count);
                    foreach (Guid id in resultIds)
                        if (scoresById.TryGetValue(id, out var sd))
                            result.Add(sd);

                    return result;
                }
            }

            // Fallback: full scan, custom logic per mode, avoid LINQ to reduce allocations
            var resultList = new List<ScoreData>();

            foreach (ScoreData sd in AllScoresEnum())
            {
                ScoreMetaData meta = sd.MetaData;
                int matchCount = 0;
                foreach (var f in filters)
                {
                    if (meta.TryGet<object>(f.Key, out var v) && v != null && v.Equals(f.Value))
                        matchCount++;
                }

                bool add = mode switch
                {
                    ScoreFilterMode.Intersection => matchCount == filters.Count,
                    ScoreFilterMode.Union => matchCount > 0,
                    ScoreFilterMode.AbsoluteComplement => matchCount == 0,
                    ScoreFilterMode.SymmetricDifference => matchCount == 1,
                    ScoreFilterMode.RelativeComplement =>
                        filters.Count == 1 ? true :
                        // first matches and none of the others
                        (meta.TryGet<object>(filters.Keys.First(), out var fv) && fv != null && fv.Equals(filters.Values.First())
                         && matchCount == 1),
                    _ => false
                };

                if (add) resultList.Add(sd);
            }

            return resultList;
        }

        public static int GetScore(Dictionary<string, object> filters = null, ScoreFilterMode mode = ScoreFilterMode.Intersection)
        {
            var list = GetScores(filters, mode);
            int s = 0;
            foreach (ScoreData item in list) s += item.Score;
            return s;
        }

        public static List<ScoreData> GetAllScores() => new List<ScoreData>(scoresById.Values);

        #endregion
    }
}
