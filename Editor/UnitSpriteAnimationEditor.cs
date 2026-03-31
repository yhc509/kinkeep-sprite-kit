using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.U2D.Animation;

public class UnitSpriteAnimationEditor : EditorWindow
{
    private static readonly string[] ClipDirectionOptions =
    {
        "None",
        "Up",
        "Down",
        "Right"
    };

    private static readonly string[] ClipDirectionLabels =
    {
        "None",
        "Up",
        "Down",
        "Right"
    };

    private UnitSpriteAnimation _animationData;
    private SerializedObject _serializedObject;
    private Vector2 _leftScrollPosition;
    private Vector2 _rightScrollPosition;
    
    private int _selectedClipIndex;
    private int _currentFrame;
    private float _frameTime;
    private bool _isPlaying;
    private double _lastTime;
    
    private bool _showAutoGenerate = true;
    private SpriteLibraryAsset _autoGenerateSourceLibrary;
    private string _autoGenerateOutputFolder = UnitSpriteAnimationAutoGenerator.DefaultOutputFolder;
    private string _autoGenerateOutputName;
    private Dictionary<string, string> _autoGenerateSourceActions;
    private GenerationReport _lastGenerationReport;

    [MenuItem("Tools/KinKeep/Sprite Animation Editor")]
    public static void ShowWindow()
    {
        var window = GetWindow<UnitSpriteAnimationEditor>("Unit Animation Editor");
        window.minSize = new Vector2(900, 600);
    }

    public static void OpenWithAsset(UnitSpriteAnimation data)
    {
        var window = GetWindow<UnitSpriteAnimationEditor>("Unit Animation Editor");
        window.minSize = new Vector2(900, 600);
        window._animationData = data;
        window._serializedObject = new SerializedObject(data);
        window._selectedClipIndex = 0;
        window._currentFrame = 0;
        window.Repaint();
    }

    [UnityEditor.Callbacks.OnOpenAsset]
    public static bool OnOpenAsset(int instanceID, int line)
    {
        var asset = EditorUtility.InstanceIDToObject(instanceID) as UnitSpriteAnimation;
        if (asset != null)
        {
            OpenWithAsset(asset);
            return true;
        }
        return false;
    }

    private void OnEnable()
    {
        Selection.selectionChanged += OnSelectionChanged;
        EditorApplication.update += OnEditorUpdate;
        EnsureAutoGenerateConfig();
    }

    private void OnDisable()
    {
        Selection.selectionChanged -= OnSelectionChanged;
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnSelectionChanged()
    {
        if (Selection.activeObject is UnitSpriteAnimation data)
        {
            _animationData = data;
            _serializedObject = new SerializedObject(data);
            _selectedClipIndex = 0;
            _currentFrame = 0;
            _isPlaying = false;
            if (string.IsNullOrWhiteSpace(_autoGenerateOutputName))
                _autoGenerateOutputName = data.name;
            Repaint();
        }
    }

    private void OnEditorUpdate()
    {
        if (!_isPlaying || _animationData == null) return;

        var clips = _animationData.Clips;
        if (clips == null || clips.Length == 0) return;
        if (_selectedClipIndex >= clips.Length) return;

        var clip = clips[_selectedClipIndex];
        if (clip.Sprites == null || clip.Sprites.Length == 0) return;

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - _lastTime);
        _lastTime = currentTime;

        _frameTime += deltaTime;

        float frameDuration = clip.GetFrameDuration(_currentFrame);
        while (_frameTime >= frameDuration)
        {
            _frameTime -= frameDuration;
            _currentFrame++;

            if (_currentFrame >= clip.Sprites.Length)
            {
                if (clip.Loop)
                    _currentFrame = 0;
                else
                {
                    _currentFrame = clip.Sprites.Length - 1;
                    _isPlaying = false;
                    break;
                }
            }

            frameDuration = clip.GetFrameDuration(_currentFrame);
        }

        Repaint();
    }

    private void OnGUI()
    {
        DrawToolbar();
        
        EditorGUILayout.BeginHorizontal();
        
        DrawLeftPanel();
        DrawRightPanel();
        
        EditorGUILayout.EndHorizontal();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUI.BeginChangeCheck();
        _animationData = (UnitSpriteAnimation)EditorGUILayout.ObjectField(
            _animationData, typeof(UnitSpriteAnimation), false, GUILayout.Width(250));
        if (EditorGUI.EndChangeCheck() && _animationData != null)
        {
            _serializedObject = new SerializedObject(_animationData);
            _selectedClipIndex = 0;
            _currentFrame = 0;
        }

        if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            CreateNewAsset();

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(450));
        _leftScrollPosition = EditorGUILayout.BeginScrollView(_leftScrollPosition);

        EnsureAutoGenerateConfig();

        if (_animationData == null)
        {
            EditorGUILayout.HelpBox("UnitSpriteAnimation 에셋을 선택하거나 새로 생성하세요.", MessageType.Info);
        }
        else
        {
            if (!EnsureSerializedObject())
            {
                EditorGUILayout.HelpBox("SerializedObject를 초기화할 수 없습니다. 에셋을 다시 선택하세요.", MessageType.Warning);
            }
            else
            {
                _serializedObject.Update();
                DrawClipList();
                
                EditorGUILayout.Space(10);
                
                DrawSelectedClipEditor();
                _serializedObject.ApplyModifiedProperties();
            }
        }

        EditorGUILayout.Space(12);
        DrawAutoGenerateSection();

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private bool EnsureSerializedObject()
    {
        if (_animationData == null)
        {
            _serializedObject = null;
            return false;
        }

        if (_serializedObject == null || _serializedObject.targetObject != _animationData)
        {
            _serializedObject = new SerializedObject(_animationData);
        }

        return _serializedObject != null;
    }

    private void EnsureAutoGenerateConfig()
    {
        if (_autoGenerateSourceLibrary == null)
        {
            _autoGenerateSourceLibrary = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(
                UnitSpriteAnimationAutoGenerator.DefaultSourceLibraryPath);
        }

        if (string.IsNullOrWhiteSpace(_autoGenerateOutputFolder))
            _autoGenerateOutputFolder = UnitSpriteAnimationAutoGenerator.DefaultOutputFolder;

        if (_autoGenerateSourceActions == null || _autoGenerateSourceActions.Count == 0)
        {
            _autoGenerateSourceActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AnimationMappingProfile defaultProfile = UnitSpriteAnimationAutoGenerator.CreateDefaultProfile();
            foreach (string typeName in UnitSpriteAnimationAutoGenerator.AnimationTypeNames)
            {
                _autoGenerateSourceActions[typeName] = defaultProfile.GetSourceAction(typeName);
            }
        }

        if (string.IsNullOrWhiteSpace(_autoGenerateOutputName))
        {
            if (_animationData != null)
                _autoGenerateOutputName = _animationData.name;
            else if (_autoGenerateSourceLibrary != null)
                _autoGenerateOutputName = _autoGenerateSourceLibrary.name;
        }
    }

    private void DrawAutoGenerateSection()
    {
        _showAutoGenerate = EditorGUILayout.Foldout(_showAutoGenerate, "Auto Generate", true);
        if (!_showAutoGenerate)
            return;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        _autoGenerateSourceLibrary = (SpriteLibraryAsset)EditorGUILayout.ObjectField(
            "Source",
            _autoGenerateSourceLibrary,
            typeof(SpriteLibraryAsset),
            false);

        EditorGUILayout.BeginHorizontal();
        _autoGenerateOutputFolder = EditorGUILayout.TextField("Output Folder", _autoGenerateOutputFolder);
        if (GUILayout.Button("...", GUILayout.Width(30)))
            SelectOutputFolder();
        EditorGUILayout.EndHorizontal();

        _autoGenerateOutputName = EditorGUILayout.TextField("Output Name", _autoGenerateOutputName);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Type -> Action Mapping", EditorStyles.boldLabel);
        foreach (string typeName in UnitSpriteAnimationAutoGenerator.AnimationTypeNames)
        {
            if (!_autoGenerateSourceActions.TryGetValue(typeName, out string sourceAction))
                sourceAction = string.Empty;

            _autoGenerateSourceActions[typeName] = EditorGUILayout.TextField(typeName, sourceAction);
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "방향 매핑: B=Up, F=Down, S=Right\n프레임 슬라이스: Jab 0~2, Cast 0~3\nAttack/Skill: 중앙 프레임 Hit 이벤트 자동 추가\n동일 이름 에셋이 있으면 기존 에셋을 업데이트합니다.",
            MessageType.None);

        if (GUILayout.Button("Generate UnitSpriteAnimation", GUILayout.Height(28)))
            RunAutoGenerate();

        if (_lastGenerationReport != null)
        {
            MessageType reportType = _lastGenerationReport.HasErrors ? MessageType.Error : MessageType.Info;
            EditorGUILayout.HelpBox(_lastGenerationReport.BuildSummary(), reportType);
        }

        EditorGUILayout.EndVertical();
    }

    private void SelectOutputFolder()
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string defaultPath = projectRoot;

        if (!string.IsNullOrWhiteSpace(_autoGenerateOutputFolder))
        {
            string candidatePath = Path.GetFullPath(Path.Combine(projectRoot, _autoGenerateOutputFolder));
            if (Directory.Exists(candidatePath))
                defaultPath = candidatePath;
        }

        string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", defaultPath, string.Empty);
        if (string.IsNullOrEmpty(selectedPath))
            return;

        string normalizedRoot = projectRoot.Replace("\\", "/");
        string normalizedSelected = selectedPath.Replace("\\", "/");

        if (!normalizedSelected.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("경로 오류", "프로젝트 내부 폴더를 선택하세요.", "확인");
            return;
        }

        string relativePath = normalizedSelected.Substring(normalizedRoot.Length).TrimStart('/');
        _autoGenerateOutputFolder = string.IsNullOrEmpty(relativePath) ? "Assets" : $"Assets/{relativePath}";
    }

    private void RunAutoGenerate()
    {
        AnimationMappingProfile profile = UnitSpriteAnimationAutoGenerator.CreateDefaultProfile();
        foreach (var pair in _autoGenerateSourceActions)
            profile.SetSourceAction(pair.Key, pair.Value);

        _lastGenerationReport = UnitSpriteAnimationAutoGenerator.Generate(
            _autoGenerateSourceLibrary,
            _autoGenerateOutputFolder,
            _autoGenerateOutputName,
            profile);

        if (string.IsNullOrWhiteSpace(_lastGenerationReport.ResultAssetPath))
            return;

        UnitSpriteAnimation createdAsset =
            AssetDatabase.LoadAssetAtPath<UnitSpriteAnimation>(_lastGenerationReport.ResultAssetPath);
        if (createdAsset == null)
            return;

        _animationData = createdAsset;
        _serializedObject = new SerializedObject(createdAsset);
        _selectedClipIndex = 0;
        _currentFrame = 0;
        _isPlaying = false;
        EditorGUIUtility.PingObject(createdAsset);
    }

    private void DrawClipList()
    {
        EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);

        var clipsProperty = _serializedObject.FindProperty("_clips");
        
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Clip", GUILayout.Width(100)))
        {
            clipsProperty.arraySize++;
            _selectedClipIndex = clipsProperty.arraySize - 1;
        }
        
        if (clipsProperty.arraySize > 0 && GUILayout.Button("- Remove", GUILayout.Width(100)))
        {
            if (_selectedClipIndex < clipsProperty.arraySize)
            {
                clipsProperty.DeleteArrayElementAtIndex(_selectedClipIndex);
                _selectedClipIndex = Mathf.Max(0, _selectedClipIndex - 1);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(5);

        for (int i = 0; i < clipsProperty.arraySize; i++)
        {
            var clipProperty = clipsProperty.GetArrayElementAtIndex(i);
            var nameProperty = clipProperty.FindPropertyRelative("Name");
            string displayName = string.IsNullOrEmpty(nameProperty.stringValue) ? $"Clip {i}" : nameProperty.stringValue;

            EditorGUILayout.BeginHorizontal();
            
            bool isSelected = i == _selectedClipIndex;
            GUI.backgroundColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : Color.white;
            
            if (GUILayout.Button(displayName, GUILayout.Height(25)))
            {
                _selectedClipIndex = i;
                _currentFrame = 0;
                _isPlaying = false;
            }
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawSelectedClipEditor()
    {
        var clipsProperty = _serializedObject.FindProperty("_clips");
        if (clipsProperty.arraySize == 0 || _selectedClipIndex >= clipsProperty.arraySize)
        {
            EditorGUILayout.HelpBox("클립을 추가하세요.", MessageType.Info);
            return;
        }

        var clipProperty = clipsProperty.GetArrayElementAtIndex(_selectedClipIndex);
        
        EditorGUILayout.LabelField("Clip Settings", EditorStyles.boldLabel);
        
        var nameProperty = clipProperty.FindPropertyRelative("Name");
        EditorGUILayout.PropertyField(nameProperty);
        DrawClipIdentityEditor(nameProperty);
        EditorGUILayout.HelpBox("Left 방향은 Right 클립 + FlipX로 처리됩니다.", MessageType.None);
        EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("Loop"));
        EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("DefaultDuration"));
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("Sprites"), true);
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Frames", EditorStyles.boldLabel);
        
        var framesProperty = clipProperty.FindPropertyRelative("Frames");
        
        DrawBatchDurationEditor(framesProperty);
        
        if (framesProperty.arraySize > 0)
        {
            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                DrawFrameEditor(framesProperty.GetArrayElementAtIndex(i), i);
            }
        }
    }

    private void DrawClipIdentityEditor(SerializedProperty nameProperty)
    {
        ParseClipName(nameProperty.stringValue, out string clipTypeName, out string clipDirectionName);

        EditorGUI.BeginChangeCheck();
        int nextTypeIndex = EditorGUILayout.Popup("Type", TypeToIndex(clipTypeName), UnitSpriteAnimationAutoGenerator.AnimationTypeNames);
        int nextDirectionIndex = EditorGUILayout.Popup("Direction", DirectionToIndex(clipDirectionName), ClipDirectionLabels);
        if (EditorGUI.EndChangeCheck())
        {
            string nextTypeName = GetTypeName(nextTypeIndex);
            string nextDirectionName = ClipDirectionOptions[Mathf.Clamp(nextDirectionIndex, 0, ClipDirectionOptions.Length - 1)];
            nameProperty.stringValue = BuildClipName(nextTypeName, nextDirectionName);
        }
    }

    private static void ParseClipName(string clipName, out string typeName, out string directionName)
    {
        typeName = GetTypeName(0);
        directionName = ClipDirectionOptions[0];

        if (string.IsNullOrEmpty(clipName))
            return;

        string[] tokens = clipName.Split('_');
        if (tokens.Length > 0)
            typeName = NormalizeTypeName(tokens[0], typeName);

        if (tokens.Length > 1)
            directionName = NormalizeDirectionName(tokens[1]);
    }

    private static string BuildClipName(string typeName, string directionName)
    {
        string normalizedTypeName = NormalizeTypeName(typeName, GetTypeName(0));
        string normalizedDirectionName = NormalizeDirectionName(directionName);
        if (string.Equals(normalizedDirectionName, "None", StringComparison.OrdinalIgnoreCase))
            return normalizedTypeName;

        return $"{normalizedTypeName}_{normalizedDirectionName}";
    }

    private static int TypeToIndex(string typeName)
    {
        for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
        {
            if (string.Equals(UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i], typeName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private static string GetTypeName(int index)
    {
        int safeIndex = Mathf.Clamp(index, 0, UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length - 1);
        return UnitSpriteAnimationAutoGenerator.AnimationTypeNames[safeIndex];
    }

    private static int DirectionToIndex(string directionName)
    {
        string normalizedDirectionName = NormalizeDirectionName(directionName);
        for (int i = 0; i < ClipDirectionOptions.Length; i++)
        {
            if (string.Equals(ClipDirectionOptions[i], normalizedDirectionName, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }

    private static string NormalizeTypeName(string typeName, string fallback)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return fallback;

        for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
        {
            string candidate = UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i];
            if (string.Equals(candidate, typeName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return fallback;
    }

    private static string NormalizeDirectionName(string directionName)
    {
        if (string.IsNullOrWhiteSpace(directionName))
            return ClipDirectionOptions[0];

        if (string.Equals(directionName, "Left", StringComparison.OrdinalIgnoreCase))
            return "Right";

        for (int i = 0; i < ClipDirectionOptions.Length; i++)
        {
            string candidate = ClipDirectionOptions[i];
            if (string.Equals(candidate, directionName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return ClipDirectionOptions[0];
    }

    private float _batchDuration = 0.1f;
    
    private void DrawBatchDurationEditor(SerializedProperty framesProperty)
    {
        if (framesProperty.arraySize == 0) return;
        
        EditorGUILayout.BeginHorizontal();
        
        EditorGUILayout.LabelField("일괄 Duration", GUILayout.Width(80));
        _batchDuration = EditorGUILayout.FloatField(_batchDuration, GUILayout.Width(60));
        EditorGUILayout.LabelField("s", GUILayout.Width(15));
        
        if (GUILayout.Button("Apply All", GUILayout.Width(70)))
        {
            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                var frameProperty = framesProperty.GetArrayElementAtIndex(i);
                frameProperty.FindPropertyRelative("Duration").floatValue = _batchDuration;
            }
        }
        
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
    }

    private void DrawFrameEditor(SerializedProperty frameProperty, int index)
    {
        bool isCurrentFrame = index == _currentFrame;
        
        EditorGUILayout.BeginVertical(isCurrentFrame ? "box" : EditorStyles.helpBox);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Frame {index}", EditorStyles.boldLabel, GUILayout.Width(70));
        
        EditorGUILayout.PropertyField(frameProperty.FindPropertyRelative("Duration"), GUIContent.none, GUILayout.Width(60));
        EditorGUILayout.LabelField("s", GUILayout.Width(15));
        
        var flipXProperty = frameProperty.FindPropertyRelative("FlipX");
        flipXProperty.boolValue = GUILayout.Toggle(flipXProperty.boolValue, "FlipX", GUILayout.Width(50));
        
        if (GUILayout.Button("▶", GUILayout.Width(25)))
        {
            _currentFrame = index;
            _isPlaying = false;
        }
        
        EditorGUILayout.EndHorizontal();

        var eventsProperty = frameProperty.FindPropertyRelative("Events");
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Events", GUILayout.Width(50));
        if (GUILayout.Button("+", GUILayout.Width(20)))
            eventsProperty.arraySize++;
        EditorGUILayout.EndHorizontal();

        for (int i = 0; i < eventsProperty.arraySize; i++)
        {
            var eventProperty = eventsProperty.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(20);
            
            EditorGUILayout.PropertyField(eventProperty.FindPropertyRelative("Type"), GUIContent.none, GUILayout.Width(70));
            EditorGUILayout.PropertyField(eventProperty.FindPropertyRelative("Param"), GUIContent.none);
            
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                eventsProperty.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        
        EditorGUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical();
        _rightScrollPosition = EditorGUILayout.BeginScrollView(_rightScrollPosition);

        if (_animationData == null || _animationData.Clips == null || _animationData.Clips.Length == 0)
        {
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        if (_selectedClipIndex >= _animationData.Clips.Length)
        {
            _selectedClipIndex = 0;
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        var clip = _animationData.Clips[_selectedClipIndex];
        if (clip.Sprites == null || clip.Sprites.Length == 0)
        {
            EditorGUILayout.HelpBox("Sprites를 추가하세요.", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        
        DrawPlaybackControls();
        DrawSpritePreview(clip);
        
        EditorGUILayout.Space(10);
        DrawTimeline(clip);
        DrawFrameInfo(clip);

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawPlaybackControls()
    {
        EditorGUILayout.BeginHorizontal();
        
        if (GUILayout.Button(_isPlaying ? "■ Stop" : "▶ Preview", GUILayout.Width(80)))
        {
            _isPlaying = !_isPlaying;
            if (_isPlaying)
            {
                _lastTime = EditorApplication.timeSinceStartup;
                _currentFrame = 0;
                _frameTime = 0f;
            }
        }

        if (GUILayout.Button("↺ Reset", GUILayout.Width(70)))
        {
            _isPlaying = false;
            _currentFrame = 0;
            _frameTime = 0f;
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawSpritePreview(UnitAnimationClip clip)
    {
        if (_currentFrame >= clip.Sprites.Length) return;

        Sprite sprite = clip.Sprites[_currentFrame];
        if (sprite == null || sprite.texture == null) return;

        bool flipX = clip.Frames != null && _currentFrame < clip.Frames.Length 
            ? clip.Frames[_currentFrame].FlipX 
            : false;

        float previewSize = 200f;
        Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);

        EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));

        Rect spriteRect = sprite.rect;
        Rect uvRect = new Rect(
            spriteRect.x / sprite.texture.width,
            spriteRect.y / sprite.texture.height,
            spriteRect.width / sprite.texture.width,
            spriteRect.height / sprite.texture.height
        );

        if (flipX)
        {
            uvRect.x += uvRect.width;
            uvRect.width = -uvRect.width;
        }

        float aspectRatio = spriteRect.width / spriteRect.height;
        Rect drawRect;
        
        if (aspectRatio > 1)
        {
            float height = previewSize / aspectRatio;
            drawRect = new Rect(previewRect.x, previewRect.y + (previewSize - height) / 2, previewSize, height);
        }
        else
        {
            float width = previewSize * aspectRatio;
            drawRect = new Rect(previewRect.x + (previewSize - width) / 2, previewRect.y, width, previewSize);
        }

        GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uvRect);
    }

    private void DrawTimeline(UnitAnimationClip clip)
    {
        EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

        float totalDuration = clip.GetTotalDuration();
        if (totalDuration <= 0) return;

        Rect timelineRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 500, 40);
        
        EditorGUI.DrawRect(timelineRect, new Color(0.25f, 0.25f, 0.25f));

        float x = timelineRect.x;
        for (int i = 0; i < clip.Sprites.Length; i++)
        {
            float frameDuration = clip.GetFrameDuration(i);
            float frameWidth = (frameDuration / totalDuration) * timelineRect.width;
            Rect frameRect = new Rect(x, timelineRect.y, frameWidth - 2, timelineRect.height);

            bool hasEvents = clip.Frames != null 
                && i < clip.Frames.Length 
                && clip.Frames[i].Events != null 
                && clip.Frames[i].Events.Length > 0;

            Color color;
            if (i == _currentFrame)
                color = hasEvents ? new Color(0.8f, 0.8f, 0.3f) : new Color(0.3f, 0.7f, 0.3f);
            else
                color = hasEvents ? new Color(0.6f, 0.5f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);

            EditorGUI.DrawRect(frameRect, color);

            if (frameWidth > 20)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10 };
                GUI.Label(frameRect, i.ToString(), style);
            }

            if (Event.current.type == EventType.MouseDown && frameRect.Contains(Event.current.mousePosition))
            {
                _currentFrame = i;
                _frameTime = 0f;
                _isPlaying = false;
                Event.current.Use();
                Repaint();
            }

            x += frameWidth;
        }
    }

    private void DrawFrameInfo(UnitAnimationClip clip)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField($"Frame: {_currentFrame} / {clip.Sprites.Length - 1}");
        EditorGUILayout.LabelField($"Duration: {clip.GetTotalDuration():F2}s");

        if (clip.Frames != null && _currentFrame < clip.Frames.Length)
        {
            var frame = clip.Frames[_currentFrame];
            if (frame.Events != null && frame.Events.Length > 0)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Events:", EditorStyles.boldLabel);
                foreach (var evt in frame.Events)
                    EditorGUILayout.LabelField($"  • {evt.Type}: {evt.Param}");
            }
        }
    }

    private void CreateNewAsset()
    {
        string path = EditorUtility.SaveFilePanelInProject(
            "Create Unit Sprite Animation", 
            "NewUnitAnimation", 
            "asset",
            "Select location",
            "Assets/Resources/Data/UnitAnimationData");

        if (string.IsNullOrEmpty(path)) return;

        var newAsset = CreateInstance<UnitSpriteAnimation>();
        AssetDatabase.CreateAsset(newAsset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        _animationData = newAsset;
        _serializedObject = new SerializedObject(newAsset);
        _selectedClipIndex = 0;
        EditorGUIUtility.PingObject(newAsset);
    }
}
