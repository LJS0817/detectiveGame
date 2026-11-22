using System;
using System.IO;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ColdCaseOS.Apps.Forensic
{
    /// <summary>
    /// OSWindow를 직접 상속하는 Unity VideoPlayer 기반 CCTV 영상 분석기 앱 창.
    /// </summary>
    public class CctvVideoController : OSWindow, IScrollHandler, IDragHandler
    {
        private static readonly int BrightnessProp = Shader.PropertyToID("_Brightness");
        private static readonly int ContrastProp = Shader.PropertyToID("_Contrast");

        [Header("Video Display")]
        [SerializeField] private VideoPlayer videoPlayer;
        [SerializeField] private RawImage videoDisplayImage;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform contentRect;

        [Header("Playback Controls")]
        [SerializeField] private Slider timelineSlider;
        [SerializeField] private Button playPauseButton;
        [SerializeField] private Button stepBackButton;
        [SerializeField] private Button stepForwardButton;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text timestampText;
        [SerializeField] private TMP_Text zoomLabelText;

        [Header("Forensic Tools & Loupe Preview (Layout F)")]
        [SerializeField] private RawImage loupePreviewImage;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private Slider contrastSlider;
        [SerializeField] private Button captureButton;
        [SerializeField] private float minZoom = 1.0f;
        [SerializeField] private float maxZoom = 4.0f;
        [SerializeField] private float stepSeconds = 0.2f;

        private CctvClipData _currentClip;
        private float _currentZoom = 1.0f;
        private bool _isScrubbing;
        private Material _runtimeMat;

        protected override void Awake()
        {
            base.Awake();
            if (playPauseButton != null) playPauseButton.onClick.AddListener(TogglePlayPause);
            if (stepBackButton != null) stepBackButton.onClick.AddListener(() => StepTime(-stepSeconds));
            if (stepForwardButton != null) stepForwardButton.onClick.AddListener(() => StepTime(+stepSeconds));
            if (captureButton != null) captureButton.onClick.AddListener(CaptureCurrentViewport);

            if (timelineSlider != null)
                timelineSlider.onValueChanged.AddListener(OnSliderValueChanged);
            if (brightnessSlider != null)
                brightnessSlider.onValueChanged.AddListener(_ => ApplyForensicShader());
            if (contrastSlider != null)
                contrastSlider.onValueChanged.AddListener(_ => ApplyForensicShader());

            if (videoDisplayImage != null && videoDisplayImage.material != null)
            {
                _runtimeMat = new Material(videoDisplayImage.material);
                videoDisplayImage.material = _runtimeMat;
            }
        }

        private void OnDestroy()
        {
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }

        protected override void OnWindowMinimized() => PauseVideo();
        protected override void OnWindowClosed() => PauseVideo();

        private void Update()
        {
            if (_currentClip == null || videoPlayer == null || !gameObject.activeInHierarchy) return;

            bool isFocused = _manager != null && _manager.FocusedWindow == this;
            if (isFocused)
            {
                if (Input.GetKeyDown(KeyCode.Space)) TogglePlayPause();
                else if (Input.GetKeyDown(KeyCode.LeftArrow)) StepTime(-stepSeconds);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) StepTime(+stepSeconds);
            }

            if (videoPlayer.isPlaying && videoPlayer.length > 0 && timelineSlider != null && !_isScrubbing)
            {
                timelineSlider.SetValueWithoutNotify((float)(videoPlayer.time / videoPlayer.length));
            }
        }

        public void OpenClip(CctvClipData clip)
        {
            if (clip == null) return;
            _currentClip = clip;

            if (titleText != null) titleText.text = clip.title;
            if (timestampText != null) timestampText.text = clip.displayTimestamp;

            ResetZoomAndShader();

            if (videoPlayer != null)
            {
                videoPlayer.source = VideoSource.Url;
                videoPlayer.url = EpisodeLoader.GetMediaFullPath(clip.videoPath);
                videoPlayer.Prepare();
                videoPlayer.Play();
                if (loupePreviewImage != null && videoPlayer.targetTexture != null)
                    loupePreviewImage.texture = videoPlayer.targetTexture;
            }
            UpdateLoupePreview();

            if (AppRoot.Instance != null)
            {
                AppRoot.Instance.RuntimeState.MarkItemRead(clip.clipId);
                AppRoot.Instance.RuntimeState.DiscoverSuspects(clip.revealsSuspectIds);
            }
            Open();
        }

        public void TogglePlayPause()
        {
            if (videoPlayer == null) return;
            if (videoPlayer.isPlaying) videoPlayer.Pause();
            else videoPlayer.Play();
            GameEvents.RaiseSfxRequested("SFX_CCTV_TICK");
        }

        public void PauseVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Pause();
        }

        public void StepTime(float deltaSec)
        {
            if (videoPlayer == null || videoPlayer.length <= 0) return;
            videoPlayer.Pause();
            double next = Math.Clamp(videoPlayer.time + deltaSec, 0.0, videoPlayer.length);
            videoPlayer.time = next;
            if (timelineSlider != null)
                timelineSlider.SetValueWithoutNotify((float)(next / videoPlayer.length));
            GameEvents.RaiseSfxRequested("SFX_CCTV_TICK");
        }

        private void OnSliderValueChanged(float normalized)
        {
            if (videoPlayer == null || videoPlayer.length <= 0) return;
            _isScrubbing = true;
            videoPlayer.time = normalized * videoPlayer.length;
            _isScrubbing = false;
        }

        public void OnScroll(PointerEventData eventData)
        {
            SetZoom(Mathf.Clamp(_currentZoom + eventData.scrollDelta.y * 0.25f, minZoom, maxZoom));
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (contentRect == null || _currentZoom <= 1.01f) return;
            contentRect.anchoredPosition += eventData.delta;
            ClampContentPan();
        }

        private void SetZoom(float zoom)
        {
            _currentZoom = zoom;
            if (contentRect != null)
            {
                contentRect.localScale = Vector3.one * _currentZoom;
                ClampContentPan();
            }
            if (zoomLabelText != null) zoomLabelText.text = $"{_currentZoom:0.0}x";
            if (captureButton != null) captureButton.interactable = _currentZoom >= CaptureRectValidator.MinZoomForCapture;
            UpdateLoupePreview();
        }

        private void UpdateLoupePreview()
        {
            if (loupePreviewImage == null) return;
            loupePreviewImage.uvRect = CalculateVisibleNormalizedRect().ToUnityRect();
        }

        private void ClampContentPan()
        {
            if (viewportRect == null || contentRect == null) return;
            Vector2 maxOffset = viewportRect.rect.size * (_currentZoom - 1f) * 0.5f;
            Vector2 p = contentRect.anchoredPosition;
            contentRect.anchoredPosition = new Vector2(
                Mathf.Clamp(p.x, -maxOffset.x, maxOffset.x),
                Mathf.Clamp(p.y, -maxOffset.y, maxOffset.y));
            UpdateLoupePreview();
        }

        private void ApplyForensicShader()
        {
            if (_runtimeMat == null) return;
            if (brightnessSlider != null) _runtimeMat.SetFloat(BrightnessProp, brightnessSlider.value);
            if (contrastSlider != null) _runtimeMat.SetFloat(ContrastProp, contrastSlider.value);
        }

        private void ResetZoomAndShader()
        {
            SetZoom(1.0f);
            if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;
            if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(0f);
            if (contrastSlider != null) contrastSlider.SetValueWithoutNotify(1f);
            ApplyForensicShader();
        }

        private void CaptureCurrentViewport()
        {
            if (_currentClip == null || AppRoot.Instance == null) return;

            NormalizedRect normRect = CalculateVisibleNormalizedRect();
            float currentSec = videoPlayer != null ? (float)videoPlayer.time : 0f;

            var result = CaptureRectValidator.Evaluate(
                _currentZoom,
                normRect,
                _currentClip.hitboxes,
                currentSec,
                isStaticPhoto: false);

            if (!result.isCaptureAllowed)
            {
                GameEvents.RaiseToastRequested("캡처 불가", result.blockReason);
                return;
            }

            string savedPngPath = SaveCroppedRenderTexture(normRect);
            GameEvents.RaiseSfxRequested("SFX_SHUTTER");

            var card = UserClueCard.Create(
                sourceId: _currentClip.clipId,
                sourceType: ClueSourceType.CctvCapture,
                displayTitle: $"[CCTV 캡처] {_currentClip.displayTimestamp} (+{currentSec:0.0}s)",
                hiddenEvidenceId: result.matchedEvidenceId,
                capturedImagePath: savedPngPath,
                isTooSmallCapture: result.isTooSmallCapture);

            AppRoot.Instance.RuntimeState.AddClueCard(card);
        }

        private NormalizedRect CalculateVisibleNormalizedRect()
        {
            if (viewportRect == null || contentRect == null)
                return new NormalizedRect(0f, 0f, 1f, 1f);

            float w = 1f / _currentZoom;
            float h = 1f / _currentZoom;
            Vector2 vpSize = viewportRect.rect.size;
            float centerX = 0.5f - (contentRect.anchoredPosition.x / (vpSize.x * _currentZoom));
            float centerY = 0.5f - (contentRect.anchoredPosition.y / (vpSize.y * _currentZoom));

            return new NormalizedRect(
                Mathf.Clamp01(centerX - w * 0.5f),
                Mathf.Clamp01(centerY - h * 0.5f),
                w,
                h);
        }

        private string SaveCroppedRenderTexture(NormalizedRect normRect)
        {
            if (videoPlayer == null || videoPlayer.targetTexture == null) return string.Empty;

            RenderTexture rt = videoPlayer.targetTexture;
            int px = Mathf.Clamp(Mathf.RoundToInt(normRect.x * rt.width), 0, rt.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(normRect.y * rt.height), 0, rt.height - 1);
            int pw = Mathf.Clamp(Mathf.RoundToInt(normRect.width * rt.width), 1, rt.width - px);
            int ph = Mathf.Clamp(Mathf.RoundToInt(normRect.height * rt.height), 1, rt.height - py);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(pw, ph, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(px, py, pw, ph), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string dir = Path.Combine(Application.persistentDataPath, "Captures");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"cap_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(filePath, tex.EncodeToPNG());
            Destroy(tex);
            return filePath;
        }
    }
}
