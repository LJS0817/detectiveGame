using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace ColdCaseOS.Data
{
    public enum ClueSourceType
    {
        TextExcerpt,
        MetadataExcerpt,
        CctvCapture,
        PhotoCapture,
        AudioClip,
        DbRecord,
        InterrogationStatement
    }

    /// <summary>
    /// 플레이어가 직접 드래그·캡처·추출해 디지털 증거판에 올린 사용자 단서 카드.
    /// 정답 여부는 UI에 노출하지 않고 hiddenEvidenceId로 내부 판별합니다.
    /// </summary>
    [Serializable]
    public class UserClueCard
    {
        [JsonProperty("cardId")] public string cardId;
        [JsonProperty("sourceId")] public string sourceId;
        [JsonProperty("sourceType")] public ClueSourceType sourceType;
        [JsonProperty("displayTitle")] public string displayTitle;
        [JsonProperty("extractedText")] public string extractedText;
        [JsonProperty("capturedImagePath")] public string capturedImagePath;

        [JsonProperty("hiddenEvidenceId")] public string hiddenEvidenceId = "NONE";
        [JsonProperty("isTooSmallCapture")] public bool isTooSmallCapture;

        [JsonProperty("userMemo")] public string userMemo = string.Empty;
        [JsonProperty("boardPosition")] public SerializableVector2 boardPosition;
        [JsonProperty("isDismissed")] public bool isDismissed; // [기각(취소선 후 접기)]
        [JsonProperty("isInTrash")] public bool isInTrash;     // [폐기함(복구 가능)]
        [JsonProperty("connectedCardIds")] public List<string> connectedCardIds = new List<string>();

        public static UserClueCard Create(
            string sourceId,
            ClueSourceType sourceType,
            string displayTitle,
            string hiddenEvidenceId,
            string extractedText = "",
            string capturedImagePath = "",
            bool isTooSmallCapture = false)
        {
            return new UserClueCard
            {
                cardId = Guid.NewGuid().ToString("N"),
                sourceId = sourceId,
                sourceType = sourceType,
                displayTitle = displayTitle,
                extractedText = extractedText ?? string.Empty,
                capturedImagePath = capturedImagePath ?? string.Empty,
                hiddenEvidenceId = string.IsNullOrEmpty(hiddenEvidenceId) ? "NONE" : hiddenEvidenceId,
                isTooSmallCapture = isTooSmallCapture,
                boardPosition = new SerializableVector2(0f, 0f)
            };
        }
    }

    /// <summary>
    /// 증거판 빈 공간에 자유롭게 붙이는 독립 포스트잇 메모 데이터.
    /// </summary>
    [Serializable]
    public class StickyNoteData
    {
        [JsonProperty("noteId")] public string noteId;
        [JsonProperty("text")] public string text;
        [JsonProperty("boardPosition")] public SerializableVector2 boardPosition;
        [JsonProperty("colorHex")] public string colorHex = "#E6C767";
    }

    /// <summary>
    /// Newtonsoft.Json의 UnityEngine.Vector2 자기참조(normalized 등) 직렬화 오류 방지용 구조체.
    /// </summary>
    [Serializable]
    public struct SerializableVector2
    {
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;

        public SerializableVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2 ToVector2() => new Vector2(x, y);
        public static SerializableVector2 FromVector2(Vector2 v) => new SerializableVector2(v.x, v.y);
    }
}
