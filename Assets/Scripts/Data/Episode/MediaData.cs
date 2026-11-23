using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace ColdCaseOS.Data
{
    [Serializable]
    public class DocumentData
    {
        [JsonProperty("docId")] public string docId;
        [JsonProperty("title")] public string title;
        [JsonProperty("category")] public string category; // REPORT, STATEMENT, LOG
        [JsonProperty("isInitial")] public bool isInitial;
        [JsonProperty("paperTemplate")] public string paperTemplate;
        [JsonProperty("bodyText")] public string bodyText;
        [JsonProperty("metadata")] public DocumentMetadata metadata;
        [JsonProperty("hiddenTags")] public List<HiddenTextTag> hiddenTags = new List<HiddenTextTag>();
        [JsonProperty("revealsSuspectIds")] public List<string> revealsSuspectIds = new List<string>();

        public void ResolveTagIndices()
        {
            if (string.IsNullOrEmpty(bodyText) || hiddenTags == null) return;
            foreach (var tag in hiddenTags)
            {
                if (tag == null || string.IsNullOrEmpty(tag.matchSubstring)) continue;
                int idx = bodyText.IndexOf(tag.matchSubstring, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    tag.startCharIndex = idx;
                    tag.endCharIndex = idx + tag.matchSubstring.Length - 1;
                }
                else
                {
                    Debug.LogWarning($"[DocumentData] '{docId}' 본문에서 matchSubstring을 찾지 못했습니다: {tag.matchSubstring}");
                }
            }
        }
    }

    [Serializable]
    public class DocumentMetadata
    {
        [JsonProperty("createdAt")] public string createdAt;
        [JsonProperty("modifiedAt")] public string modifiedAt;
        [JsonProperty("author")] public string author;
        [JsonProperty("sourceFile")] public string sourceFile;
        [JsonProperty("metadataEvidenceId")] public string metadataEvidenceId; // EP3·4·6 메타데이터 뷰어 조작 단서 ID
    }

    [Serializable]
    public class HiddenTextTag
    {
        [JsonProperty("matchSubstring")] public string matchSubstring;
        [JsonProperty("evidenceId")] public string evidenceId;

        [JsonIgnore] public int startCharIndex = -1;
        [JsonIgnore] public int endCharIndex = -1;
        [JsonIgnore] public int Length => (startCharIndex >= 0 && endCharIndex >= startCharIndex)
            ? (endCharIndex - startCharIndex + 1)
            : 0;
    }

    [Serializable]
    public class PhotoData
    {
        [JsonProperty("photoId")] public string photoId;
        [JsonProperty("title")] public string title;
        [JsonProperty("isInitial")] public bool isInitial;
        [JsonProperty("imagePath")] public string imagePath;
        [JsonProperty("hitboxes")] public List<VisualClueHitbox> hitboxes = new List<VisualClueHitbox>();
        [JsonProperty("revealsSuspectIds")] public List<string> revealsSuspectIds = new List<string>();
    }

    [Serializable]
    public class CctvClipData
    {
        [JsonProperty("clipId")] public string clipId;
        [JsonProperty("title")] public string title;
        [JsonProperty("isInitial")] public bool isInitial;
        [JsonProperty("videoPath")] public string videoPath;
        [JsonProperty("displayTimestamp")] public string displayTimestamp;
        [JsonProperty("requiresBrightnessAdjust")] public bool requiresBrightnessAdjust;
        [JsonProperty("hitboxes")] public List<VisualClueHitbox> hitboxes = new List<VisualClueHitbox>();
        [JsonProperty("revealsSuspectIds")] public List<string> revealsSuspectIds = new List<string>();
    }

    [Serializable]
    public class VisualClueHitbox
    {
        [JsonProperty("startSec")] public float startSec;
        [JsonProperty("endSec")] public float endSec;
        [JsonProperty("normalizedRect")] public NormalizedRect normalizedRect;
        [JsonProperty("evidenceId")] public string evidenceId;

        public bool IsActiveAtTime(float currentSec, bool isStaticPhoto)
        {
            if (isStaticPhoto) return true;
            return currentSec >= startSec && currentSec <= endSec;
        }
    }

    /// <summary>
    /// Newtonsoft.Json의 UnityEngine.Rect 직렬화 이슈를 방지하는 0~1 정규화 사각형 구조체.
    /// </summary>
    [Serializable]
    public struct NormalizedRect
    {
        [JsonProperty("x")] public float x;
        [JsonProperty("y")] public float y;
        [JsonProperty("width")] public float width;
        [JsonProperty("height")] public float height;

        public NormalizedRect(float x, float y, float width, float height)
        {
            this.x = x;
            this.y = y;
            this.width = width;
            this.height = height;
        }

        [JsonIgnore] public float XMin => x;
        [JsonIgnore] public float YMin => y;
        [JsonIgnore] public float XMax => x + width;
        [JsonIgnore] public float YMax => y + height;
        [JsonIgnore] public float Area => Mathf.Max(0f, width) * Mathf.Max(0f, height);

        public Rect ToUnityRect() => new Rect(x, y, width, height);

        public static NormalizedRect Intersect(NormalizedRect a, NormalizedRect b)
        {
            float xMin = Mathf.Max(a.XMin, b.XMin);
            float yMin = Mathf.Max(a.YMin, b.YMin);
            float xMax = Mathf.Min(a.XMax, b.XMax);
            float yMax = Mathf.Min(a.YMax, b.YMax);

            if (xMax <= xMin || yMax <= yMin)
                return new NormalizedRect(0f, 0f, 0f, 0f);

            return new NormalizedRect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
