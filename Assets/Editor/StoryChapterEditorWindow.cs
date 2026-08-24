using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

// StoryChapter 에셋을 인스펙터 폭 제약 없이 편집하기 위한 전용 창
// Editor 라는 이름의 폴더 안에 있어야 하며 빌드에는 포함되지 않는다
// 편집은 SerializedObject 를 통해서만 하므로 저장과 Undo 는 Unity 기본 동작을 그대로 따르고
// 인스펙터와 이 창을 번갈아 써도 같은 에셋 파일에 그대로 반영된다
public class StoryChapterEditorWindow : EditorWindow
{
    // EditorWindow 는 SerializeField 가 붙은 필드만 리컴파일과 창 재열기 이후에 값을 유지한다
    // 이게 없으면 스크립트를 고칠 때마다 편집하던 챕터와 선택이 풀린다
    [SerializeField] private StoryChapter target;
    [SerializeField] private int selectedIndex = -1;

    private SerializedObject serializedTarget;
    private SerializedProperty linesProperty;

    private Vector2 listScroll;
    private Vector2 detailScroll;

    // 목록의 항목 추가나 삭제는 레이아웃 자체를 바꾸므로 그리는 도중에 실행하면 안 된다
    // 여기에 담아두었다가 한 프레임의 그리기가 끝난 뒤에 실행한다
    private System.Action pendingAction;

    private GUIStyle rowStyle;

    private const float LIST_WIDTH = 280f;
    private const float TEXT_AREA_HEIGHT = 70f;

    [MenuItem("Window/SandBingo/Story Editor")]
    static void OpenWindow()
    {
        GetWindow<StoryChapterEditorWindow>("Story Editor");
    }

    // 프로젝트 창에서 StoryChapter 에셋을 더블클릭하면 이 창으로 연다
    [OnOpenAsset]
    static bool OnOpenAsset(int instanceID, int line)
    {
        StoryChapter chapter = EditorUtility.InstanceIDToObject(instanceID) as StoryChapter;
        if (chapter == null) return false;

        StoryChapterEditorWindow window = GetWindow<StoryChapterEditorWindow>("Story Editor");
        window.SetTarget(chapter);
        return true;
    }

    void OnEnable()
    {
        // 스크립트 리컴파일 후에도 편집 대상을 잃지 않게 다시 연결한다
        if (target != null) SetTarget(target);
    }

    void SetTarget(StoryChapter chapter)
    {
        bool sameTarget = (chapter == target);

        target = chapter;
        serializedTarget = target != null ? new SerializedObject(target) : null;
        linesProperty = serializedTarget != null ? serializedTarget.FindProperty("lines") : null;

        if (!sameTarget) selectedIndex = -1;
    }

    // ===== 그리기 =====

    void OnGUI()
    {
        DrawHeader();

        if (target == null)
        {
            EditorGUILayout.HelpBox(
                "편집할 StoryChapter 에셋을 위에 넣거나 프로젝트 창에서 더블클릭하세요.",
                MessageType.Info);
            return;
        }

        // 리컴파일 등으로 SerializedObject 만 사라진 경우 다시 만든다
        if (serializedTarget == null || linesProperty == null)
            SetTarget(target);

        serializedTarget.Update();

        EditorGUILayout.BeginHorizontal();
        DrawLineList();
        DrawSelectedLine();
        EditorGUILayout.EndHorizontal();

        serializedTarget.ApplyModifiedProperties();

        RunPendingAction();
    }

    void DrawHeader()
    {
        EditorGUILayout.Space(4);

        EditorGUI.BeginChangeCheck();
        StoryChapter picked = (StoryChapter)EditorGUILayout.ObjectField(
            "Chapter", target, typeof(StoryChapter), false);

        // 대상이 바뀌면 아래쪽에 그려지는 컨트롤 개수 자체가 달라진다
        // 그리는 도중에 바꾸면 레이아웃 계산과 어긋나므로 프레임 끝으로 미룬다
        if (EditorGUI.EndChangeCheck())
            pendingAction = () => SetTarget(picked);

        EditorGUILayout.Space(4);
    }

    // ===== 좌측 줄 목록 =====

    void DrawLineList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(LIST_WIDTH));

        EditorGUILayout.LabelField($"Lines ({linesProperty.arraySize})", EditorStyles.boldLabel);

        listScroll = EditorGUILayout.BeginScrollView(listScroll, "box");

        for (int i = 0; i < linesProperty.arraySize; i++)
            DrawListRow(i, linesProperty.GetArrayElementAtIndex(i));

        EditorGUILayout.EndScrollView();

        DrawListButtons();

        EditorGUILayout.EndVertical();
    }

    // 한 줄의 요약. 화자와 함께 배경, 판넬이 바뀌는 줄인지 표시해서 연출 흐름을 목록에서 바로 읽게 한다
    void DrawListRow(int index, SerializedProperty line)
    {
        bool hasDialogue = line.FindPropertyRelative("hasDialogue").boolValue;
        string speaker = line.FindPropertyRelative("speakerNameEN").stringValue;

        string who;
        if (!hasDialogue) who = "(no dialogue)";
        else if (string.IsNullOrEmpty(speaker)) who = "(narration)";
        else who = speaker;

        string marks = "";
        if (line.FindPropertyRelative("changeBackground").boolValue) marks += " [BG]";
        if (line.FindPropertyRelative("changePanel").boolValue) marks += " [PANEL]";
        if (line.FindPropertyRelative("changeBgm").boolValue) marks += " [BGM]";

        Color previous = GUI.backgroundColor;
        if (index == selectedIndex)
            GUI.backgroundColor = new Color(0.45f, 0.65f, 1f);

        // 선택이 바뀌면 오른쪽에 그려지는 컨트롤 개수가 달라진다
        // IMGUI 는 한 프레임 안에서 레이아웃을 먼저 계산해두고 그리므로
        // 그리는 도중에 바꾸면 개수가 맞지 않아 오류가 난다. 프레임 끝으로 미룬다
        if (GUILayout.Button($"{index:00}  {who}{marks}", RowStyle))
        {
            int picked = index;
            pendingAction = () => selectedIndex = picked;
        }

        GUI.backgroundColor = previous;
    }

    void DrawListButtons()
    {
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Add"))
        {
            int insertAt = (selectedIndex < 0) ? linesProperty.arraySize : selectedIndex + 1;
            pendingAction = () => InsertLine(insertAt);
        }

        using (new EditorGUI.DisabledScope(selectedIndex < 0))
        {
            if (GUILayout.Button("Remove"))
                pendingAction = DeleteSelected;

            if (GUILayout.Button("Up"))
                pendingAction = () => MoveSelected(-1);

            if (GUILayout.Button("Down"))
                pendingAction = () => MoveSelected(1);
        }

        EditorGUILayout.EndHorizontal();
    }

    // ===== 우측 상세 =====

    void DrawSelectedLine()
    {
        EditorGUILayout.BeginVertical();

        if (selectedIndex < 0 || selectedIndex >= linesProperty.arraySize)
        {
            EditorGUILayout.LabelField("왼쪽 목록에서 줄을 선택하세요.");
            EditorGUILayout.EndVertical();
            return;
        }

        SerializedProperty line = linesProperty.GetArrayElementAtIndex(selectedIndex);

        detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

        EditorGUILayout.LabelField($"Line {selectedIndex}", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);

        SerializedProperty hasDialogue = line.FindPropertyRelative("hasDialogue");
        EditorGUILayout.PropertyField(hasDialogue, new GUIContent("Has Dialogue"));

        using (new EditorGUI.DisabledScope(!hasDialogue.boolValue))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Speaker", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("speakerNameEN"), new GUIContent("Name EN"));
            EditorGUILayout.PropertyField(line.FindPropertyRelative("speakerNameKR"), new GUIContent("Name KR"));

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Dialogue", EditorStyles.boldLabel);
            DrawTextArea(line.FindPropertyRelative("dialogueTextEN"), "Text EN");
            DrawTextArea(line.FindPropertyRelative("dialogueTextKR"), "Text KR");

            EditorGUILayout.Space(6);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("portrait"), new GUIContent("Portrait"));
        }

        EditorGUILayout.Space(10);
        DrawChannel(line, "changeBackground", "background", "Background");

        EditorGUILayout.Space(4);
        DrawChannel(line, "changePanel", "characterPanel", "Character Panel",
                    "값이 비어 있으면 현재 판넬을 제거합니다.");

        EditorGUILayout.Space(4);
        DrawChannel(line, "changeBgm", "bgm", "BGM");

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    // 체크가 꺼진 채널은 그 줄에서 무시되므로 값 칸도 함께 비활성화해서 오해를 줄인다
    void DrawChannel(SerializedProperty line, string flagName, string valueName, string label, string emptyNote = null)
    {
        SerializedProperty flag = line.FindPropertyRelative(flagName);
        SerializedProperty value = line.FindPropertyRelative(valueName);

        EditorGUILayout.BeginVertical("box");

        EditorGUILayout.PropertyField(flag, new GUIContent($"Change {label}"));

        using (new EditorGUI.DisabledScope(!flag.boolValue))
        {
            EditorGUILayout.PropertyField(value, new GUIContent(label));
        }

        if (emptyNote != null && flag.boolValue && value.objectReferenceValue == null)
            EditorGUILayout.HelpBox(emptyNote, MessageType.None);

        EditorGUILayout.EndVertical();
    }

    // TextArea 속성으로 그리면 높이가 고정되어 창을 넓게 쓰는 의미가 줄어들기 때문에 직접 그린다
    void DrawTextArea(SerializedProperty property, string label)
    {
        EditorGUILayout.LabelField(label);
        property.stringValue = EditorGUILayout.TextArea(property.stringValue, GUILayout.MinHeight(TEXT_AREA_HEIGHT));
    }

    // ===== 목록 편집 =====

    void InsertLine(int index)
    {
        index = Mathf.Clamp(index, 0, linesProperty.arraySize);
        linesProperty.InsertArrayElementAtIndex(index);

        // Unity 의 배열 삽입은 앞 항목의 값을 그대로 복사하므로 새 줄로 쓰려면 직접 비운다
        SerializedProperty line = linesProperty.GetArrayElementAtIndex(index);
        line.FindPropertyRelative("hasDialogue").boolValue = true;
        line.FindPropertyRelative("speakerNameEN").stringValue = "";
        line.FindPropertyRelative("speakerNameKR").stringValue = "";
        line.FindPropertyRelative("dialogueTextEN").stringValue = "";
        line.FindPropertyRelative("dialogueTextKR").stringValue = "";
        line.FindPropertyRelative("portrait").objectReferenceValue = null;
        line.FindPropertyRelative("changeBackground").boolValue = false;
        line.FindPropertyRelative("background").objectReferenceValue = null;
        line.FindPropertyRelative("changePanel").boolValue = false;
        line.FindPropertyRelative("characterPanel").objectReferenceValue = null;
        line.FindPropertyRelative("changeBgm").boolValue = false;
        line.FindPropertyRelative("bgm").objectReferenceValue = null;

        serializedTarget.ApplyModifiedProperties();
        selectedIndex = index;
    }

    void DeleteSelected()
    {
        if (selectedIndex < 0 || selectedIndex >= linesProperty.arraySize) return;

        linesProperty.DeleteArrayElementAtIndex(selectedIndex);
        serializedTarget.ApplyModifiedProperties();

        selectedIndex = Mathf.Min(selectedIndex, linesProperty.arraySize - 1);
    }

    void MoveSelected(int delta)
    {
        int destination = selectedIndex + delta;

        if (selectedIndex < 0) return;
        if (destination < 0 || destination >= linesProperty.arraySize) return;

        linesProperty.MoveArrayElement(selectedIndex, destination);
        serializedTarget.ApplyModifiedProperties();

        selectedIndex = destination;
    }

    void RunPendingAction()
    {
        if (pendingAction == null) return;

        System.Action action = pendingAction;
        pendingAction = null;

        action();
        Repaint();
    }

    // ===== 스타일 =====

    // GUIStyle 은 OnGUI 안에서만 안전하게 만들 수 있으므로 처음 그릴 때 한 번 생성한다
    GUIStyle RowStyle
    {
        get
        {
            if (rowStyle == null)
            {
                rowStyle = new GUIStyle(EditorStyles.miniButton);
                rowStyle.alignment = TextAnchor.MiddleLeft;
            }

            return rowStyle;
        }
    }
}
