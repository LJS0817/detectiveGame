#if UNITY_EDITOR
using System.Collections.Generic;
using ColdCaseOS.Apps.EvidenceBoard;
using ColdCaseOS.Apps.FileExplorer;
using ColdCaseOS.Apps.FileViewer;
using ColdCaseOS.Apps.Forensic;
using ColdCaseOS.Apps.Messenger;
using ColdCaseOS.Apps.PoliceDB;
using ColdCaseOS.Apps.Report;
using ColdCaseOS.Apps.TacticalMap;
using ColdCaseOS.Core;
using ColdCaseOS.OS;
using ColdCaseOS.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

namespace ColdCaseOS.EditorTools
{
    /// <summary>
    /// 단일 씬(MainScene)의 전체 UI 계층 구조, 기본 프리팹, 모든 SerializeField 참조를 자동 생성·바인딩하는 에디터 도구.
    /// 메뉴: Tools > ColdCaseOS > Build MainScene Hierarchy
    /// </summary>
    public static class MainSceneSetupTool
    {
        private const string PrefabDir = "Assets/Prefabs/UI";
        private const string SpriteDir = "Assets/Sprites/UI";

        private static Sprite _windowFrameSprite;
        private static Sprite _titleBarTopSprite;
        private static Sprite _rounded4Sprite;

        // 확정 팔레트 (시안 08): 형광등 아이보리 바탕(#ECE6DA) + 어두운 창 여백(#3A3937) + 내부 종이 시트(#F6F3EC)
        private static readonly Color BgDark = new Color(0.925f, 0.902f, 0.855f, 1f);        // #ECE6DA 형광등 아이보리 바탕
        private static readonly Color PanelBg = new Color(0.227f, 0.224f, 0.216f, 0.99f);    // #3A3937 어두운 창 본문/여백
        private static readonly Color PaperSheetBg = new Color(0.965f, 0.953f, 0.925f, 1f);  // #F6F3EC 내부 종이 시트
        private static readonly Color TitleBarBg = new Color(0.173f, 0.173f, 0.173f, 1f);    // #2C2C2C 매트 차콜-블랙 타이틀바
        private static readonly Color ButtonBg = new Color(0.180f, 0.176f, 0.169f, 1f);      // #2E2D2B 다크 차콜 버튼
        private static readonly Color InputBg = new Color(0.910f, 0.890f, 0.847f, 1f);       // #E8E3D8 입력창 베이지
        private static readonly Color AccentColor = new Color(0.788f, 0.604f, 0.180f, 1f);   // #C99A2E 머스타드 오커
        private static readonly Color WarningColor = new Color(0.510f, 0.141f, 0.141f, 1f);  // #822424 다크 옥스블러드
        private static readonly Color InkTextColor = new Color(0.110f, 0.106f, 0.098f, 1f);  // #1C1B19 종이 위 먹색 텍스트
        private static readonly Color PaperTextColor = new Color(0.965f, 0.953f, 0.925f, 1f);// #F6F3EC 어두운 창 위 텍스트

        [MenuItem("Tools/ColdCaseOS/Build MainScene Hierarchy")]
        public static void BuildMainScene()
        {
            if (GameObject.Find("AppRoot") != null || GameObject.Find("MainCanvas") != null)
            {
                if (!EditorUtility.DisplayDialog(
                        "ColdCaseOS MainScene Setup",
                        "현재 씬에 이미 AppRoot 또는 MainCanvas가 있습니다. 기존 오브젝트를 삭제하고 다시 생성할까요?",
                        "다시 생성", "취소"))
                {
                    return;
                }

                DestroyIfExists("AppRoot");
                DestroyIfExists("MainCanvas");
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabDir);
            EnsureFolder("Assets/Sprites");
            EnsureFolder(SpriteDir);

            // 0. 6px 둥근 모서리 + 1px 먹색 외곽선 9-Slice UI 스프라이트 자동 생성
            GenerateRoundedUiSprites();

            // 1. 공용 런타임 UI 프리팹 생성
            var fileRowPrefab = CreateButtonPrefab("FileRowButton", new Vector2(360, 42), "파일 항목");
            var actionButtonPrefab = CreateButtonPrefab("ActionButton", new Vector2(320, 46), "수사 지시");
            var messageItemPrefab = CreateMessageItemPrefab();
            var mapNodePrefab = CreateMapNodePrefab();
            var clueCardPrefab = CreateClueCardPrefab();
            var stickyNotePrefab = CreateStickyNotePrefab();
            var pinWirePrefab = CreatePinWirePrefab();

            // 2. 카메라 및 EventSystem 보장
            EnsureCameraAndEventSystem();

            // 3. AppRoot + AudioController + TutorialController
            var appRootGo = new GameObject("AppRoot");
            var appRoot = appRootGo.AddComponent<AppRoot>();
            var audioCtrl = appRootGo.AddComponent<AudioController>();
            var bgmSrc = appRootGo.AddComponent<AudioSource>();
            var sfxSrc = appRootGo.AddComponent<AudioSource>();
            bgmSrc.playOnAwake = false;
            sfxSrc.playOnAwake = false;
            SetProps(audioCtrl, ("bgmSource", bgmSrc), ("sfxSource", sfxSrc));

            var tutorialCtrl = appRootGo.AddComponent<TutorialController>();

            // 4. MainCanvas (1920x1080)
            var canvasGo = new GameObject("MainCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            // 5. SettingsModal (최상위 모달)
            var settingsModal = BuildSettingsModal(canvasGo.transform, audioCtrl);

            // 6. TitlePanel
            var titleCg = BuildTitlePanel(canvasGo.transform, settingsModal);

            // 7. CaseCabinetPanel
            var cabinetCg = BuildCaseCabinetPanel(canvasGo.transform);

            // 8. InvestigationOSPanel
            var osPanelGo = CreateStretchPanel("InvestigationOSPanel", canvasGo.transform, BgDark);
            var osCg = osPanelGo.AddComponent<CanvasGroup>();

            // DesktopBounds (상단/하단 작업표시줄 64px 제외)
            var desktopGo = new GameObject("DesktopBounds", typeof(RectTransform));
            desktopGo.transform.SetParent(osPanelGo.transform, false);
            var desktopRect = desktopGo.GetComponent<RectTransform>();
            desktopRect.anchorMin = new Vector2(0f, 0f);
            desktopRect.anchorMax = new Vector2(1f, 1f);
            desktopRect.offsetMin = new Vector2(0f, 64f);
            desktopRect.offsetMax = Vector2.zero;

            var windowManager = desktopGo.AddComponent<WindowManager>();
            var registeredWindows = new List<OSWindow>();

            // 8-1. OS 앱 윈도우 10종 생성 및 바인딩 (별도 MetadataViewer 창 제거, DocumentViewer 하단 바 통합)
            registeredWindows.Add(BuildFileExplorerWindow(desktopRect, fileRowPrefab, actionButtonPrefab));
            registeredWindows.Add(BuildDocumentViewerWindow(desktopRect));
            registeredWindows.Add(BuildPhotoInspectorWindow(desktopRect));
            registeredWindows.Add(BuildCctvVideoWindow(desktopRect));
            registeredWindows.Add(BuildAudioWaveWindow(desktopRect));
            registeredWindows.Add(BuildEvidenceBoardWindow(desktopRect, clueCardPrefab, stickyNotePrefab, pinWirePrefab, actionButtonPrefab));
            registeredWindows.Add(BuildTacticalMapWindow(desktopRect, mapNodePrefab, actionButtonPrefab));
            registeredWindows.Add(BuildPoliceDbWindow(desktopRect, actionButtonPrefab));
            registeredWindows.Add(BuildMessengerWindow(desktopRect, messageItemPrefab, actionButtonPrefab));

            var settlementView = BuildResultSettlementView(osPanelGo.transform);
            registeredWindows.Add(BuildReportWindow(desktopRect, settlementView));

            // WindowManager에 등록
            SetWindowManagerProps(windowManager, desktopRect, registeredWindows);

            // 8-2. Taskbar
            BuildTaskbar(osPanelGo.transform);

            // 8-3. Tutorial Guide Banner
            BuildTutorialBanner(osPanelGo.transform, tutorialCtrl);

            // 8-4. NotificationToastManager
            var toastManager = BuildToastOverlay(osPanelGo.transform);

            // 9. AppRoot 최종 바인딩
            SetProps(appRoot,
                ("titlePanel", titleCg),
                ("caseCabinetPanel", cabinetCg),
                ("investigationOsPanel", osCg),
                ("windowManager", windowManager),
                ("toastManager", toastManager));

            // SettingsModal을 최상단으로 이동
            settingsModal.transform.SetAsLastSibling();

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[MainSceneSetupTool] MainScene UI 계층 구조, 프리팹(Assets/Prefabs/UI), 모든 SerializeField 바인딩이 완료되었습니다.");
        }

        // =========================================================================
        // 1. Root Panels (Title, CaseCabinet, Settings, Taskbar, Toast, Tutorial)
        // =========================================================================

        private static CanvasGroup BuildTitlePanel(Transform parent, SettingsModal settingsModal)
        {
            var go = CreateStretchPanel("TitlePanel", parent, BgDark);
            var cg = go.AddComponent<CanvasGroup>();
            var ctrl = go.AddComponent<TitleScreenController>();

            CreateText("GameTitle", go.transform, "COLD CASE OS", 54, TextAlignmentOptions.Center, new Vector2(0, 220), new Vector2(800, 80), InkTextColor);
            CreateText("SubTitle", go.transform, "서울지방경찰청 미제사건 전담 재수사 단말", 22, TextAlignmentOptions.Center, new Vector2(0, 155), new Vector2(800, 40), InkTextColor);

            var contBtn = CreateButton("ContinueButton", go.transform, "이어하기", new Vector2(0, 40), new Vector2(320, 54));
            var newBtn = CreateButton("NewGameButton", go.transform, "새 게임 (EP01)", new Vector2(0, -30), new Vector2(320, 54));
            var cabBtn = CreateButton("CaseCabinetButton", go.transform, "사건 캐비닛", new Vector2(0, -100), new Vector2(320, 54));
            var setBtn = CreateButton("SettingsButton", go.transform, "환경 설정", new Vector2(0, -170), new Vector2(320, 54));
            var quitBtn = CreateButton("QuitButton", go.transform, "종료", new Vector2(0, -240), new Vector2(320, 54));

            SetProps(ctrl,
                ("continueButton", contBtn),
                ("newGameButton", newBtn),
                ("caseCabinetButton", cabBtn),
                ("settingsButton", setBtn),
                ("quitButton", quitBtn),
                ("settingsModal", settingsModal));

            return cg;
        }

        private static CanvasGroup BuildCaseCabinetPanel(Transform parent)
        {
            var go = CreateStretchPanel("CaseCabinetPanel", parent, BgDark);
            var cg = go.AddComponent<CanvasGroup>();
            var ctrl = go.AddComponent<CaseCabinetController>();

            CreateText("CabinetHeader", go.transform, "사건 파일 캐비닛 (EP01 ~ EP07)", 36, TextAlignmentOptions.Left, new Vector2(-500, 440), new Vector2(700, 50), InkTextColor);
            var trustTxt = CreateText("TrustLevelText", go.transform, "전담팀 신뢰도: Lv.2 / 5", 22, TextAlignmentOptions.Right, new Vector2(560, 460), new Vector2(500, 36), InkTextColor);
            var volTxt = CreateText("VolatileSummaryText", go.transform, "휘발성 단서 원본 확보: 0 / 6", 20, TextAlignmentOptions.Right, new Vector2(560, 424), new Vector2(500, 32), InkTextColor);
            var backBtn = CreateButton("BackToTitleButton", go.transform, "타이틀로", new Vector2(-780, -460), new Vector2(200, 48));

            string[] epIds = { "EP01", "EP02", "EP03", "EP04", "EP05", "EP06", "EP07" };
            string[] epTitles =
            {
                "EP01. 심야 편의점 강도상해",
                "EP02. 국도 뺑소니 사망 사건",
                "EP03. 물류창고 방화·보험사기",
                "EP04. 영화 촬영장 소품 살인",
                "EP05. 현금수송차 특수강도살인",
                "EP06. 오피스텔 밀실 변사 사건",
                "EP07. 최종전 — K-POL OS의 설계자"
            };

            var so = new SerializedObject(ctrl);
            so.FindProperty("trustLevelText").objectReferenceValue = trustTxt;
            so.FindProperty("volatileSummaryText").objectReferenceValue = volTxt;
            so.FindProperty("backToTitleButton").objectReferenceValue = backBtn;

            var slotsProp = so.FindProperty("episodeSlots");
            slotsProp.arraySize = epIds.Length;

            for (int i = 0; i < epIds.Length; i++)
            {
                int col = i % 4;
                int row = i / 4;
                Vector2 pos = new Vector2(-540 + col * 360, 160 - row * 280);
                var cardBtn = CreateButton($"Card_{epIds[i]}", go.transform, epTitles[i], pos, new Vector2(320, 220));
                var statusTxt = CreateText("StatusText", cardBtn.transform, "수사 착수 가능", 16, TextAlignmentOptions.Center, new Vector2(0, -45), new Vector2(290, 32), PaperTextColor);
                var volBadge = CreateText("VolatileBadge", cardBtn.transform, "-", 14, TextAlignmentOptions.Center, new Vector2(0, -80), new Vector2(290, 28), AccentColor);

                var elem = slotsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("episodeId").stringValue = epIds[i];
                elem.FindPropertyRelative("displayTitle").stringValue = epTitles[i];
                elem.FindPropertyRelative("selectButton").objectReferenceValue = cardBtn;
                elem.FindPropertyRelative("statusText").objectReferenceValue = statusTxt;
                elem.FindPropertyRelative("volatileBadgeText").objectReferenceValue = volBadge;
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            return cg;
        }

        private static SettingsModal BuildSettingsModal(Transform parent, AudioController audioCtrl)
        {
            var go = CreateCenteredPanel("SettingsModal", parent, new Vector2(560, 420), PanelBg);
            var modal = go.AddComponent<SettingsModal>();

            CreateText("Title", go.transform, "환경 설정", 28, TextAlignmentOptions.Center, new Vector2(0, 165), new Vector2(400, 44));
            CreateText("BgmLabel", go.transform, "BGM 음량", 18, TextAlignmentOptions.Left, new Vector2(-170, 90), new Vector2(140, 30));
            var bgmSlider = CreateSlider("BgmSlider", go.transform, new Vector2(60, 90), new Vector2(260, 24));
            CreateText("SfxLabel", go.transform, "SFX 음량", 18, TextAlignmentOptions.Left, new Vector2(-170, 30), new Vector2(140, 30));
            var sfxSlider = CreateSlider("SfxSlider", go.transform, new Vector2(60, 30), new Vector2(260, 24));
            var fsToggle = CreateToggle("FullscreenToggle", go.transform, "전체 화면", new Vector2(0, -35));
            var resetBtn = CreateButton("ResetTutorialButton", go.transform, "EP01 튜토리얼 초기화", new Vector2(0, -100), new Vector2(260, 42));
            var closeBtn = CreateButton("CloseButton", go.transform, "닫기", new Vector2(0, -160), new Vector2(180, 44));

            SetProps(modal,
                ("audioController", audioCtrl),
                ("bgmSlider", bgmSlider),
                ("sfxSlider", sfxSlider),
                ("fullscreenToggle", fsToggle),
                ("resetTutorialButton", resetBtn),
                ("closeButton", closeBtn));

            return modal;
        }

        private static void BuildTaskbar(Transform parent)
        {
            var barGo = new GameObject("Taskbar", typeof(RectTransform), typeof(Image));
            barGo.transform.SetParent(parent, false);
            var rt = barGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 64f);
            barGo.GetComponent<Image>().color = TitleBarBg;

            var ctrl = barGo.AddComponent<TaskbarController>();

            var fileBtn = CreateButton("Btn_FileExplorer", barGo.transform, "파일 탐색기", new Vector2(-810, 0), new Vector2(130, 44));
            var boardBtn = CreateButton("Btn_EvidenceBoard", barGo.transform, "증거판", new Vector2(-670, 0), new Vector2(120, 44));
            var mapBtn = CreateButton("Btn_TacticalMap", barGo.transform, "전술 지도", new Vector2(-540, 0), new Vector2(120, 44));
            var dbBtn = CreateButton("Btn_PoliceDB", barGo.transform, "경찰 DB", new Vector2(-410, 0), new Vector2(120, 44));
            var msgBtn = CreateButton("Btn_Messenger", barGo.transform, "메신저", new Vector2(-280, 0), new Vector2(120, 44));
            var repBtn = CreateButton("Btn_Report", barGo.transform, "송치 보고서", new Vector2(-145, 0), new Vector2(130, 44));

            var badgeGo = new GameObject("NewBadge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(msgBtn.transform, false);
            var brt = badgeGo.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1f, 1f);
            brt.anchoredPosition = new Vector2(-8f, -8f);
            brt.sizeDelta = new Vector2(16f, 16f);
            badgeGo.GetComponent<Image>().color = WarningColor;
            badgeGo.SetActive(false);

            var partnerTxt = CreateText("PartnerLocationText", barGo.transform, "강민호 경사: 대기", 16, TextAlignmentOptions.Right, new Vector2(210, 0), new Vector2(280, 36), PaperTextColor);
            var volTxt = CreateText("VolatileDeadlineText", barGo.transform, "휘발성 시한 --", 16, TextAlignmentOptions.Right, new Vector2(470, 0), new Vector2(220, 36), AccentColor);
            var subTxt = CreateText("SubmissionDeadlineText", barGo.transform, "송치 마감 --", 16, TextAlignmentOptions.Right, new Vector2(690, 0), new Vector2(210, 36), PaperTextColor);
            var clockTxt = CreateText("ClockText", barGo.transform, "2026.11.18 09:40", 16, TextAlignmentOptions.Right, new Vector2(865, 0), new Vector2(160, 36), PaperTextColor);

            SetProps(ctrl,
                ("clockText", clockTxt),
                ("submissionDeadlineText", subTxt),
                ("volatileDeadlineText", volTxt),
                ("partnerLocationText", partnerTxt),
                ("fileExplorerButton", fileBtn),
                ("evidenceBoardButton", boardBtn),
                ("tacticalMapButton", mapBtn),
                ("policeDbButton", dbBtn),
                ("messengerButton", msgBtn),
                ("reportButton", repBtn),
                ("messengerNewBadge", badgeGo));
        }

        private static void BuildTutorialBanner(Transform parent, TutorialController tutorialCtrl)
        {
            var go = CreateCenteredPanel("TutorialGuideBanner", parent, new Vector2(780, 110), TitleBarBg);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -16f);

            var title = CreateText("StepTitle", go.transform, "수사 가이드", 18, TextAlignmentOptions.Left, new Vector2(-60, 26), new Vector2(620, 28), AccentColor);
            var desc = CreateText("StepDesc", go.transform, "", 15, TextAlignmentOptions.Left, new Vector2(-60, -14), new Vector2(620, 52), PaperTextColor);
            var skipBtn = CreateButton("SkipButton", go.transform, "건너뛰기", new Vector2(320, 0), new Vector2(100, 36));

            SetProps(tutorialCtrl,
                ("guidePanel", go),
                ("stepTitleText", title),
                ("stepDescriptionText", desc),
                ("skipButton", skipBtn));
        }

        private static NotificationToastManager BuildToastOverlay(Transform parent)
        {
            var go = new GameObject("ToastOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-24f, -24f);
            rt.sizeDelta = new Vector2(380f, 90f);
            go.GetComponent<Image>().color = TitleBarBg;

            var cg = go.GetComponent<CanvasGroup>();
            var mgr = go.AddComponent<NotificationToastManager>();
            var title = CreateText("ToastTitle", go.transform, "알림", 17, TextAlignmentOptions.Left, new Vector2(0, 18), new Vector2(340, 26), AccentColor);
            var body = CreateText("ToastBody", go.transform, "", 14, TextAlignmentOptions.Left, new Vector2(0, -16), new Vector2(340, 38), PaperTextColor);

            SetProps(mgr, ("toastRect", rt), ("toastGroup", cg), ("titleText", title), ("bodyText", body));
            return mgr;
        }

        // =========================================================================
        // 2. OS Application Windows (All inherit OSWindow)
        // =========================================================================

        private static OSWindow BuildFileExplorerWindow(Transform parent, Button fileRowPrefab, Button caseRowPrefab)
        {
            // 확정 A안 커스텀: 좌측 28% 다운로드 사건 목록 + 우측 72% 종이 화이트 파일 리스트
            var (win, body) = CreateOSWindow<FileExplorerController>(parent, "FileExplorer", "사건 파일 탐색기", new Vector2(680, 560), new Vector2(-560, 40));

            var sidebar = CreateCenteredPanel("CaseSidebar", body, new Vector2(190, 520), ButtonBg);
            sidebar.GetComponent<RectTransform>().anchoredPosition = new Vector2(-240, 0);
            CreateText("SidebarHeader", sidebar.transform, "배정 사건 목록", 14, TextAlignmentOptions.Left, new Vector2(0, 236), new Vector2(168, 24), AccentColor);
            var caseScroll = CreateScrollView("CaseScroll", sidebar.transform, new Vector2(0, -16), new Vector2(174, 466));
            caseScroll.GetComponent<Image>().color = TitleBarBg;

            var rightSheet = CreateCenteredPanel("FileListSheet", body, new Vector2(468, 520), PaperSheetBg);
            rightSheet.GetComponent<RectTransform>().anchoredPosition = new Vector2(96, 0);

            // 상단 필터 탭
            string[] filterLabels = { "전체", "문서", "사진", "CCTV·녹취" };
            for (int i = 0; i < filterLabels.Length; i++)
            {
                var fBtn = CreateButton($"FilterBtn_{i}", rightSheet.transform, filterLabels[i], new Vector2(-165 + i * 110, 234), new Vector2(100, 30));
                UnityEventTools.AddIntPersistentListener(fBtn.onClick, win.SetFilter, i);
            }

            var fileScroll = CreateScrollView("FileScroll", rightSheet.transform, new Vector2(0, -20), new Vector2(448, 456));
            fileScroll.GetComponent<Image>().color = PaperSheetBg;
            var emptyTxt = CreateText("EmptyNotice", rightSheet.transform, "", 15, TextAlignmentOptions.Center, Vector2.zero, new Vector2(400, 64), InkTextColor);

            SetProps(win,
                ("caseListContainer", caseScroll.content),
                ("caseRowPrefab", caseRowPrefab),
                ("fileListContainer", fileScroll.content),
                ("fileRowPrefab", fileRowPrefab),
                ("emptyNoticeText", emptyTxt));
            return win;
        }

        private static OSWindow BuildDocumentViewerWindow(Transform parent)
        {
            // 확정 C안 커스텀: 공문서 시트 + 하단 접이식 클릭형 메타데이터 바 (별도 MetadataViewer 창 제거)
            var (win, body) = CreateOSWindow<DocumentViewer>(parent, "DocumentViewer", "문서 열람기", new Vector2(640, 700), new Vector2(-100, 20));
            var titleTxt = CreateText("DocTitle", body, "문서 제목", 20, TextAlignmentOptions.Left, new Vector2(0, 304), new Vector2(588, 32), PaperTextColor);

            var scroll = CreateScrollView("DocScroll", body, new Vector2(0, 26), new Vector2(588, 492));
            scroll.GetComponent<Image>().color = PaperSheetBg;

            var bodyTxt = CreateText("DocBodyText", scroll.content, "", 17, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(548, 1200), InkTextColor);
            var dragSelector = bodyTxt.gameObject.AddComponent<TextDragSelector>();

            // 하단 접이식 메타데이터 바
            var toggleMetaBtn = CreateButton("ToggleMetadataBarBtn", body, "파일 메타데이터 속성 ▾", new Vector2(0, -236), new Vector2(588, 30));
            var metaPanel = CreateCenteredPanel("MetadataContentPanel", body, new Vector2(588, 86), TitleBarBg);
            metaPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -296);

            var createdBtn = CreateButton("MetaCreatedBtn", metaPanel.transform, "생성: -", new Vector2(-144, 20), new Vector2(280, 32));
            var modifiedBtn = CreateButton("MetaModifiedBtn", metaPanel.transform, "수정: -", new Vector2(144, 20), new Vector2(280, 32));
            var authorBtn = CreateButton("MetaAuthorBtn", metaPanel.transform, "작성자: -", new Vector2(-144, -20), new Vector2(280, 32));
            var sourceBtn = CreateButton("MetaSourceBtn", metaPanel.transform, "원본: -", new Vector2(144, -20), new Vector2(280, 32));
            metaPanel.SetActive(false);

            // 본문 드래그 & 메타데이터 클릭 공용 플로팅 툴바
            var toolbarGo = CreateCenteredPanel("FloatingClueToolbar", body, new Vector2(240, 48), TitleBarBg);
            var toolbar = toolbarGo.AddComponent<FloatingClueToolbar>();
            var addBtn = CreateButton("AddClueBtn", toolbarGo.transform, "보드에 추가", new Vector2(-50, 0), new Vector2(120, 36));
            var cancelBtn = CreateButton("CancelBtn", toolbarGo.transform, "취소", new Vector2(70, 0), new Vector2(80, 36));
            SetProps(toolbar, ("addToBoardButton", addBtn), ("cancelButton", cancelBtn));
            SetProps(dragSelector, ("floatingToolbar", toolbar));

            SetProps(win,
                ("titleText", titleTxt),
                ("bodyText", bodyTxt),
                ("dragSelector", dragSelector),
                ("floatingToolbar", toolbar),
                ("scrollRect", scroll),
                ("toggleMetadataBarButton", toggleMetaBtn),
                ("metadataContentPanel", metaPanel),
                ("createdAtButton", createdBtn),
                ("modifiedAtButton", modifiedBtn),
                ("authorButton", authorBtn),
                ("sourceFileButton", sourceBtn));
            return win;
        }

        private static OSWindow BuildPhotoInspectorWindow(Transform parent)
        {
            // 확정 F안: 좌측 70% 전체 사진 + 우측 30% 확대 루페 미리보기 칼럼
            var (win, body) = CreateOSWindow<PhotoInspectorController>(parent, "PhotoInspector", "현장 사진 분석기", new Vector2(840, 580), new Vector2(80, 40));
            var titleTxt = CreateText("PhotoTitle", body, "사진", 17, TextAlignmentOptions.Left, new Vector2(-130, 248), new Vector2(540, 28), PaperTextColor);

            var vpGo = CreateCenteredPanel("Viewport", body, new Vector2(560, 480), Color.black);
            vpGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-124, -18);
            vpGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("PhotoContent", typeof(RectTransform), typeof(RawImage));
            contentGo.transform.SetParent(vpGo.transform, false);
            contentGo.GetComponent<RectTransform>().sizeDelta = new Vector2(560, 480);

            var rightCol = CreateCenteredPanel("LoupeColumn", body, new Vector2(240, 520), ButtonBg);
            rightCol.GetComponent<RectTransform>().anchoredPosition = new Vector2(288, -4);
            var zoomTxt = CreateText("ZoomLabel", rightCol.transform, "1.0x", 15, TextAlignmentOptions.Center, new Vector2(0, 232), new Vector2(210, 26), AccentColor);

            var loupeGo = new GameObject("LoupePreview", typeof(RectTransform), typeof(RawImage));
            loupeGo.transform.SetParent(rightCol.transform, false);
            loupeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 96);
            loupeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(212, 212);

            var capBtn = CreateButton("CaptureBtn", rightCol.transform, "캡처 (2.0x+)", new Vector2(0, -210), new Vector2(212, 42));

            SetProps(win,
                ("photoImage", contentGo.GetComponent<RawImage>()),
                ("loupePreviewImage", loupeGo.GetComponent<RawImage>()),
                ("viewportRect", vpGo.GetComponent<RectTransform>()),
                ("contentRect", contentGo.GetComponent<RectTransform>()),
                ("titleText", titleTxt),
                ("zoomLabelText", zoomTxt),
                ("captureButton", capBtn));
            return win;
        }

        private static OSWindow BuildCctvVideoWindow(Transform parent)
        {
            // 확정 F안: 좌측 70% CCTV 전체 프레임 + 타임라인 / 우측 30% 확대 루페 미리보기 + 슬라이더 + 캡처
            var (win, body) = CreateOSWindow<CctvVideoController>(parent, "CctvVideo", "CCTV 포렌식 플레이어", new Vector2(920, 620), new Vector2(120, 20));
            var titleTxt = CreateText("CctvTitle", body, "CCTV", 17, TextAlignmentOptions.Left, new Vector2(-240, 266), new Vector2(360, 28), PaperTextColor);
            var tsTxt = CreateText("Timestamp", body, "00:00:00", 15, TextAlignmentOptions.Right, new Vector2(40, 266), new Vector2(180, 28), AccentColor);

            var vpGo = CreateCenteredPanel("VideoViewport", body, new Vector2(610, 410), Color.black);
            vpGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-138, 36);
            vpGo.AddComponent<RectMask2D>();

            var contentGo = new GameObject("VideoContent", typeof(RectTransform), typeof(RawImage));
            contentGo.transform.SetParent(vpGo.transform, false);
            var contentRt = contentGo.GetComponent<RectTransform>();
            contentRt.sizeDelta = new Vector2(610, 410);

            var rtAsset = new RenderTexture(1280, 720, 0);
            AssetDatabase.CreateAsset(rtAsset, $"{PrefabDir}/CctvRenderTexture.renderTexture");
            contentGo.GetComponent<RawImage>().texture = rtAsset;

            var vp = win.gameObject.AddComponent<VideoPlayer>();
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rtAsset;
            vp.playOnAwake = false;

            var timeline = CreateSlider("TimelineSlider", body, new Vector2(-138, -196), new Vector2(600, 22));
            var backBtn = CreateButton("StepBackBtn", body, "◀ -0.2s", new Vector2(-340, -244), new Vector2(110, 36));
            var playBtn = CreateButton("PlayPauseBtn", body, "재생/일시정지", new Vector2(-138, -244), new Vector2(150, 36));
            var fwdBtn = CreateButton("StepFwdBtn", body, "+0.2s ▶", new Vector2(64, -244), new Vector2(110, 36));

            // 우측 30% 루페 미리보기 & 보정 칼럼
            var rightCol = CreateCenteredPanel("LoupeColumn", body, new Vector2(260, 560), ButtonBg);
            rightCol.GetComponent<RectTransform>().anchoredPosition = new Vector2(316, -4);
            var zoomTxt = CreateText("ZoomLabel", rightCol.transform, "1.0x LOUPE", 15, TextAlignmentOptions.Center, new Vector2(0, 250), new Vector2(230, 26), AccentColor);

            var loupeGo = new GameObject("LoupePreview", typeof(RectTransform), typeof(RawImage));
            loupeGo.transform.SetParent(rightCol.transform, false);
            loupeGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 118);
            loupeGo.GetComponent<RectTransform>().sizeDelta = new Vector2(228, 210);
            loupeGo.GetComponent<RawImage>().texture = rtAsset;

            CreateText("BrightLbl", rightCol.transform, "밝기", 14, TextAlignmentOptions.Left, new Vector2(0, -16), new Vector2(224, 22), PaperTextColor);
            var brightSlider = CreateSlider("BrightnessSlider", rightCol.transform, new Vector2(0, -44), new Vector2(224, 20));
            brightSlider.minValue = -0.5f;
            brightSlider.maxValue = 0.5f;

            CreateText("ContrastLbl", rightCol.transform, "대비", 14, TextAlignmentOptions.Left, new Vector2(0, -86), new Vector2(224, 22), PaperTextColor);
            var contrastSlider = CreateSlider("ContrastSlider", rightCol.transform, new Vector2(0, -114), new Vector2(224, 20));
            contrastSlider.minValue = 0.5f;
            contrastSlider.maxValue = 2.0f;
            contrastSlider.value = 1.0f;

            var capBtn = CreateButton("CaptureBtn", rightCol.transform, "증거판에 캡처 (2.0x+)", new Vector2(0, -228), new Vector2(228, 44));

            SetProps(win,
                ("videoPlayer", vp),
                ("videoDisplayImage", contentGo.GetComponent<RawImage>()),
                ("loupePreviewImage", loupeGo.GetComponent<RawImage>()),
                ("viewportRect", vpGo.GetComponent<RectTransform>()),
                ("contentRect", contentRt),
                ("timelineSlider", timeline),
                ("playPauseButton", playBtn),
                ("stepBackButton", backBtn),
                ("stepForwardButton", fwdBtn),
                ("titleText", titleTxt),
                ("timestampText", tsTxt),
                ("zoomLabelText", zoomTxt),
                ("brightnessSlider", brightSlider),
                ("contrastSlider", contrastSlider),
                ("captureButton", capBtn));
            return win;
        }

        private static OSWindow BuildAudioWaveWindow(Transform parent)
        {
            var (win, body) = CreateOSWindow<AudioWaveAnalyzer>(parent, "AudioWave", "오디오 노이즈 필터 분석기", new Vector2(560, 420), new Vector2(220, -40));
            var src = win.gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            var lpf = win.gameObject.AddComponent<AudioLowPassFilter>();

            var titleTxt = CreateText("AudioTitle", body, "녹취 파일", 18, TextAlignmentOptions.Left, new Vector2(0, 150), new Vector2(510, 30));
            var statusTxt = CreateText("StatusText", body, "노이즈 필터 대역", 15, TextAlignmentOptions.Left, new Vector2(0, 112), new Vector2(510, 26));
            var slider = CreateSlider("CutoffSlider", body, new Vector2(0, 72), new Vector2(500, 24));
            var transcriptTxt = CreateText("TranscriptText", body, "", 16, TextAlignmentOptions.TopLeft, new Vector2(0, -30), new Vector2(510, 140));
            var playBtn = CreateButton("PlayPauseBtn", body, "재생/일시정지", new Vector2(-130, -145), new Vector2(180, 40));
            var extractBtn = CreateButton("ExtractClueBtn", body, "증거판에 녹취록 추가", new Vector2(130, -145), new Vector2(220, 40));

            SetProps(win,
                ("audioSource", src),
                ("lowPassFilter", lpf),
                ("titleText", titleTxt),
                ("statusText", statusTxt),
                ("transcriptText", transcriptTxt),
                ("cutoffSlider", slider),
                ("playPauseButton", playBtn),
                ("extractClueButton", extractBtn));
            return win;
        }

        private static OSWindow BuildEvidenceBoardWindow(
            Transform parent,
            ClueCardView cardPrefab,
            StickyNoteView notePrefab,
            PinWireRenderer wirePrefab,
            Button restoreBtnPrefab)
        {
            var (win, body) = CreateOSWindow<EvidenceBoardController>(parent, "EvidenceBoard", "디지털 증거판", new Vector2(1040, 720), new Vector2(0, 20));
            var addNoteBtn = CreateButton("AddStickyNoteBtn", body, "+ 포스트잇", new Vector2(-410, 310), new Vector2(140, 34));
            var trashBtn = CreateButton("ToggleTrashBtn", body, "폐기함", new Vector2(420, 310), new Vector2(120, 34));

            var wireGo = new GameObject("WireContainer", typeof(RectTransform));
            wireGo.transform.SetParent(body, false);
            Stretch(wireGo.GetComponent<RectTransform>(), new Vector2(12, 12), new Vector2(-12, -56));

            var cardGo = new GameObject("CardContainer", typeof(RectTransform));
            cardGo.transform.SetParent(body, false);
            Stretch(cardGo.GetComponent<RectTransform>(), new Vector2(12, 12), new Vector2(-12, -56));

            var trashGo = CreateCenteredPanel("TrashBinPanel", body, new Vector2(420, 460), PanelBg);
            var trashPanel = trashGo.AddComponent<TrashBinPanel>();
            var emptyTxt = CreateText("EmptyLabel", trashGo.transform, "폐기된 단서가 없습니다.", 16, TextAlignmentOptions.Center, Vector2.zero, new Vector2(360, 36));
            var trashScroll = CreateScrollView("TrashScroll", trashGo.transform, new Vector2(0, -10), new Vector2(380, 380));
            SetProps(trashPanel,
                ("listContainer", trashScroll.content),
                ("restoreItemButtonPrefab", restoreBtnPrefab),
                ("emptyLabel", emptyTxt));
            trashGo.SetActive(false);

            SetProps(win,
                ("cardContainer", cardGo.GetComponent<RectTransform>()),
                ("wireContainer", wireGo.GetComponent<RectTransform>()),
                ("clueCardPrefab", cardPrefab),
                ("stickyNotePrefab", notePrefab),
                ("pinWirePrefab", wirePrefab),
                ("addStickyNoteButton", addNoteBtn),
                ("toggleTrashBinButton", trashBtn),
                ("trashBinPanel", trashPanel));
            return win;
        }

        private static OSWindow BuildTacticalMapWindow(Transform parent, MapNodeView nodePrefab, Button actionBtnPrefab)
        {
            var (win, body) = CreateOSWindow<CityMapController>(parent, "TacticalMap", "전술 현장 지도", new Vector2(980, 680), new Vector2(40, 10));
            // 확정 B안: 좌측 인쇄 종이 구역 지도 시트(#F6F3EC) + 우측 다크 지시 패널
            var nodeContainerGo = CreateCenteredPanel("NodeContainer", body, new Vector2(580, 580), PaperSheetBg);
            nodeContainerGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(-180, -10);

            var partnerTxt = CreateText("PartnerLocation", body, "강민호 경사: 대기", 16, TextAlignmentOptions.Left, new Vector2(290, 265), new Vector2(340, 28));
            var titleTxt = CreateText("NodeTitle", body, "장소를 선택하세요", 20, TextAlignmentOptions.Left, new Vector2(290, 225), new Vector2(340, 32));
            var descTxt = CreateText("NodeDesc", body, "", 14, TextAlignmentOptions.TopLeft, new Vector2(290, 165), new Vector2(340, 72));
            var actScroll = CreateScrollView("ActionScroll", body, new Vector2(290, -70), new Vector2(340, 360));

            // TargetSelectModal
            var modalGo = CreateCenteredPanel("TargetSelectModal", body, new Vector2(440, 380), PanelBg);
            var targetModal = modalGo.AddComponent<TargetSelectModal>();
            var mTitle = CreateText("ModalTitle", modalGo.transform, "수사 대상 선택", 20, TextAlignmentOptions.Center, new Vector2(0, 155), new Vector2(380, 32));
            var mEmpty = CreateText("EmptyNotice", modalGo.transform, "", 15, TextAlignmentOptions.Center, Vector2.zero, new Vector2(380, 60));
            var mScroll = CreateScrollView("OptionScroll", modalGo.transform, new Vector2(0, 10), new Vector2(380, 220));
            var mCancel = CreateButton("CancelBtn", modalGo.transform, "취소", new Vector2(0, -145), new Vector2(140, 38));
            SetProps(targetModal,
                ("titleText", mTitle),
                ("emptyNoticeText", mEmpty),
                ("optionContainer", mScroll.content),
                ("optionButtonPrefab", actionBtnPrefab),
                ("cancelButton", mCancel));
            modalGo.SetActive(false);

            // TimeSkipOverlay
            var skipGo = CreateStretchPanel("TimeSkipOverlay", body, new Color(0.11f, 0.106f, 0.098f, 0.88f));
            var skipOverlay = skipGo.AddComponent<TimeSkipOverlay>();
            var sStatus = CreateText("SkipStatus", skipGo.transform, "현장 수사 진행 중...", 28, TextAlignmentOptions.Center, new Vector2(0, 30), new Vector2(600, 44), PaperTextColor);
            var sDetail = CreateText("SkipDetail", skipGo.transform, "", 18, TextAlignmentOptions.Center, new Vector2(0, -25), new Vector2(600, 36), AccentColor);
            SetProps(skipOverlay, ("statusText", sStatus), ("elapsedDetailText", sDetail));
            skipGo.SetActive(false);

            SetProps(win,
                ("nodeContainer", nodeContainerGo.GetComponent<RectTransform>()),
                ("nodePrefab", nodePrefab),
                ("selectedNodeTitleText", titleTxt),
                ("selectedNodeDescText", descTxt),
                ("partnerLocationText", partnerTxt),
                ("actionListContainer", actScroll.content),
                ("actionButtonPrefab", actionBtnPrefab),
                ("targetSelectModal", targetModal),
                ("timeSkipOverlay", skipOverlay));
            return win;
        }

        private static OSWindow BuildPoliceDbWindow(Transform parent, Button historyRowPrefab)
        {
            // 확정 B안: 좌측 최근 조회 이력 사이드바 + 우측 검색 바 & 종이 화이트 조회 시트
            var (win, body) = CreateOSWindow<PoliceDatabaseController>(parent, "PoliceDB", "K-POL 경찰 통합 DB", new Vector2(780, 540), new Vector2(-60, -20));

            var sidebar = CreateCenteredPanel("HistorySidebar", body, new Vector2(204, 496), ButtonBg);
            sidebar.GetComponent<RectTransform>().anchoredPosition = new Vector2(-280, -2);
            CreateText("HistoryHeader", sidebar.transform, "최근 조회 이력", 14, TextAlignmentOptions.Left, new Vector2(0, 224), new Vector2(180, 24), AccentColor);
            var histScroll = CreateScrollView("HistoryScroll", sidebar.transform, new Vector2(0, -16), new Vector2(188, 444));
            histScroll.GetComponent<Image>().color = TitleBarBg;

            var input = CreateInputField("SearchInput", body, "인물명, 차량번호, 회사명 입력...", new Vector2(44, 224), new Vector2(420, 40));
            var searchBtn = CreateButton("SearchBtn", body, "조회", new Vector2(316, 224), new Vector2(104, 40));

            var resultSheet = CreateCenteredPanel("ResultSheet", body, new Vector2(536, 360), PaperSheetBg);
            resultSheet.GetComponent<RectTransform>().anchoredPosition = new Vector2(102, 8);
            var rTitle = CreateText("ResultTitle", resultSheet.transform, "조회 대기 중", 19, TextAlignmentOptions.Left, new Vector2(0, 146), new Vector2(496, 30), InkTextColor);
            var rBody = CreateText("ResultBody", resultSheet.transform, "", 15, TextAlignmentOptions.TopLeft, new Vector2(0, -16), new Vector2(496, 280), InkTextColor);

            var addBtn = CreateButton("AddToBoardBtn", body, "조회 기록 증거판 추가", new Vector2(102, -214), new Vector2(260, 40));

            SetProps(win,
                ("historyListContainer", histScroll.content),
                ("historyItemPrefab", historyRowPrefab),
                ("searchInputField", input),
                ("searchButton", searchBtn),
                ("resultTitleText", rTitle),
                ("resultBodyText", rBody),
                ("addResultToBoardButton", addBtn));
            return win;
        }

        private static OSWindow BuildMessengerWindow(Transform parent, GameObject messageItemPrefab, Button downloadBtnPrefab)
        {
            // 확정 A안: 좌측 2채널 사이드바(수사심의관 / 강민호 경사) + 우측 대화창 & 파일 다운로드 첨부
            var (win, body) = CreateOSWindow<MessengerController>(parent, "Messenger", "K-POL 내부 메신저", new Vector2(720, 600), new Vector2(500, 20));

            var sidebar = CreateCenteredPanel("ChannelSidebar", body, new Vector2(204, 556), ButtonBg);
            sidebar.GetComponent<RectTransform>().anchoredPosition = new Vector2(-252, -2);
            CreateText("ChannelHeader", sidebar.transform, "대화 채널", 14, TextAlignmentOptions.Left, new Vector2(0, 250), new Vector2(180, 24), AccentColor);

            var hqBtn = CreateButton("HqChannelBtn", sidebar.transform, "서울청 수사심의관", new Vector2(0, 198), new Vector2(184, 46));
            var hqBadge = CreateBadgeDot("HqUnreadBadge", hqBtn.transform, new Vector2(78, 12), WarningColor);
            hqBadge.SetActive(false);

            var partnerBtn = CreateButton("PartnerChannelBtn", sidebar.transform, "강민호 경사", new Vector2(0, 140), new Vector2(184, 46));
            var partnerBadge = CreateBadgeDot("PartnerUnreadBadge", partnerBtn.transform, new Vector2(78, 12), WarningColor);
            partnerBadge.SetActive(false);

            var headerTxt = CreateText("HeaderStatus", body, "서울청 수사심의관 — 공식 사건 배정", 15, TextAlignmentOptions.Left, new Vector2(104, 256), new Vector2(480, 28), PaperTextColor);
            var scroll = CreateScrollView("MessageScroll", body, new Vector2(104, 18), new Vector2(480, 424));
            scroll.GetComponent<Image>().color = PanelBg;
            var hintBtn = CreateButton("AskHintBtn", body, "강민호 경사에게 의견 묻기", new Vector2(104, -236), new Vector2(320, 42));

            SetProps(win,
                ("hqChannelButton", hqBtn),
                ("partnerChannelButton", partnerBtn),
                ("hqUnreadBadge", hqBadge),
                ("partnerUnreadBadge", partnerBadge),
                ("messageListContainer", scroll.content),
                ("messageItemPrefab", messageItemPrefab),
                ("downloadButtonPrefab", downloadBtnPrefab),
                ("scrollRect", scroll),
                ("askPartnerHintButton", hintBtn),
                ("headerStatusText", headerTxt));
            return win;
        }

        private static OSWindow BuildReportWindow(Transform parent, ResultSettlementView settlementView)
        {
            // 확정 C안: 2단 모듈 종이 카드(피의자 지정 / 핵심 증거 1~3) + 하단 고정 결재 푸터 바
            var (win, body) = CreateOSWindow<ReportValidator>(parent, "Report", "검찰 송치 보고서", new Vector2(700, 680), new Vector2(0, 10));
            var headerTxt = CreateText("CaseHeader", body, "사건 송치 보고서", 19, TextAlignmentOptions.Left, new Vector2(0, 290), new Vector2(648, 30), PaperTextColor);

            // 상단 카드: 피의자 지정
            var suspectCard = CreateCenteredPanel("SuspectCard", body, new Vector2(648, 104), PaperSheetBg);
            suspectCard.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 212);
            CreateText("CulpritLabel", suspectCard.transform, "1. 피의자 지목 (발견된 인물)", 16, TextAlignmentOptions.Left, new Vector2(-180, 26), new Vector2(260, 26), InkTextColor);
            var culpritDd = CreateDropdown("CulpritDropdown", suspectCard.transform, new Vector2(0, -14), new Vector2(604, 38));

            // 중앙 카드: 핵심 증거 1~3
            var evidenceCard = CreateCenteredPanel("EvidenceCard", body, new Vector2(648, 286), PaperSheetBg);
            evidenceCard.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 6);
            CreateText("EvidenceCardTitle", evidenceCard.transform, "2. 입증 증거 첨부 (3건)", 16, TextAlignmentOptions.Left, new Vector2(-180, 118), new Vector2(260, 26), InkTextColor);

            var so = new SerializedObject(win);
            so.FindProperty("caseHeaderText").objectReferenceValue = headerTxt;
            so.FindProperty("culpritDropdown").objectReferenceValue = culpritDd;
            so.FindProperty("settlementView").objectReferenceValue = settlementView;

            var slotsProp = so.FindProperty("slotBindings");
            slotsProp.arraySize = 3;
            for (int i = 0; i < 3; i++)
            {
                float y = 66 - i * 80;
                var lbl = CreateText($"SlotLabel_{i}", evidenceCard.transform, $"핵심 증거 슬롯 {i + 1}", 15, TextAlignmentOptions.Left, new Vector2(-180, y), new Vector2(240, 26), InkTextColor);
                var dd = CreateDropdown($"SlotDropdown_{i}", evidenceCard.transform, new Vector2(88, y), new Vector2(420, 36));
                var elem = slotsProp.GetArrayElementAtIndex(i);
                elem.FindPropertyRelative("slotLabelText").objectReferenceValue = lbl;
                elem.FindPropertyRelative("clueDropdown").objectReferenceValue = dd;
            }

            // 반려 피드백 배너
            var fbPanel = CreateCenteredPanel("FeedbackPanel", body, new Vector2(648, 74), WarningColor);
            fbPanel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -184);
            var fbTxt = CreateText("FeedbackText", fbPanel.transform, "", 14, TextAlignmentOptions.Center, Vector2.zero, new Vector2(616, 60), PaperTextColor);
            fbPanel.SetActive(false);

            // 하단 고정 결재 푸터 바
            var footerBar = CreateCenteredPanel("ApprovalFooterBar", body, new Vector2(698, 66), TitleBarBg);
            footerBar.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -290);
            var submitBtn = CreateButton("SubmitReportBtn", footerBar.transform, "검찰 송치 보고서 제출", new Vector2(190, 0), new Vector2(260, 42));
            submitBtn.GetComponent<Image>().color = AccentColor;
            var submitLabel = submitBtn.GetComponentInChildren<TMP_Text>();
            if (submitLabel != null) submitLabel.color = InkTextColor;

            so.FindProperty("submitButton").objectReferenceValue = submitBtn;
            so.FindProperty("feedbackText").objectReferenceValue = fbTxt;
            so.FindProperty("feedbackPanel").objectReferenceValue = fbPanel;
            so.ApplyModifiedPropertiesWithoutUndo();

            return win;
        }

        private static ResultSettlementView BuildResultSettlementView(Transform parent)
        {
            var go = CreateStretchPanel("ResultSettlementView", parent, new Color(0.04f, 0.06f, 0.09f, 0.95f));
            var cg = go.AddComponent<CanvasGroup>();
            var view = go.AddComponent<ResultSettlementView>();

            var card = CreateCenteredPanel("SettlementCard", go.transform, new Vector2(780, 520), PanelBg);
            var caseTxt = CreateText("CaseTitle", card.transform, "", 22, TextAlignmentOptions.Center, new Vector2(0, 195), new Vector2(700, 36));
            var headTxt = CreateText("Headline", card.transform, "", 28, TextAlignmentOptions.Center, new Vector2(0, 125), new Vector2(700, 72));
            var closeTxt = CreateText("PartnerClosing", card.transform, "", 18, TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(680, 96));
            var timeTxt = CreateText("TimeSummary", card.transform, "", 16, TextAlignmentOptions.Center, new Vector2(0, -65), new Vector2(680, 30));
            var volTxt = CreateText("VolatileStatus", card.transform, "", 16, TextAlignmentOptions.Center, new Vector2(0, -100), new Vector2(680, 30));
            var trustTxt = CreateText("TrustStatus", card.transform, "", 16, TextAlignmentOptions.Center, new Vector2(0, -135), new Vector2(680, 30));
            var confirmBtn = CreateButton("ConfirmButton", card.transform, "확인", new Vector2(0, -200), new Vector2(220, 48));

            SetProps(view,
                ("canvasGroup", cg),
                ("caseTitleText", caseTxt),
                ("headlineText", headTxt),
                ("partnerClosingText", closeTxt),
                ("timeSummaryText", timeTxt),
                ("volatileStatusText", volTxt),
                ("trustStatusText", trustTxt),
                ("confirmButton", confirmBtn));

            return view;
        }

        // =========================================================================
        // 3. Prefab Generators
        // =========================================================================

        private static Button CreateButtonPrefab(string name, Vector2 size, string label)
        {
            var temp = CreateButton(name, null, label, Vector2.zero, size);
            var prefab = SaveAsPrefab(temp.gameObject, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(temp.gameObject);
            return prefab.GetComponent<Button>();
        }

        private static GameObject CreateMessageItemPrefab()
        {
            var go = new GameObject("MessageItem", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(430, 84);
            go.GetComponent<Image>().color = PaperSheetBg;
            go.GetComponent<LayoutElement>().minHeight = 72;
            CreateText("MessageText", go.transform, "메시지 내용", 15, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(400, 68), InkTextColor);

            var prefab = SaveAsPrefab(go, $"{PrefabDir}/MessageItem.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static MapNodeView CreateMapNodePrefab()
        {
            var go = new GameObject("MapNodeView", typeof(RectTransform), typeof(Image), typeof(Button));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(150, 52);
            go.GetComponent<Image>().color = ButtonBg;
            var view = go.AddComponent<MapNodeView>();

            var nameTxt = CreateText("NameText", go.transform, "장소", 14, TextAlignmentOptions.Center, Vector2.zero, new Vector2(138, 40), PaperTextColor);
            var partnerDot = CreateBadgeDot("PartnerIndicator", go.transform, new Vector2(-60, 16), AccentColor);
            var selHl = CreateBadgeDot("SelectedHighlight", go.transform, new Vector2(0, -22), Color.cyan);
            var pendDot = CreateBadgeDot("PendingActionDot", go.transform, new Vector2(60, 16), new Color(0.95f, 0.35f, 0.35f, 1f));

            SetProps(view,
                ("selectButton", go.GetComponent<Button>()),
                ("nameText", nameTxt),
                ("partnerHereIndicator", partnerDot),
                ("selectedHighlight", selHl),
                ("pendingActionDot", pendDot));

            var prefab = SaveAsPrefab(go, $"{PrefabDir}/MapNodeView.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<MapNodeView>();
        }

        private static ClueCardView CreateClueCardPrefab()
        {
            var go = new GameObject("ClueCardView", typeof(RectTransform), typeof(Image));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(260, 220);
            go.GetComponent<Image>().color = new Color(0.92f, 0.89f, 0.82f, 1f);
            var view = go.AddComponent<ClueCardView>();

            var pinBtn = CreateButton("PinButton", go.transform, "●", new Vector2(0, 96), new Vector2(28, 28));
            var titleTxt = CreateText("TitleText", go.transform, "단서 제목", 14, TextAlignmentOptions.Left, new Vector2(-15, 68), new Vector2(210, 24));
            titleTxt.color = Color.black;

            var bodyGo = new GameObject("ContentBodyRoot", typeof(RectTransform));
            bodyGo.transform.SetParent(go.transform, false);
            var excerptTxt = CreateText("ExcerptText", bodyGo.transform, "", 12, TextAlignmentOptions.TopLeft, new Vector2(0, 18), new Vector2(236, 62));
            excerptTxt.color = Color.black;

            var previewGo = new GameObject("CapturePreview", typeof(RectTransform), typeof(RawImage));
            previewGo.transform.SetParent(bodyGo.transform, false);
            previewGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 18);
            previewGo.GetComponent<RectTransform>().sizeDelta = new Vector2(220, 62);
            previewGo.SetActive(false);

            var memoInput = CreateInputField("MemoInput", bodyGo.transform, "메모 입력...", new Vector2(0, -42), new Vector2(236, 36));
            var dismissBtn = CreateButton("DismissBtn", go.transform, "기각", new Vector2(-62, -90), new Vector2(96, 28));
            var trashBtn = CreateButton("TrashBtn", go.transform, "폐기", new Vector2(62, -90), new Vector2(96, 28));

            SetProps(view,
                ("pinAnchor", pinBtn.GetComponent<RectTransform>()),
                ("pinConnectButton", pinBtn),
                ("titleText", titleTxt),
                ("contentBodyRoot", bodyGo),
                ("excerptText", excerptTxt),
                ("capturePreviewImage", previewGo.GetComponent<RawImage>()),
                ("memoInputField", memoInput),
                ("dismissToggleButton", dismissBtn),
                ("moveToTrashButton", trashBtn));

            var prefab = SaveAsPrefab(go, $"{PrefabDir}/ClueCardView.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ClueCardView>();
        }

        private static StickyNoteView CreateStickyNotePrefab()
        {
            var go = new GameObject("StickyNoteView", typeof(RectTransform), typeof(Image));
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(200, 170);
            go.GetComponent<Image>().color = new Color(0.92f, 0.80f, 0.42f, 1f);
            var view = go.AddComponent<StickyNoteView>();

            var delBtn = CreateButton("DeleteBtn", go.transform, "X", new Vector2(82, 68), new Vector2(28, 24));
            var input = CreateInputField("NoteInput", go.transform, "메모...", new Vector2(0, -12), new Vector2(180, 120));

            SetProps(view, ("noteInputField", input), ("deleteButton", delBtn));

            var prefab = SaveAsPrefab(go, $"{PrefabDir}/StickyNoteView.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<StickyNoteView>();
        }

        private static PinWireRenderer CreatePinWirePrefab()
        {
            var go = new GameObject("PinWireRenderer", typeof(RectTransform), typeof(Image));
            go.GetComponent<Image>().color = new Color(0.85f, 0.22f, 0.22f, 0.9f);
            go.AddComponent<PinWireRenderer>();

            var prefab = SaveAsPrefab(go, $"{PrefabDir}/PinWireRenderer.prefab");
            Object.DestroyImmediate(go);
            return prefab.GetComponent<PinWireRenderer>();
        }

        // =========================================================================
        // 4. Generic UI Helpers
        // =========================================================================

        private static (T win, Transform body) CreateOSWindow<T>(Transform parent, string windowId, string title, Vector2 size, Vector2 pos)
            where T : OSWindow
        {
            var go = new GameObject($"{windowId}Window", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var bgImg = go.GetComponent<Image>();
            bgImg.color = PanelBg;
            if (_windowFrameSprite != null)
            {
                bgImg.sprite = _windowFrameSprite;
                bgImg.type = Image.Type.Sliced;
            }

            var win = go.AddComponent<T>();

            // Slim 32px matte charcoal title bar (top 6px rounded) + drag handle
            var tbGo = new GameObject("TitleBar", typeof(RectTransform), typeof(Image));
            tbGo.transform.SetParent(go.transform, false);
            var tbRt = tbGo.GetComponent<RectTransform>();
            tbRt.anchorMin = new Vector2(0f, 1f);
            tbRt.anchorMax = new Vector2(1f, 1f);
            tbRt.pivot = new Vector2(0.5f, 1f);
            tbRt.sizeDelta = new Vector2(0f, 32f);
            var tbImg = tbGo.GetComponent<Image>();
            tbImg.color = TitleBarBg;
            if (_titleBarTopSprite != null)
            {
                tbImg.sprite = _titleBarTopSprite;
                tbImg.type = Image.Type.Sliced;
            }

            var dragHandle = tbGo.AddComponent<WindowHeader>();
            SetProps(dragHandle, ("targetWindow", win));

            var titleTxt = CreateText("WindowTitle", tbGo.transform, title, 15, TextAlignmentOptions.Left, new Vector2(-60, 0), new Vector2(size.x - 160, 26), PaperTextColor);
            titleTxt.raycastTarget = false;

            var minBtn = CreateButton("MinimizeBtn", tbGo.transform, "—", new Vector2(size.x * 0.5f - 48f, 0), new Vector2(26, 22));
            var closeBtn = CreateButton("CloseBtn", tbGo.transform, "×", new Vector2(size.x * 0.5f - 18f, 0), new Vector2(26, 22));
            UnityEventTools.AddPersistentListener(minBtn.onClick, win.Minimize);
            UnityEventTools.AddPersistentListener(closeBtn.onClick, win.Close);

            var bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(go.transform, false);
            Stretch(bodyGo.GetComponent<RectTransform>(), new Vector2(1f, 1f), new Vector2(-1f, -32f));

            SetProps(win, ("windowId", windowId), ("titleBarBackground", tbImg));
            go.SetActive(false);
            return (win, bodyGo.transform);
        }

        private static GameObject CreateStretchPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static GameObject CreateCenteredPanel(string name, Transform parent, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<RectTransform>().sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static TMP_Text CreateText(string name, Transform parent, string text, float size, TextAlignmentOptions align, Vector2 pos, Vector2 dims, Color? colorOverride = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = dims;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = colorOverride ?? PaperTextColor;
            return tmp;
        }

        private static Button CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            if (parent != null) go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = ButtonBg;
            if (_rounded4Sprite != null)
            {
                img.sprite = _rounded4Sprite;
                img.type = Image.Type.Sliced;
            }

            var txt = CreateText("Label", go.transform, label, 16, TextAlignmentOptions.Center, Vector2.zero, size - new Vector2(12, 8), PaperTextColor);
            txt.raycastTarget = false;
            return go.GetComponent<Button>();
        }

        private static TMP_InputField CreateInputField(string name, Transform parent, string placeholder, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = InputBg;

            var vp = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(go.transform, false);
            Stretch(vp.GetComponent<RectTransform>(), new Vector2(8, 4), new Vector2(-8, -4));

            var ph = CreateText("Placeholder", vp.transform, placeholder, 14, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero);
            Stretch(ph.rectTransform, Vector2.zero, Vector2.zero);
            ph.color = new Color(0.11f, 0.106f, 0.098f, 0.45f);

            var txt = CreateText("Text", vp.transform, "", 14, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, InkTextColor);
            Stretch(txt.rectTransform, Vector2.zero, Vector2.zero);

            var field = go.GetComponent<TMP_InputField>();
            field.textViewport = vp.GetComponent<RectTransform>();
            field.textComponent = txt;
            field.placeholder = ph;
            return field;
        }

        private static TMP_Dropdown CreateDropdown(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(TMP_Dropdown));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = InputBg;

            var caption = CreateText("Label", go.transform, "선택...", 15, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero);
            Stretch(caption.rectTransform, new Vector2(10, 4), new Vector2(-28, -4));

            var template = CreateCenteredPanel("Template", go.transform, new Vector2(size.x, 180), PanelBg);
            var tRt = template.GetComponent<RectTransform>();
            tRt.anchorMin = new Vector2(0f, 0f);
            tRt.anchorMax = new Vector2(1f, 0f);
            tRt.pivot = new Vector2(0.5f, 1f);
            tRt.anchoredPosition = Vector2.zero;

            var scroll = template.AddComponent<ScrollRect>();
            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(template.transform, false);
            Stretch(vp.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(vp.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.sizeDelta = new Vector2(0f, 34f);

            var itemGo = new GameObject("Item", typeof(RectTransform), typeof(Toggle), typeof(Image));
            itemGo.transform.SetParent(content.transform, false);
            itemGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);
            itemGo.GetComponent<Image>().color = ButtonBg;
            var itemLabel = CreateText("Item Label", itemGo.transform, "옵션", 14, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, PaperTextColor);
            Stretch(itemLabel.rectTransform, new Vector2(10, 2), new Vector2(-10, -2));

            scroll.viewport = vp.GetComponent<RectTransform>();
            scroll.content = cRt;
            template.SetActive(false);

            var dd = go.GetComponent<TMP_Dropdown>();
            dd.template = tRt;
            dd.captionText = caption;
            dd.itemText = itemLabel;
            return dd;
        }

        private static Slider CreateSlider(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            var bg = CreateStretchPanel("Background", go.transform, InputBg);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            Stretch(fillArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            var fill = CreateStretchPanel("Fill", fillArea.transform, AccentColor);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(go.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            var handle = CreateCenteredPanel("Handle", handleArea.transform, new Vector2(18, size.y + 4), Color.white);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            return slider;
        }

        private static Toggle CreateToggle(string name, Transform parent, string label, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(220, 32);

            var bg = CreateCenteredPanel("Background", go.transform, new Vector2(24, 24), InputBg);
            bg.GetComponent<RectTransform>().anchoredPosition = new Vector2(-90, 0);
            var check = CreateCenteredPanel("Checkmark", bg.transform, new Vector2(14, 14), AccentColor);
            CreateText("Label", go.transform, label, 16, TextAlignmentOptions.Left, new Vector2(20, 0), new Vector2(180, 28));

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            return toggle;
        }

        private static ScrollRect CreateScrollView(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = InputBg;

            var vp = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            vp.transform.SetParent(go.transform, false);
            Stretch(vp.GetComponent<RectTransform>(), new Vector2(6, 6), new Vector2(-6, -6));

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(vp.transform, false);
            var cRt = content.GetComponent<RectTransform>();
            cRt.anchorMin = new Vector2(0f, 1f);
            cRt.anchorMax = new Vector2(1f, 1f);
            cRt.pivot = new Vector2(0.5f, 1f);
            cRt.sizeDelta = Vector2.zero;

            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 6f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = content.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = go.GetComponent<ScrollRect>();
            scroll.viewport = vp.GetComponent<RectTransform>();
            scroll.content = cRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            return scroll;
        }

        private static GameObject CreateBadgeDot(string name, Transform parent, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(12, 12);
            go.GetComponent<Image>().color = color;
            return go;
        }

        private static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        private static void EnsureCameraAndEventSystem()
        {
            if (Object.FindFirstObjectByType<Camera>() == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                var cam = camGo.GetComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = BgDark;
                cam.orthographic = true;
            }

            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void GenerateRoundedUiSprites()
        {
            _windowFrameSprite = CreateRoundedSpriteAsset(
                $"{SpriteDir}/UI_WindowFrame_6px.png",
                size: 32,
                radius: 6f,
                roundBottom: true,
                borderPx: 1f,
                borderColor: TitleBarBg,
                fillColor: Color.white,
                sliceBorder: new Vector4(8, 8, 8, 8));

            _titleBarTopSprite = CreateRoundedSpriteAsset(
                $"{SpriteDir}/UI_TitleBar_Top6px.png",
                size: 32,
                radius: 6f,
                roundBottom: false,
                borderPx: 0f,
                borderColor: Color.clear,
                fillColor: Color.white,
                sliceBorder: new Vector4(8, 2, 8, 8));

            _rounded4Sprite = CreateRoundedSpriteAsset(
                $"{SpriteDir}/UI_Rounded_4px.png",
                size: 24,
                radius: 4f,
                roundBottom: true,
                borderPx: 0f,
                borderColor: Color.clear,
                fillColor: Color.white,
                sliceBorder: new Vector4(6, 6, 6, 6));
        }

        private static Sprite CreateRoundedSpriteAsset(
            string assetPath,
            int size,
            float radius,
            bool roundBottom,
            float borderPx,
            Color borderColor,
            Color fillColor,
            Vector4 sliceBorder)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = SignedDistRoundedRect(x + 0.5f, y + 0.5f, size, size, radius, roundBottom);
                    if (dist > 0.5f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                    else
                    {
                        float alpha = Mathf.Clamp01(0.5f - dist);
                        bool isBorder = borderPx > 0f && dist > -borderPx;
                        Color c = isBorder ? borderColor : fillColor;
                        c.a *= alpha;
                        tex.SetPixel(x, y, c);
                    }
                }
            }
            tex.Apply();

            System.IO.File.WriteAllBytes(assetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = sliceBorder;
                importer.filterMode = FilterMode.Bilinear;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static float SignedDistRoundedRect(float x, float y, float w, float h, float r, bool roundBottom)
        {
            float dx = Mathf.Max(0f, Mathf.Abs(x - w * 0.5f) - (w * 0.5f - r));
            float topHalf = h * 0.5f - r;
            float dy = (!roundBottom && y < h * 0.5f)
                ? Mathf.Max(0f, (h * 0.5f - y) - h * 0.5f)
                : Mathf.Max(0f, Mathf.Abs(y - h * 0.5f) - topHalf);

            if (!roundBottom && y < r)
            {
                float edgeX = Mathf.Abs(x - w * 0.5f) - w * 0.5f;
                float edgeY = -y;
                return Mathf.Max(edgeX, edgeY);
            }

            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        private static GameObject SaveAsPrefab(GameObject go, string path)
        {
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }

        private static void DestroyIfExists(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) Object.DestroyImmediate(existing);
        }

        private static void SetProps(Object target, params (string propName, object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (propName, value) in pairs)
            {
                var prop = so.FindProperty(propName);
                if (prop == null)
                {
                    Debug.LogWarning($"[MainSceneSetupTool] Property '{propName}' not found on {target.GetType().Name}");
                    continue;
                }

                if (value is string s) prop.stringValue = s;
                else if (value is Object obj) prop.objectReferenceValue = obj;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetWindowManagerProps(WindowManager wm, RectTransform desktopBounds, List<OSWindow> windows)
        {
            var so = new SerializedObject(wm);
            so.FindProperty("desktopBounds").objectReferenceValue = desktopBounds;
            var listProp = so.FindProperty("registeredWindows");
            listProp.arraySize = windows.Count;
            for (int i = 0; i < windows.Count; i++)
            {
                listProp.GetArrayElementAtIndex(i).objectReferenceValue = windows[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
