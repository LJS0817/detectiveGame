using System.Collections.Generic;
using ColdCaseOS.Apps.FileViewer;
using ColdCaseOS.Apps.Forensic;
using ColdCaseOS.Core;
using ColdCaseOS.Data;
using ColdCaseOS.OS;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ColdCaseOS.Apps.FileExplorer
{
    public enum FileFilterCategory
    {
        All,
        Documents,
        Photos,
        CctvAndAudio
    }

    /// <summary>
    /// OSWindow를 상속하는 사건 파일 탐색기 앱 창.
    /// 해금된 문서·현장 사진·CCTV·오디오 목록과 미열람(NEW) 뱃지를 관리하며 클릭 시 해당 뷰어를 엽니다.
    /// </summary>
    public class FileExplorerController : OSWindow
    {
        [Header("File List UI")]
        [SerializeField] private Transform fileListContainer;
        [SerializeField] private Button fileRowPrefab;

        private FileFilterCategory _currentFilter = FileFilterCategory.All;
        private readonly List<GameObject> _rows = new List<GameObject>();

        private void OnEnable()
        {
            GameEvents.OnItemUnlocked += HandleRefresh;
            GameEvents.OnItemMarkedRead += HandleRefresh;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;
        }

        private void OnDisable()
        {
            GameEvents.OnItemUnlocked -= HandleRefresh;
            GameEvents.OnItemMarkedRead -= HandleRefresh;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void HandleEpisodeLoaded(EpisodeData _) => RefreshList();
        private void HandleRefresh(string _) => RefreshList();

        public void SetFilter(int filterIndex)
        {
            _currentFilter = (FileFilterCategory)filterIndex;
            RefreshList();
        }

        public void RefreshList()
        {
            foreach (var r in _rows)
                if (r != null) Destroy(r);
            _rows.Clear();

            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (state?.Episode == null || fileRowPrefab == null || fileListContainer == null) return;

            if (_currentFilter == FileFilterCategory.All || _currentFilter == FileFilterCategory.Documents)
            {
                foreach (var doc in state.Episode.documents)
                {
                    if (!state.UnlockedDocIds.Contains(doc.docId)) continue;
                    var captured = doc;
                    CreateRow(doc.docId, $"[문서] {doc.title}", !state.ReadItemIds.Contains(doc.docId), () =>
                    {
                        app.Windows?.GetWindow<DocumentViewer>()?.OpenDocument(captured);
                    });
                }
            }

            if (_currentFilter == FileFilterCategory.All || _currentFilter == FileFilterCategory.Photos)
            {
                foreach (var photo in state.Episode.photos)
                {
                    if (!state.UnlockedPhotoIds.Contains(photo.photoId)) continue;
                    var captured = photo;
                    CreateRow(photo.photoId, $"[사진] {photo.title}", !state.ReadItemIds.Contains(photo.photoId), () =>
                    {
                        app.Windows?.GetWindow<PhotoInspectorController>()?.OpenPhotoAsync(captured).Forget();
                    });
                }
            }

            if (_currentFilter == FileFilterCategory.All || _currentFilter == FileFilterCategory.CctvAndAudio)
            {
                foreach (var clip in state.Episode.cctvClips)
                {
                    if (!state.UnlockedCctvIds.Contains(clip.clipId)) continue;
                    var captured = clip;
                    CreateRow(clip.clipId, $"[CCTV] {clip.title}", !state.ReadItemIds.Contains(clip.clipId), () =>
                    {
                        app.Windows?.GetWindow<CctvVideoController>()?.OpenClip(captured);
                    });
                }

                foreach (var audio in state.Episode.audioClips)
                {
                    if (!state.UnlockedAudioIds.Contains(audio.audioId)) continue;
                    var captured = audio;
                    CreateRow(audio.audioId, $"[녹취] {audio.title}", !state.ReadItemIds.Contains(audio.audioId), () =>
                    {
                        app.Windows?.GetWindow<AudioWaveAnalyzer>()?.OpenAudioAsync(captured).Forget();
                    });
                }
            }
        }

        private void CreateRow(string itemId, string displayLabel, bool isNew, System.Action onClick)
        {
            var btn = Instantiate(fileRowPrefab, fileListContainer);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = isNew ? $"<color=#E6B84C>[NEW]</color> {displayLabel}" : displayLabel;

            btn.onClick.AddListener(() =>
            {
                GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
                onClick?.Invoke();
            });
            _rows.Add(btn.gameObject);
        }
    }
}
