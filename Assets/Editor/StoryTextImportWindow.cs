using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

// 시나리오 텍스트 파일 두 개를 읽어서 StoryChapter 에셋 일곱 개의 대사 배열을 채운다
// 채우는 필드는 hasDialogue, speakerNameEN, dialogueTextEN, speakerNameKR, dialogueTextKR 다섯 개뿐이고
// 초상화, 배경, 판넬, BGM 은 비운 상태로 두어 유니티에서 직접 지정한다
// Editor 라는 이름의 폴더 안에 있어야 하며 빌드에는 포함되지 않는다
public class StoryTextImportWindow : EditorWindow
{
    // 텍스트 파일의 구간 순서와 챕터 에셋의 대응
    private static readonly string[] CHAPTER_NAMES =
    {
        "S1pre", "Tutorial", "S1post", "S2pre", "S2post", "S3pre", "S3post"
    };

    private static readonly string[] FILE_LABELS = { "Korean", "English" };

    // 유니티를 껐다 켜도 경로가 남도록 EditorPrefs 에 보관한다
    private static readonly string[] PREF_KEYS =
    {
        "SandBingo.StoryImport.PathKR",
        "SandBingo.StoryImport.PathEN"
    };

    // 화자와 대사를 나누는 구분자. 앞뒤 공백까지 포함해야 대사 안의 콜론과 섞이지 않는다
    private const string SPEAKER_DELIMITER = " : ";

    // EditorWindow 는 SerializeField 가 붙은 필드만 리컴파일 이후에 값을 유지한다
    [SerializeField] private StoryChapter[] chapters;

    private string[] paths = new string[2];

    // 파싱 결과를 담아두고 Apply 버튼에서 다시 사용한다
    private List<List<ParsedLine>> parsedKR;
    private List<List<ParsedLine>> parsedEN;

    private string statusMessage = "";
    private bool isReady = false;

    private Vector2 scroll;

    // 파일 선택 창처럼 그리는 도중에 실행하면 안 되는 동작을 담아두었다가 프레임 끝에 실행한다
    private System.Action pendingAction;

    // 한 줄의 파싱 결과. 화자가 비어 있으면 나레이션이다
    private struct ParsedLine
    {
        public string speaker;
        public string text;
    }

    [MenuItem("Window/SandBingo/Story Text Import")]
    static void OpenWindow()
    {
        GetWindow<StoryTextImportWindow>("Story Import");
    }

    void OnEnable()
    {
        for (int i = 0; i < paths.Length; i++)
            paths[i] = EditorPrefs.GetString(PREF_KEYS[i], "");

        if (chapters == null || chapters.Length != CHAPTER_NAMES.Length)
            chapters = new StoryChapter[CHAPTER_NAMES.Length];

        AutoFillChapters();
    }

    // ===== 챕터 슬롯 =====

    // 비어 있는 슬롯만 이름으로 찾아서 채운다. 직접 넣은 값은 덮어쓰지 않는다
    void AutoFillChapters()
    {
        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            if (chapters[i] != null) continue;

            string[] guids = AssetDatabase.FindAssets(CHAPTER_NAMES[i] + " t:StoryChapter");

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // 부분 일치로 걸린 다른 에셋을 배제한다
                if (Path.GetFileNameWithoutExtension(path) != CHAPTER_NAMES[i]) continue;

                chapters[i] = AssetDatabase.LoadAssetAtPath<StoryChapter>(path);
                break;
            }
        }
    }

    // ===== 그리기 =====

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Text Files", EditorStyles.boldLabel);

        for (int i = 0; i < paths.Length; i++)
            DrawFileField(i);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Chapters", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();

        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            chapters[i] = (StoryChapter)EditorGUILayout.ObjectField(
                string.Format("{0}. {1}", i + 1, CHAPTER_NAMES[i]),
                chapters[i], typeof(StoryChapter), false);
        }

        // 슬롯이 바뀌면 이전 파싱 결과의 대상이 달라지므로 다시 확인하게 한다
        // statusMessage 를 비우면 아래 HelpBox 가 사라져 이 프레임의 컨트롤 개수가 달라지므로
        // 그리는 도중이 아니라 프레임 끝에서 처리한다
        if (EditorGUI.EndChangeCheck())
        {
            pendingAction = () =>
            {
                isReady = false;
                statusMessage = "";
            };
        }

        if (GUILayout.Button("비어 있는 슬롯을 이름으로 채우기"))
            pendingAction = AutoFillChapters;

        EditorGUILayout.Space(10);

        if (GUILayout.Button("Parse", GUILayout.Height(24)))
            pendingAction = Parse;

        using (new EditorGUI.DisabledScope(!isReady))
        {
            if (GUILayout.Button("Apply", GUILayout.Height(24)))
                pendingAction = Apply;
        }

        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(statusMessage, isReady ? MessageType.Info : MessageType.Error);
        }

        EditorGUILayout.EndScrollView();

        RunPendingAction();
    }

    // 경로 표시와 찾아보기 버튼을 한 줄에 그린다
    void DrawFileField(int index)
    {
        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField(FILE_LABELS[index], GUILayout.Width(60));

        string shown = string.IsNullOrEmpty(paths[index]) ? "(선택되지 않음)" : paths[index];
        EditorGUILayout.SelectableLabel(shown, EditorStyles.textField,
            GUILayout.Height(EditorGUIUtility.singleLineHeight));

        if (GUILayout.Button("...", GUILayout.Width(30)))
        {
            int picked = index;
            pendingAction = () => PickFile(picked);
        }

        EditorGUILayout.EndHorizontal();
    }

    void PickFile(int index)
    {
        string selected = EditorUtility.OpenFilePanel(FILE_LABELS[index] + " scenario text", "", "txt");

        if (string.IsNullOrEmpty(selected)) return;

        paths[index] = selected;
        EditorPrefs.SetString(PREF_KEYS[index], selected);

        isReady = false;
        statusMessage = "";
    }

    void RunPendingAction()
    {
        if (pendingAction == null) return;

        System.Action action = pendingAction;
        pendingAction = null;

        action();
        Repaint();
    }

    // ===== 파싱 =====

    // 파일 한 개를 구간 목록으로 읽는다. 빈 줄은 버리고 구분선에서 구간을 나눈다
    List<List<ParsedLine>> ParseFile(string path)
    {
        List<List<ParsedLine>> sections = new List<List<ParsedLine>>();
        sections.Add(new List<ParsedLine>());

        string[] raw = File.ReadAllLines(path, Encoding.UTF8);

        foreach (string line in raw)
        {
            string trimmed = line.Trim();

            if (trimmed.Length == 0) continue;

            if (IsSectionSeparator(trimmed))
            {
                sections.Add(new List<ParsedLine>());
                continue;
            }

            sections[sections.Count - 1].Add(ParseLine(trimmed));
        }

        // 파일 끝에 구분선이 남아서 생긴 빈 구간은 버린다
        while (sections.Count > 0 && sections[sections.Count - 1].Count == 0)
            sections.RemoveAt(sections.Count - 1);

        return sections;
    }

    // 하이픈만으로 이루어진 줄을 구분선으로 본다. 개수가 달라져도 동작하게 한다
    bool IsSectionSeparator(string line)
    {
        if (line.Length < 3) return false;

        foreach (char c in line)
        {
            if (c != '-') return false;
        }

        return true;
    }

    ParsedLine ParseLine(string line)
    {
        ParsedLine result = new ParsedLine();

        int index = line.IndexOf(SPEAKER_DELIMITER);

        if (index < 0)
        {
            result.speaker = "";
            result.text = line;
            return result;
        }

        result.speaker = line.Substring(0, index).Trim();
        result.text = line.Substring(index + SPEAKER_DELIMITER.Length).Trim();

        return result;
    }

    // 디코딩에 실패한 바이트가 바뀌어 들어오는 대체 문자를 찾는다
    bool ContainsReplacementChar(List<List<ParsedLine>> sections)
    {
        foreach (List<ParsedLine> section in sections)
        {
            foreach (ParsedLine line in section)
            {
                if (line.speaker.IndexOf('\uFFFD') >= 0) return true;
                if (line.text.IndexOf('\uFFFD') >= 0) return true;
            }
        }

        return false;
    }

    // ===== 검사 =====

    void Parse()
    {
        isReady = false;
        parsedKR = null;
        parsedEN = null;

        for (int i = 0; i < paths.Length; i++)
        {
            if (string.IsNullOrEmpty(paths[i]) || !File.Exists(paths[i]))
            {
                statusMessage = FILE_LABELS[i] + " 파일 경로가 비어 있거나 파일이 없습니다.";
                return;
            }
        }

        List<List<ParsedLine>> kr = ParseFile(paths[0]);
        List<List<ParsedLine>> en = ParseFile(paths[1]);

        List<string> errors = new List<string>();

        // UTF8 이 아닌 파일을 읽으면 오류 없이 대체 문자로 바뀐 채 통과해버린다
        // 대사에는 쓸 일이 없는 문자이므로 하나라도 있으면 인코딩 문제로 본다
        for (int i = 0; i < paths.Length; i++)
        {
            if (ContainsReplacementChar(i == 0 ? kr : en))
                errors.Add(FILE_LABELS[i] + " 파일이 UTF8 이 아닌 것 같습니다. UTF8 로 다시 저장하세요.");
        }

        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            if (chapters[i] == null)
                errors.Add(string.Format("{0}번 챕터 슬롯({1})이 비어 있습니다.", i + 1, CHAPTER_NAMES[i]));
        }

        if (kr.Count != CHAPTER_NAMES.Length)
            errors.Add(string.Format("한국어 파일의 구간이 {0}개입니다. {1}개여야 합니다.", kr.Count, CHAPTER_NAMES.Length));

        if (en.Count != CHAPTER_NAMES.Length)
            errors.Add(string.Format("영어 파일의 구간이 {0}개입니다. {1}개여야 합니다.", en.Count, CHAPTER_NAMES.Length));

        // 구간 수가 맞을 때만 줄 단위 대조를 한다
        if (kr.Count == CHAPTER_NAMES.Length && en.Count == CHAPTER_NAMES.Length)
        {
            for (int i = 0; i < CHAPTER_NAMES.Length; i++)
            {
                // 구분선이 연달아 있으면 빈 구간이 생긴다
                // 양쪽 파일이 똑같이 비어 있으면 줄 수 비교만으로는 통과하므로 따로 막는다
                if (kr[i].Count == 0)
                {
                    errors.Add(string.Format("{0}번 구간({1})이 비어 있습니다. 구분선을 확인하세요.",
                        i + 1, CHAPTER_NAMES[i]));
                    continue;
                }

                if (kr[i].Count != en[i].Count)
                {
                    errors.Add(string.Format("{0}번 구간({1})의 줄 수가 다릅니다. 한국어 {2}줄, 영어 {3}줄.",
                        i + 1, CHAPTER_NAMES[i], kr[i].Count, en[i].Count));
                    continue;
                }

                // 두 파일이 어긋났는지 확인한다. 한쪽만 나레이션이면 줄이 밀린 것이다
                for (int j = 0; j < kr[i].Count; j++)
                {
                    bool krNarration = kr[i][j].speaker.Length == 0;
                    bool enNarration = en[i][j].speaker.Length == 0;

                    if (krNarration != enNarration)
                    {
                        errors.Add(string.Format("{0}번 구간({1}) {2}번 줄에서 나레이션 여부가 서로 다릅니다.",
                            i + 1, CHAPTER_NAMES[i], j + 1));
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            statusMessage = string.Join("\n", errors.ToArray());
            return;
        }

        parsedKR = kr;
        parsedEN = en;
        isReady = true;
        statusMessage = BuildSummary();
    }

    string BuildSummary()
    {
        StringBuilder builder = new StringBuilder();
        builder.AppendLine("검사를 통과했습니다. Apply 를 누르면 아래대로 덮어씁니다.");
        builder.AppendLine();

        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            int before = chapters[i].lines != null ? chapters[i].lines.Length : 0;
            int after = parsedKR[i].Count;

            builder.AppendLine(string.Format("{0} : {1}줄에서 {2}줄", CHAPTER_NAMES[i], before, after));
        }

        return builder.ToString();
    }

    // ===== 적용 =====

    void Apply()
    {
        if (!isReady) return;
        if (parsedKR == null || parsedEN == null) return;

        // Parse 이후에 슬롯이 비워졌을 수 있으므로 쓰기 직전에 한 번 더 확인한다
        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            if (chapters[i] != null) continue;

            isReady = false;
            statusMessage = string.Format("{0}번 챕터 슬롯({1})이 비어 있습니다. 다시 Parse 하세요.", i + 1, CHAPTER_NAMES[i]);
            return;
        }

        bool confirmed = EditorUtility.DisplayDialog(
            "Story Import",
            "챕터 일곱 개의 대사 배열을 새로 만듭니다.\n초상화와 배경을 포함한 기존 지정값은 모두 사라집니다.\n계속할까요?",
            "덮어쓰기", "취소");

        if (!confirmed) return;

        for (int i = 0; i < CHAPTER_NAMES.Length; i++)
        {
            StoryChapter chapter = chapters[i];

            // 배열을 통째로 갈아끼우는 구조 변경이므로 상태 전체를 기록하는 쪽을 쓴다
            Undo.RegisterCompleteObjectUndo(chapter, "Import Story Text");

            List<ParsedLine> kr = parsedKR[i];
            List<ParsedLine> en = parsedEN[i];

            StoryLine[] lines = new StoryLine[kr.Count];

            for (int j = 0; j < kr.Count; j++)
            {
                // 새 인스턴스는 StoryLine 의 기본값을 그대로 쓴다
                // hasDialogue 는 true, 나머지 채널 플래그는 false, 스프라이트 참조는 비어 있다
                StoryLine line = new StoryLine();

                line.speakerNameKR = kr[j].speaker;
                line.dialogueTextKR = kr[j].text;
                line.speakerNameEN = en[j].speaker;
                line.dialogueTextEN = en[j].text;

                lines[j] = line;
            }

            chapter.lines = lines;
            EditorUtility.SetDirty(chapter);
        }

        AssetDatabase.SaveAssets();

        isReady = false;
        statusMessage = "적용을 마쳤습니다. 초상화와 배경은 Story Editor 에서 지정하세요.";
    }
}
