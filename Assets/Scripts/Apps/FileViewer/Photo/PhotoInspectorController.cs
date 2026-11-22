using System;
using System.IO;
using ColdCaseOS.Apps.Forensic;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileViewer
{
    /// <summary>
    /// OSWindow를 직접 상속하는 정지 현장 사진 확대·캡처 앱 창.
    /// </summary>
    public class PhotoInspectorController : OSWindow, IScrollHandler, IDragHandler
    {
        [Header("Photo Inspector UI (Layout F Loupe)")]
        [SerializeField] private RawImage photoImage;
        [SerializeField] private RawImage loupePreviewImage;
        [SerializeField] private RectTransform viewportRect;
        [SerializeField] private RectTransform contentRect;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text zoomLabelText;
        [SerializeField] private Button captureButton;
        [SerializeField] private float minZoom = 1.0f;
        [SerializeField] private float maxZoom = 4.0f;

        private PhotoData _currentPhoto;
        private Texture2D _loadedTex;
        private float _currentZoom = 1.0f;

        protected override void Awake()
        {
            base.Awake();
            if (captureButton != null)
                captureButton.onClick.AddListener(CaptureCurrentViewport);
        }

        private void OnDestroy()
        {
            if (_loadedTex != null) Destroy(_loadedTex);
        }

        public async UniTaskVoid OpenPhotoAsync(PhotoData photo)
        {
            if (photo == null) return;
            _currentPhoto = photo;

            if (titleText != null) titleText.text = photo.title;
            SetZoom(1.0f);
            if (contentRect != null) contentRect.anchoredPosition = Vector2.zero;

            var tex = await EpisodeLoader.LoadTextureAsync(photo.imagePath);
            if (_loadedTex != null) Destroy(_loadedTex);
            _loadedTex = tex;
            if (photoImage != null) photoImage.texture = _loadedTex;
            if (loupePreviewImage != null) loupePreviewImage.texture = _loadedTex;
            UpdateLoupePreview();

            if (AppRoot.Instance != null)
            {
                AppRoot.Instance.RuntimeState.MarkItemRead(photo.photoId);
                AppRoot.Instance.RuntimeState.DiscoverSuspects(photo.revealsSuspectIds);
            }
            Open();
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

        private void CaptureCurrentViewport()
        {
            if (_currentPhoto == null || AppRoot.Instance == null) return;

            NormalizedRect normRect = CalculateVisibleNormalizedRect();
            var result = CaptureRectValidator.Evaluate(
                _currentZoom,
                normRect,
                _currentPhoto.hitboxes,
                currentVideoSec: 0f,
                isStaticPhoto: true);

            if (!result.isCaptureAllowed)
            {
                GameEvents.RaiseToastRequested("캡처 불가", result.blockReason);
                return;
            }

            string savedPath = SaveCroppedTexture(normRect);
            GameEvents.RaiseSfxRequested("SFX_SHUTTER");

            var card = UserClueCard.Create(
                sourceId: _currentPhoto.photoId,
                sourceType: ClueSourceType.PhotoCapture,
                displayTitle: $"[사진 캡처] {_currentPhoto.title}",
                hiddenEvidenceId: result.matchedEvidenceId,
                capturedImagePath: savedPath,
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

        private string SaveCroppedTexture(NormalizedRect normRect)
        {
            if (_loadedTex == null) return string.Empty;

            int px = Mathf.Clamp(Mathf.RoundToInt(normRect.x * _loadedTex.width), 0, _loadedTex.width - 1);
            int py = Mathf.Clamp(Mathf.RoundToInt(normRect.y * _loadedTex.height), 0, _loadedTex.height - 1);
            int pw = Mathf.Clamp(Mathf.RoundToInt(normRect.width * _loadedTex.width), 1, _loadedTex.width - px);
            int ph = Mathf.Clamp(Mathf.RoundToInt(normRect.height * _loadedTex.height), 1, _loadedTex.height - py);

            Color[] pixels = _loadedTex.GetPixels(px, py, pw, ph);
            var cropped = new Texture2D(pw, ph, TextureFormat.RGBA32, false);
            cropped.SetPixels(pixels);
            cropped.Apply();

            string dir = Path.Combine(Application.persistentDataPath, "Captures");
            Directory.CreateDirectory(dir);
            string filePath = Path.Combine(dir, $"photo_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(filePath, cropped.EncodeToPNG());
            Destroy(cropped);
            return filePath;
        }
    }
}
