using System;
using System.Collections.Generic;

namespace VV.DependencyLoader
{
    [Serializable]
    public class PackageManifest
    {
        public string name;
        public string version;
        public string displayName;
        public string description;
        /// <summary>
        /// Unity's minimum viable version 
        /// </summary>
        public string unity;
        public string type;
        public List<string> defineConstraints;
        public List<string> keywords;
        public List<DependencyEntry> dependencies;
    }

    [Serializable]
    public class DependencyEntry
    {
        public string packageId;
        public string version;
        public string url;
    }
}