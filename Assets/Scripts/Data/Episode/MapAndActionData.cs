using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ColdCaseOS.Data
{
    public enum ActionCategory
    {
        INVESTIGATION,
        INTERROGATION,
        FORENSIC,
        EXTERNAL_AGENCY
    }

    [Serializable]
    public class MapNodeData
    {
        [JsonProperty("nodeId")] public string nodeId;
        [JsonProperty("name")] public string name;
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("isInitiallyUnlocked")] public bool isInitiallyUnlocked;
        [JsonProperty("description")] public string description;
    }

    [Serializable]
    public class TravelEdgeData
    {
        [JsonProperty("nodeA")] public string nodeA;
        [JsonProperty("nodeB")] public string nodeB;
        [JsonProperty("minutes")] public int minutes;

        public bool Connects(string a, string b)
        {
            return (nodeA == a && nodeB == b) || (nodeA == b && nodeB == a);
        }
    }

    [Serializable]
    public class FieldActionData
    {
        [JsonProperty("actionId")] public string actionId;
        [JsonProperty("nodeId")] public string nodeId;
        [JsonProperty("title")] public string title;
        [JsonProperty("category")] public string category; // ActionCategory 문자열
        [JsonProperty("baseWorkMinutes")] public int baseWorkMinutes;
        [JsonProperty("reportMinutes")] public int reportMinutes;

        [JsonProperty("requiresTargetSelect")] public bool requiresTargetSelect;
        [JsonProperty("targetOptions")] public List<ActionTargetOption> targetOptions = new List<ActionTargetOption>();

        [JsonProperty("rewardDocIds")] public List<string> rewardDocIds = new List<string>();
        [JsonProperty("rewardPhotoIds")] public List<string> rewardPhotoIds = new List<string>();
        [JsonProperty("rewardCctvIds")] public List<string> rewardCctvIds = new List<string>();
        [JsonProperty("volatileBonusDocId")] public string volatileBonusDocId;
        [JsonProperty("partnerReportMessage")] public string partnerReportMessage;

        public ActionCategory GetCategoryEnum()
        {
            return Enum.TryParse(category, true, out ActionCategory parsed)
                ? parsed
                : ActionCategory.INVESTIGATION;
        }
    }

    [Serializable]
    public class ActionTargetOption
    {
        [JsonProperty("targetId")] public string targetId;
        [JsonProperty("label")] public string label;
        [JsonProperty("isInitiallyDiscovered")] public bool isInitiallyDiscovered;
        [JsonProperty("rewardDocIds")] public List<string> rewardDocIds = new List<string>();
        [JsonProperty("volatileBonusDocId")] public string volatileBonusDocId;
        [JsonProperty("partnerReportMessage")] public string partnerReportMessage;
    }

    [Serializable]
    public class PoliceDbEntry
    {
        [JsonProperty("keywords")] public List<string> keywords = new List<string>();
        [JsonProperty("displayName")] public string displayName;
        [JsonProperty("recordText")] public string recordText;
        [JsonProperty("unlocksNodeId")] public string unlocksNodeId;
        [JsonProperty("revealsSuspectId")] public string revealsSuspectId;
    }
}
