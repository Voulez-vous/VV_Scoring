using System.Collections.Generic;

namespace VV.Scoring
{
    public interface IScorable
    {
        Dictionary<string, object> Ids { get; set; }
        int Score { get; set; }
    }
}