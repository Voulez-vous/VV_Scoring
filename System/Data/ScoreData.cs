using System;
using System.Collections.Generic;
using UnityEngine;

namespace VV.Scoring.Data
{
    public enum ScoreMergingRule
    {
        AllowDuplicates,
        OverrideAll,
        OverrideMetaData,
        IgnoreIncoming
    }
    
    /// <summary>
    /// Runtime score data storage with efficient clone/merge semantics.
    /// </summary>
    [Serializable]
    public class ScoreData : IEquatable<ScoreData>, ICloneable
    {
        // Use a stable GUID for identity in runtime. This GUID is created locally.
        private readonly Guid guid;

        [SerializeField] private string scoreName;
        [SerializeField] private int score;
        [SerializeField] private ScoreMetaData metaData;

        public Guid Id => guid;
        public string Name { get => scoreName; set => scoreName = value; }
        public int Score { get => score; set => score = value; }
        public ScoreMetaData MetaData => metaData;

        public ScoreData()
        {
            guid = Guid.NewGuid();
            metaData = new ScoreMetaData();
            score = 0;
        }

        public ScoreData(int score = 0, string name = null) : this()
        {
            this.score = score;
            this.scoreName = name;
        }

        public ScoreData(int score, IDictionary<string, object> metaData, string name = null) : this(score: score, name: name)
        {
            if (metaData != null) this.metaData.AddRange(metaData);
        }
        
        public ScoreData(ScoreConfigSO scoreConfig) 
            : this(score: scoreConfig.Score, metaData: scoreConfig.Ids, name: scoreConfig.name)
        { }

        public ScoreData(ScoreData other)
        {
            // keep same id if cloning intentionally
            guid = other.guid;
            scoreName = other.scoreName;
            score = other.score;
            metaData = other.metaData?.Clone() ?? new ScoreMetaData();
        }

        public object Clone()
        {
            // Perform an explicit deep-ish clone to avoid MemberwiseClone pitfalls
            return new ScoreData(this);
        }

        public void Merge(ScoreData incoming, ScoreMergingRule mergingRule = ScoreMergingRule.OverrideAll)
        {
            if (incoming == null) return;

            switch (mergingRule)
            {
                case ScoreMergingRule.AllowDuplicates:
                    Score += incoming.Score;
                    // Add keys that don't exist
                    foreach (var kv in incoming.MetaData.Data)
                    {
                        if (!metaData.ContainsKey(kv.Key))
                            metaData.Add(kv.Key, kv.Value);
                    }
                    break;

                case ScoreMergingRule.OverrideAll:
                    Score = incoming.Score;
                    scoreName = incoming.scoreName;
                    metaData = incoming.MetaData.Clone();
                    break;

                case ScoreMergingRule.OverrideMetaData:
                    Score += incoming.Score;
                    // override metadata keys with incoming
                    metaData.AddRange(new Dictionary<string, object>(incoming.MetaData.Data));
                    break;

                case ScoreMergingRule.IgnoreIncoming:
                    // do nothing
                    break;
            }
        }
        
        public static ScoreData operator +(ScoreData a, ScoreData b)
        {
            return new ScoreData(a.Score + b.Score);
        }

        public bool Equals(ScoreData other)
        {
            return other != null && guid.Equals(other.guid);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as ScoreData);
        }

        public override int GetHashCode()
        {
            return guid.GetHashCode();
        }
    }
}