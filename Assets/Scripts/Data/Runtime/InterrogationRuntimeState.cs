using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ColdCaseOS.Data
{
    /// <summary>
    /// 조사실 심문 대화 1개 턴 기록.
    /// </summary>
    [Serializable]
    public class InterrogationChatTurn
    {
        [JsonProperty("role")] public string role; // "player" | "suspect" | "system"
        [JsonProperty("sender")] public string sender;
        [JsonProperty("body")] public string body;
        [JsonProperty("emotion")] public string emotion = "calm";
        [JsonProperty("timestamp")] public string timestamp;
        [JsonProperty("extractableStatement")] public string extractableStatement = string.Empty;
        [JsonProperty("rewardEvidenceId")] public string rewardEvidenceId = string.Empty;
        [JsonProperty("attachedCardTitles")] public List<string> attachedCardTitles = new List<string>();
    }

    /// <summary>
    /// 사건 관련자 1인의 런타임 심문 진행 상태(평정심, 세션 턴, 해금 레이어, 대화 로그).
    /// </summary>
    [Serializable]
    public class InterrogationRuntimeState
    {
        [JsonProperty("suspectId")] public string suspectId;
        [JsonProperty("composure")] public int composure = 75;
        [JsonProperty("sessionsUsed")] public int sessionsUsed;
        [JsonProperty("turnsRemainingInSession")] public int turnsRemainingInSession;
        [JsonProperty("isSessionActive")] public bool isSessionActive;
        [JsonProperty("lastEmotion")] public string lastEmotion = "calm";
        [JsonProperty("unlockedLayerIds")] public List<string> unlockedLayerIds = new List<string>();
        [JsonProperty("chatHistory")] public List<InterrogationChatTurn> chatHistory = new List<InterrogationChatTurn>();

        public bool HasLayer(string layerId)
            => !string.IsNullOrEmpty(layerId) && unlockedLayerIds != null && unlockedLayerIds.Contains(layerId);

        public bool UnlockLayer(string layerId)
        {
            if (string.IsNullOrEmpty(layerId)) return false;
            if (unlockedLayerIds == null) unlockedLayerIds = new List<string>();
            if (unlockedLayerIds.Contains(layerId)) return false;
            unlockedLayerIds.Add(layerId);
            return true;
        }
    }

    /// <summary>
    /// 현장수사반(강민호 경사) 지휘 채널 메시지 로그 항목.
    /// </summary>
    [Serializable]
    public class FieldCommandMessageData
    {
        [JsonProperty("sender")] public string sender;
        [JsonProperty("body")] public string body;
        [JsonProperty("timestamp")] public string timestamp;
        [JsonProperty("isPlayer")] public bool isPlayer;
        [JsonProperty("isSystemAlert")] public bool isSystemAlert;
    }
}
