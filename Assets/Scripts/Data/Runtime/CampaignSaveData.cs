using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ColdCaseOS.Data
{
    public enum VolatileClueState
    {
        None,
        Damaged,
        Complete
    }

    /// <summary>
    /// 캠페인 전체 진행도 및 자동 저장 데이터 (save_data.json).
    /// </summary>
    [Serializable]
    public class CampaignSaveData
    {
        public const int MinTrustLevel = 0;
        public const int MaxTrustLevel = 5;
        public const int DefaultTrustLevel = 2;

        [JsonProperty("trustLevel")] public int trustLevel = DefaultTrustLevel;
        [JsonProperty("episodeRecords")] public Dictionary<string, EpisodeProgressRecord> episodeRecords = new Dictionary<string, EpisodeProgressRecord>();
        [JsonProperty("activeEpisodeSnapshot")] public EpisodeRuntimeSnapshot activeEpisodeSnapshot;

        public int CountCompleteVolatileClues()
        {
            int count = 0;
            foreach (var kv in episodeRecords)
            {
                if (kv.Key != "EP07" && kv.Value != null && kv.Value.volatileClueState == VolatileClueState.Complete)
                {
                    count++;
                }
            }
            return count;
        }

        public void AdjustTrust(int delta)
        {
            trustLevel = Math.Max(MinTrustLevel, Math.Min(MaxTrustLevel, trustLevel + delta));
        }
    }

    [Serializable]
    public class EpisodeProgressRecord
    {
        [JsonProperty("episodeId")] public string episodeId;
        [JsonProperty("isCleared")] public bool isCleared;
        [JsonProperty("bestElapsedMinutes")] public int bestElapsedMinutes = int.MaxValue;
        [JsonProperty("clearedWithinDeadline")] public bool clearedWithinDeadline;
        [JsonProperty("volatileClueState")] public VolatileClueState volatileClueState = VolatileClueState.None;
        [JsonProperty("achievedEndingType")] public string achievedEndingType; // EP07 전용
    }

    /// <summary>
    /// 진행 중인 에피소드의 중간 자동 저장 스냅샷.
    /// </summary>
    [Serializable]
    public class EpisodeRuntimeSnapshot
    {
        [JsonProperty("episodeId")] public string episodeId;
        [JsonProperty("elapsedMinutes")] public int elapsedMinutes;
        [JsonProperty("isOvertime")] public bool isOvertime;
        [JsonProperty("volatileDeadlineNotified")] public bool volatileDeadlineNotified;
        [JsonProperty("isCaseFilesDownloaded")] public bool isCaseFilesDownloaded;
        [JsonProperty("currentPartnerNodeId")] public string currentPartnerNodeId;

        [JsonProperty("unlockedNodeIds")] public List<string> unlockedNodeIds = new List<string>();
        [JsonProperty("unlockedDocIds")] public List<string> unlockedDocIds = new List<string>();
        [JsonProperty("unlockedPhotoIds")] public List<string> unlockedPhotoIds = new List<string>();
        [JsonProperty("unlockedCctvIds")] public List<string> unlockedCctvIds = new List<string>();
        [JsonProperty("discoveredSuspectIds")] public List<string> discoveredSuspectIds = new List<string>();
        [JsonProperty("queriedDbDisplayNames")] public List<string> queriedDbDisplayNames = new List<string>();
        [JsonProperty("readItemIds")] public List<string> readItemIds = new List<string>();

        [JsonProperty("completedActionKeys")] public List<string> completedActionKeys = new List<string>();
        [JsonProperty("clueCards")] public List<UserClueCard> clueCards = new List<UserClueCard>();
        [JsonProperty("stickyNotes")] public List<StickyNoteData> stickyNotes = new List<StickyNoteData>();
        [JsonProperty("messengerLogs")] public List<MessengerMessageData> messengerLogs = new List<MessengerMessageData>();
        [JsonProperty("interrogationStates")] public List<InterrogationRuntimeState> interrogationStates = new List<InterrogationRuntimeState>();
        [JsonProperty("fieldCommandLogs")] public List<FieldCommandMessageData> fieldCommandLogs = new List<FieldCommandMessageData>();
    }

    [Serializable]
    public class MessengerMessageData
    {
        [JsonProperty("channelId")] public string channelId = "PARTNER"; // "HQ" (서울청 수사심의관) | "PARTNER" (강민호 경사)
        [JsonProperty("sender")] public string sender;
        [JsonProperty("timestamp")] public string timestamp;
        [JsonProperty("body")] public string body;
        [JsonProperty("isSystemAlert")] public bool isSystemAlert;
        [JsonProperty("downloadableEpisodeId")] public string downloadableEpisodeId; // 비어있지 않으면 [사건 파일 다운로드] 버튼 표시
        [JsonProperty("revealsSuspectIds")] public List<string> revealsSuspectIds = new List<string>();
    }
}
