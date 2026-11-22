using System.Collections.Generic;
using ColdCaseOS.Data;

namespace ColdCaseOS.Apps.Forensic
{
    public struct CaptureValidationResult
    {
        public bool isCaptureAllowed;       // 전체 화면 통캡처 방지 통과 여부
        public string blockReason;          // 캡처 불가 시 안내 메시지
        public string matchedEvidenceId;    // "NONE" 또는 정답 evidenceId
        public bool isTooSmallCapture;      // 단서는 포함됐으나 너무 작아 식별 불가
    }

    /// <summary>
    /// CCTV 영상 및 정지 현장 사진 공용 정밀 캡처 2중 수학적 검증기.
    /// 1) 전체 화면 통캡처 금지 (줌 2.0x 이상 또는 캡처 영역 가로·세로 40% 이하)
    /// 2) 핵심 단서 Hitbox 85% 이상 포함 + 캡처 프레임 내 최소 15% 면적 유지
    /// </summary>
    public static class CaptureRectValidator
    {
        public const float MinZoomForCapture = 2.0f;
        public const float MaxNormalizedAxisSpan = 0.50f; // 2.0x 줌 시 뷰포트가 원본의 50% 이하
        public const float MinHitboxContainedRatio = 0.85f;
        public const float MinAreaRatioInCapture = 0.15f;

        public static CaptureValidationResult Evaluate(
            float currentZoom,
            NormalizedRect captureNormRect,
            List<VisualClueHitbox> hitboxes,
            float currentVideoSec,
            bool isStaticPhoto)
        {
            if (currentZoom < MinZoomForCapture ||
                captureNormRect.width > MaxNormalizedAxisSpan + 0.01f ||
                captureNormRect.height > MaxNormalizedAxisSpan + 0.01f)
            {
                return new CaptureValidationResult
                {
                    isCaptureAllowed = false,
                    blockReason = "전체 화면은 캡처할 수 없습니다. 단서 영역을 2배 이상 확대해 주십시오.",
                    matchedEvidenceId = "NONE",
                    isTooSmallCapture = false
                };
            }

            if (hitboxes == null || hitboxes.Count == 0 || captureNormRect.Area <= 0f)
            {
                return new CaptureValidationResult
                {
                    isCaptureAllowed = true,
                    matchedEvidenceId = "NONE",
                    isTooSmallCapture = false
                };
            }

            string bestEvidenceId = "NONE";
            bool anyTooSmall = false;

            foreach (var hb in hitboxes)
            {
                if (hb == null || !hb.IsActiveAtTime(currentVideoSec, isStaticPhoto)) continue;
                float hbArea = hb.normalizedRect.Area;
                if (hbArea <= 0f) continue;

                var inter = NormalizedRect.Intersect(captureNormRect, hb.normalizedRect);
                float interArea = inter.Area;
                if (interArea <= 0f) continue;

                float containedRatio = interArea / hbArea;
                float sizeInCapture = interArea / captureNormRect.Area;

                if (containedRatio >= MinHitboxContainedRatio)
                {
                    if (sizeInCapture >= MinAreaRatioInCapture)
                    {
                        bestEvidenceId = hb.evidenceId;
                        anyTooSmall = false;
                        break;
                    }

                    // 단서는 다 들어왔으나 너무 멀리서 작게 찍힌 경우 → 보고서/현장직이 식별 불가 피드백 반환
                    anyTooSmall = true;
                }
            }

            return new CaptureValidationResult
            {
                isCaptureAllowed = true,
                matchedEvidenceId = bestEvidenceId,
                isTooSmallCapture = anyTooSmall
            };
        }
    }
}
