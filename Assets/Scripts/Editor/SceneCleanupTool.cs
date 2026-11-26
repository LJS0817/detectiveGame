#if UNITY_EDITOR
using System.Collections.Generic;
using ColdCaseOS.OS;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ColdCaseOS.EditorTools
{
    /// <summary>
    /// 현재 열려 있는 GameScene의 커스텀 UI를 그대로 보존하면서,
    /// 제거된 파트너(Messenger)·전술 지도(TacticalMap)·마감 시한 관련 미사용 오브젝트만 안전하게 삭제하는 에디터 도구.
    /// 메뉴: Tools > ColdCaseOS > Cleanup Unused Map & Messenger Objects
    /// </summary>
    public static class SceneCleanupTool
    {
        private static readonly string[] UnusedObjectNames =
        {
            "TacticalMap",
            "Messenger",
            "Btn_TacticalMap",
            "Btn_Messenger",
            "PartnerLocation",
            "PartnerLocationText",
            "SubmissionDeadlineText",
            "VolatileDeadlineText"
        };

        [MenuItem("Tools/ColdCaseOS/Cleanup Unused Map & Messenger Objects")]
        public static void CleanupUnusedSceneObjects()
        {
            var targets = new List<GameObject>();
            var nameSet = new HashSet<string>(UnusedObjectNames);

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid() || !go.scene.isLoaded) continue;
                if (EditorUtility.IsPersistent(go)) continue;
                if (nameSet.Contains(go.name))
                    targets.Add(go);
            }

            if (targets.Count == 0)
            {
                EditorUtility.DisplayDialog(
                    "ColdCaseOS Scene Cleanup",
                    "현재 씬에서 삭제할 미사용 지도/메신저/마감 시한 오브젝트가 없습니다.",
                    "확인");
                return;
            }

            var removedNames = new List<string>();
            foreach (var go in targets)
            {
                if (go == null) continue;
                removedNames.Add(go.name);
                Undo.DestroyObjectImmediate(go);
            }

            // WindowManager의 null 참조 정리
            foreach (var wm in Object.FindObjectsByType<WindowManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var so = new SerializedObject(wm);
                var prop = so.FindProperty("registeredWindows");
                if (prop != null && prop.isArray)
                {
                    for (int i = prop.arraySize - 1; i >= 0; i--)
                    {
                        if (prop.GetArrayElementAtIndex(i).objectReferenceValue == null)
                            prop.DeleteArrayElementAtIndex(i);
                    }
                    so.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log($"[SceneCleanupTool] 미사용 오브젝트 {removedNames.Count}개 삭제 완료: {string.Join(", ", removedNames)}");
        }
    }
}
#endif
