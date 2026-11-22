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
    /// OSWindow를 상속하는 사건 파일 탐색기 앱 창 (확정 A안 커스텀).
    /// 좌측 사이드바에는 메신저에서 다운로드한 사건 목록(사건명 + 작은 케이스번호)을 표시하고,
    /// 우측 종이 리스트 영역에 선택 사건의 해금된 문서·사진·CCTV·녹취 목록을 표시합니다.
    /// </summary>
    public class FileExplorerController : OSWindow
    {
        [Header("Left Sidebar (Downloaded Cases)")]
        [SerializeField] private Transform caseListContainer;
        [SerializeField] private Button caseRowPrefab;

        [Header("Right File List UI")]
        [SerializeField] private Transform fileListContainer;
        [SerializeField] private Button fileRowPrefab;
        [SerializeField] private TMP_Text emptyNoticeText;

        private FileFilterCategory _currentFilter = FileFilterCategory.All;
        private readonly List<GameObject> _caseRows = new List<GameObject>();
        private readonly List<GameObject> _fileRows = new List<GameObject>();

        private void OnEnable()
        {
            GameEvents.OnItemUnlocked += HandleRefresh;
            GameEvents.OnItemMarkedRead += HandleRefresh;
            GameEvents.OnCaseFilesDownloaded += HandleRefresh;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded += HandleEpisodeLoaded;
            RefreshAll();
        }

        private void OnDisable()
        {
            GameEvents.OnItemUnlocked -= HandleRefresh;
            GameEvents.OnItemMarkedRead -= HandleRefresh;
            GameEvents.OnCaseFilesDownloaded -= HandleRefresh;
            if (AppRoot.Instance != null)
                AppRoot.Instance.OnEpisodeLoaded -= HandleEpisodeLoaded;
        }

        private void HandleEpisodeLoaded(EpisodeData _) => RefreshAll();
        private void HandleRefresh(string _) => RefreshAll();

        public void SetFilter(int filterIndex)
        {
            _currentFilter = (FileFilterCategory)filterIndex;
            RefreshFileList();
        }

        public void RefreshAll()
        {
            RefreshCaseSidebar();
            RefreshFileList();
        }

        private void RefreshCaseSidebar()
        {
            foreach (var r in _caseRows)
                if (r != null) Destroy(r);
            _caseRows.Clear();

            var state = AppRoot.Instance?.RuntimeState;
            var episode = state?.Episode;
            if (state == null || episode == null || caseListContainer == null || caseRowPrefab == null) return;

            if (!state.IsCaseFilesDownloaded) return;

            var btn = Instantiate(caseRowPrefab, caseListContainer);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text = $"<b>{episode.title}</b>\n<size=78%><color=#C99A2E>{episode.caseNumber}</color></size>";
            }
            btn.onClick.AddListener(() =>
            {
                GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
                RefreshFileList();
            });
            _caseRows.Add(btn.gameObject);
        }

        public void RefreshFileList()
        {
            foreach (var r in _fileRows)
                if (r != null) Destroy(r);
            _fileRows.Clear();

            var app = AppRoot.Instance;
            var state = app?.RuntimeState;
            if (state?.Episode == null || fileRowPrefab == null || fileListContainer == null) return;

            if (!state.IsCaseFilesDownloaded)
            {
                if (emptyNoticeText != null)
                {
                    emptyNoticeText.gameObject.SetActive(true);
                    emptyNoticeText.text = "다운로드된 사건 파일이 없습니다.\n메신저에서 [사건 파일 다운로드]를 눌러주세요.";
                }
                return;
            }

            if (emptyNoticeText != null) emptyNoticeText.gameObject.SetActive(false);

            if (_currentFilter == FileFilterCategory.All || _currentFilter == FileFilterCategory.Documents)
            {
                foreach (var doc in state.Episode.documents)
                {
                    if (!state.UnlockedDocIds.Contains(doc.docId)) continue;
                    var captured = doc;
                    CreateFileRow($"[문서] {doc.title}", !state.ReadItemIds.Contains(doc.docId), () =>
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
                    CreateFileRow($"[사진] {photo.title}", !state.ReadItemIds.Contains(photo.photoId), () =>
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
                    CreateFileRow($"[CCTV] {clip.title}", !state.ReadItemIds.Contains(clip.clipId), () =>
                    {
                        app.Windows?.GetWindow<CctvVideoController>()?.OpenClip(captured);
                    });
                }

                foreach (var audio in state.Episode.audioClips)
                {
                    if (!state.UnlockedAudioIds.Contains(audio.audioId)) continue;
                    var captured = audio;
                    CreateFileRow($"[녹취] {audio.title}", !state.ReadItemIds.Contains(audio.audioId), () =>
                    {
                        app.Windows?.GetWindow<AudioWaveAnalyzer>()?.OpenAudioAsync(captured).Forget();
                    });
                }
            }
        }

        private void CreateFileRow(string displayLabel, bool isNew, System.Action onClick)
        {
            var btn = Instantiate(fileRowPrefab, fileListContainer);
            var label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = isNew ? $"<color=#822424><b>[NEW]</b></color> {displayLabel}" : displayLabel;

            btn.onClick.AddListener(() =>
            {
                GameEvents.RaiseSfxRequested("SFX_MOUSE_CLICK");
                onClick?.Invoke();
            });
            _fileRows.Add(btn.gameObject);
        }
    }
}
