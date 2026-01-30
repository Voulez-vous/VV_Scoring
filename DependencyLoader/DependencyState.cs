using Unity.Properties;
using UnityEditor;
using UnityEngine;

namespace VV.DependencyLoader
{
    public enum DependencyStatus
    {
        Pending,
        Installed,
        Missing,
        Installing,
        Failed
    }

    /// <summary>
    /// UPM = Unity Package Manager
    /// </summary>
    public enum DependencyType
    {
        Git,
        UPM
    }
    
    public class DependencyState
    {
        public DependencyEntry entry;
        public DependencyStatus status;
        public DependencyType type;
        
        [CreateProperty]
        public string StatusToText => status.ToString();
    }
}