using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace ColdCaseOS.Data
{
    /// <summary>
    /// EP 1~7 공용 마스터 정적 데이터 DTO.
    /// StreamingAssets/Episodes/EPxx/epXX.json 파일과 1:1 매핑됩니다.
    /// </summary>
    [Serializable]
    public class EpisodeData
    {
        [JsonProperty("episodeId")] public string episodeId;
        [JsonProperty("caseNumber")] public string caseNumber;
        [JsonProperty("title")] public string title;
        [JsonProperty("crimeType")] public string crimeType;
        [JsonProperty("startDateTimeIso")] public string startDateTimeIso;

        [JsonProperty("volatileDeadlineMinutes")] public int volatileDeadlineMinutes;
        [JsonProperty("submissionDeadlineMinutes")] public int submissionDeadlineMinutes;
        [JsonProperty("startNodeId")] public string startNodeId;
        [JsonProperty("briefingSummary")] public string briefingSummary;

        [JsonProperty("documents")] public List<DocumentData> documents = new List<DocumentData>();
        [JsonProperty("photos")] public List<PhotoData> photos = new List<PhotoData>();
        [JsonProperty("cctvClips")] public List<CctvClipData> cctvClips = new List<CctvClipData>();

        [JsonProperty("mapNodes")] public List<MapNodeData> mapNodes = new List<MapNodeData>();
        [JsonProperty("travelEdges")] public List<TravelEdgeData> travelEdges = new List<TravelEdgeData>();
        [JsonProperty("actions")] public List<FieldActionData> actions = new List<FieldActionData>();
        [JsonProperty("dbEntries")] public List<PoliceDbEntry> dbEntries = new List<PoliceDbEntry>();

        [JsonProperty("answerRule")] public ReportAnswerRule answerRule;
        [JsonProperty("arrestReports")] public List<ArrestReportTier> arrestReports = new List<ArrestReportTier>();

        /// <summary>
        /// JSON 로드 직후 모든 문서의 HiddenTextTag 문자 인덱스를 bodyText 기준으로 자동 계산합니다.
        /// </summary>
        public void ResolveAllTextTagIndices()
        {
            if (documents == null) return;
            foreach (var doc in documents)
            {
                doc?.ResolveTagIndices();
            }
        }

        public DateTime GetStartDateTime()
        {
            return DateTime.TryParse(startDateTimeIso, out var parsed)
                ? parsed
                : new DateTime(2026, 11, 18, 9, 0, 0);
        }
    }

    [Serializable]
    public class ReportAnswerRule
    {
        [JsonProperty("correctCulpritId")] public string correctCulpritId;
        [JsonProperty("culpritOptions")] public List<CulpritOption> culpritOptions = new List<CulpritOption>();
        [JsonProperty("slots")] public List<ReportSlotRule> slots = new List<ReportSlotRule>();
    }

    [Serializable]
    public class CulpritOption
    {
        [JsonProperty("id")] public string id;
        [JsonProperty("label")] public string label;
        [JsonProperty("wrongFeedback")] public string wrongFeedback;
    }

    [Serializable]
    public class ReportSlotRule
    {
        [JsonProperty("slotId")] public string slotId;
        [JsonProperty("label")] public string label;
        [JsonProperty("acceptedEvidenceIds")] public List<string> acceptedEvidenceIds = new List<string>();
        [JsonProperty("wrongFeedback")] public string wrongFeedback;
    }

    [Serializable]
    public class ArrestReportTier
    {
        [JsonProperty("maxElapsedMinutes")] public int maxElapsedMinutes;
        [JsonProperty("endingType")] public string endingType; // EP07 전용: TRUE_ENDING, NORMAL_ENDING, BITTER_ENDING
        [JsonProperty("minCompleteVolatileClues")] public int minCompleteVolatileClues; // EP07 전용
        [JsonProperty("headline")] public string headline;
        [JsonProperty("partnerClosing")] public string partnerClosing;
    }
}
