using KinKeep.SpriteKit;
using UnityEditor;
using UnityEngine;

namespace KinKeep.SpriteKit.Editor
{
    public class UnitSpriteAnimationEditor : EditorWindow
    {
        private readonly UnitSpriteAnimationPreviewPanel _previewPanel = new UnitSpriteAnimationPreviewPanel();
        private readonly UnitSpriteAnimationAutoGeneratePanel _autoGeneratePanel = new UnitSpriteAnimationAutoGeneratePanel();

        private UnitSpriteAnimation _animationData;
        private SerializedObject _serializedObject;
        private Vector2 _leftScrollPosition;
        private Vector2 _rightScrollPosition;
        private int _selectedClipIndex;
        private float _batchDuration = 0.1f;

        [MenuItem("KinKeep/Sprite Animation Editor")]
        public static void ShowWindow()
        {
            UnitSpriteAnimationEditor window = GetWindow<UnitSpriteAnimationEditor>("Unit Animation Editor");
            window.minSize = new Vector2(900f, 600f);
        }

        public static void OpenWithAsset(UnitSpriteAnimation data)
        {
            UnitSpriteAnimationEditor window = GetWindow<UnitSpriteAnimationEditor>("Unit Animation Editor");
            window.minSize = new Vector2(900f, 600f);
            window.SetAnimationData(data);
            window.Repaint();
        }

        [UnityEditor.Callbacks.OnOpenAsset]
        public static bool OnOpenAsset(int instanceId, int line)
        {
            UnitSpriteAnimation asset = EditorUtility.InstanceIDToObject(instanceId) as UnitSpriteAnimation;
            if (asset == null)
            {
                return false;
            }

            OpenWithAsset(asset);
            return true;
        }

        private void OnEnable()
        {
            Selection.selectionChanged += OnSelectionChanged;
            EditorApplication.update += OnEditorUpdate;
            _autoGeneratePanel.EnsureConfig(_animationData);
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
                SetAnimationData(data);
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            _previewPanel.OnEditorUpdate(_animationData, _selectedClipIndex);
            if (_previewPanel.IsPlaying)
            {
                Repaint();
            }
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
            UnitSpriteAnimation nextAnimationData = (UnitSpriteAnimation)EditorGUILayout.ObjectField(
                _animationData,
                typeof(UnitSpriteAnimation),
                false,
                GUILayout.Width(250));

            if (EditorGUI.EndChangeCheck())
            {
                SetAnimationData(nextAnimationData);
            }

            if (GUILayout.Button("New", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                CreateNewAsset();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawLeftPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(450f));
            _leftScrollPosition = EditorGUILayout.BeginScrollView(_leftScrollPosition);

            if (_animationData == null)
            {
                EditorGUILayout.HelpBox("Select a UnitSpriteAnimation asset or create a new one.", MessageType.Info);
            }
            else if (!EnsureSerializedObject())
            {
                EditorGUILayout.HelpBox("SerializedObject could not be created. Re-select the asset.", MessageType.Warning);
            }
            else
            {
                _serializedObject.Update();
                DrawConfigurationEditor();
                EditorGUILayout.Space(10f);
                DrawClipList();
                EditorGUILayout.Space(10f);
                DrawSelectedClipEditor();

                bool hasSerializedChanges = _serializedObject.ApplyModifiedProperties();
                // Keep frame metadata aligned after any inspector edit.
                SyncAfterSerializedChanges(hasSerializedChanges);
            }

            EditorGUILayout.Space(12f);
            if (_autoGeneratePanel.Draw(_animationData, out UnitSpriteAnimation generatedAsset))
            {
                SetAnimationData(generatedAsset);
            }

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

        private void SyncAfterSerializedChanges(bool hasSerializedChanges)
        {
            if (_animationData == null)
            {
                return;
            }

            bool isDirty = hasSerializedChanges;
            if (_animationData.SyncClipFrames())
            {
                _serializedObject = new SerializedObject(_animationData);
                isDirty = true;
            }

            if (isDirty)
            {
                EditorUtility.SetDirty(_animationData);
            }
        }

        private void DrawConfigurationEditor()
        {
            EditorGUILayout.LabelField("Direction Settings", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Direction Entries define clip suffix names. Flip Entries let a missing direction reuse another direction with FlipX. Generator Direction Entries map source category suffixes to direction indices.",
                MessageType.None);

            SerializedProperty directionEntriesProperty = _serializedObject.FindProperty("_directionEntries");
            SerializedProperty flipEntriesProperty = _serializedObject.FindProperty("_flipEntries");
            SerializedProperty generatorEntriesProperty = _serializedObject.FindProperty("_generatorDirectionEntries");
            SerializedProperty clipsProperty = _serializedObject.FindProperty("_clips");

            EditorGUILayout.PropertyField(directionEntriesProperty, true);
            EditorGUILayout.PropertyField(flipEntriesProperty, true);
            EditorGUILayout.PropertyField(generatorEntriesProperty, true);
            UnitSpriteAnimationEditorValidationUtility.DrawDirectionWarnings(
                directionEntriesProperty,
                flipEntriesProperty,
                generatorEntriesProperty,
                clipsProperty);
        }

        private void DrawClipList()
        {
            EditorGUILayout.LabelField("Clips", EditorStyles.boldLabel);
            SerializedProperty clipsProperty = _serializedObject.FindProperty("_clips");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("+ Add Clip", GUILayout.Width(100f)))
            {
                clipsProperty.arraySize++;
                _selectedClipIndex = clipsProperty.arraySize - 1;
                _previewPanel.ResetPlayback();
            }

            if (clipsProperty.arraySize > 0 && GUILayout.Button("- Remove", GUILayout.Width(100f)))
            {
                if (_selectedClipIndex < clipsProperty.arraySize)
                {
                    clipsProperty.DeleteArrayElementAtIndex(_selectedClipIndex);
                    _selectedClipIndex = Mathf.Clamp(_selectedClipIndex - 1, 0, Mathf.Max(0, clipsProperty.arraySize - 1));
                    _previewPanel.ResetPlayback();
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5f);
            for (int i = 0; i < clipsProperty.arraySize; i++)
            {
                SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(i);
                SerializedProperty nameProperty = clipProperty.FindPropertyRelative("_name");
                string displayName = string.IsNullOrEmpty(nameProperty.stringValue) ? $"Clip {i}" : nameProperty.stringValue;

                EditorGUILayout.BeginHorizontal();
                bool isSelected = i == _selectedClipIndex;
                GUI.backgroundColor = isSelected ? new Color(0.3f, 0.5f, 0.8f) : Color.white;

                if (GUILayout.Button(displayName, GUILayout.Height(25f)))
                {
                    _selectedClipIndex = i;
                    _previewPanel.ResetPlayback();
                }

                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawSelectedClipEditor()
        {
            SerializedProperty clipsProperty = _serializedObject.FindProperty("_clips");
            if (clipsProperty.arraySize == 0 || _selectedClipIndex >= clipsProperty.arraySize)
            {
                EditorGUILayout.HelpBox("Add a clip to start editing.", MessageType.Info);
                return;
            }

            SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(_selectedClipIndex);
            EditorGUILayout.LabelField("Clip Settings", EditorStyles.boldLabel);

            SerializedProperty nameProperty = clipProperty.FindPropertyRelative("_name");
            EditorGUILayout.PropertyField(nameProperty);
            DrawClipIdentityEditor(nameProperty);
            EditorGUILayout.HelpBox(
                "Explicit clip names win first. If a requested direction clip is missing, runtime can fall back through Flip Entries.",
                MessageType.None);

            EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("_loop"));
            EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("_defaultDuration"));

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Sprites", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(clipProperty.FindPropertyRelative("_sprites"), true);

            EditorGUILayout.Space(10f);
            EditorGUILayout.LabelField("Frames", EditorStyles.boldLabel);
            SerializedProperty framesProperty = clipProperty.FindPropertyRelative("_frames");
            DrawBatchDurationEditor(framesProperty);

            for (int i = 0; i < framesProperty.arraySize; i++)
            {
                DrawFrameEditor(framesProperty.GetArrayElementAtIndex(i), i);
            }
        }

        private void DrawClipIdentityEditor(SerializedProperty nameProperty)
        {
            UnitSpriteAnimationEditorClipNaming.ParseClipName(
                _animationData,
                nameProperty.stringValue,
                out string clipTypeName,
                out int directionPopupIndex);

            string[] directionLabels = UnitSpriteAnimationEditorClipNaming.BuildDirectionLabels(_animationData);

            EditorGUI.BeginChangeCheck();
            int nextTypeIndex = EditorGUILayout.Popup(
                "Type",
                UnitSpriteAnimationEditorClipNaming.TypeToIndex(clipTypeName),
                UnitSpriteAnimationAutoGenerator.AnimationTypeNames);
            int nextDirectionPopupIndex = EditorGUILayout.Popup("Direction", directionPopupIndex, directionLabels);

            if (EditorGUI.EndChangeCheck())
            {
                string nextTypeName = UnitSpriteAnimationEditorClipNaming.GetTypeName(nextTypeIndex);
                nameProperty.stringValue = UnitSpriteAnimationEditorClipNaming.BuildClipName(
                    _animationData,
                    nextTypeName,
                    nextDirectionPopupIndex);
            }
        }

        private void DrawBatchDurationEditor(SerializedProperty framesProperty)
        {
            if (framesProperty.arraySize == 0)
            {
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Batch Duration", GUILayout.Width(90f));
            _batchDuration = EditorGUILayout.FloatField(_batchDuration, GUILayout.Width(60f));
            EditorGUILayout.LabelField("s", GUILayout.Width(15f));

            if (GUILayout.Button("Apply All", GUILayout.Width(70f)))
            {
                for (int i = 0; i < framesProperty.arraySize; i++)
                {
                    SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(i);
                    frameProperty.FindPropertyRelative("_duration").floatValue = _batchDuration;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5f);
        }

        private void DrawFrameEditor(SerializedProperty frameProperty, int index)
        {
            bool isCurrentFrame = index == _previewPanel.CurrentFrame;
            EditorGUILayout.BeginVertical(isCurrentFrame ? "box" : EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Frame {index}", EditorStyles.boldLabel, GUILayout.Width(70f));
            EditorGUILayout.PropertyField(frameProperty.FindPropertyRelative("_duration"), GUIContent.none, GUILayout.Width(60f));
            EditorGUILayout.LabelField("s", GUILayout.Width(15f));

            SerializedProperty flipXProperty = frameProperty.FindPropertyRelative("_flipX");
            flipXProperty.boolValue = GUILayout.Toggle(flipXProperty.boolValue, "FlipX", GUILayout.Width(50f));

            if (GUILayout.Button("▶", GUILayout.Width(25f)))
            {
                _previewPanel.SelectFrame(index);
            }

            EditorGUILayout.EndHorizontal();

            SerializedProperty eventsProperty = frameProperty.FindPropertyRelative("_events");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Events", GUILayout.Width(50f));
            if (GUILayout.Button("+", GUILayout.Width(20f)))
            {
                eventsProperty.arraySize++;
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < eventsProperty.arraySize; i++)
            {
                SerializedProperty eventProperty = eventsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(20f);
                EditorGUILayout.PropertyField(eventProperty.FindPropertyRelative("_type"), GUIContent.none, GUILayout.Width(70f));
                EditorGUILayout.PropertyField(eventProperty.FindPropertyRelative("_param"), GUIContent.none);

                bool isRemoved = false;
                if (GUILayout.Button("×", GUILayout.Width(20f)))
                {
                    eventsProperty.DeleteArrayElementAtIndex(i);
                    isRemoved = true;
                }

                EditorGUILayout.EndHorizontal();

                if (isRemoved)
                    break;
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

            UnitAnimationClip clip = _animationData.Clips[_selectedClipIndex];
            if (clip == null || clip.Sprites == null || clip.Sprites.Length == 0)
            {
                EditorGUILayout.HelpBox("Add sprites to preview this clip.", MessageType.Info);
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            _previewPanel.Draw(clip);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void CreateNewAsset()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create Unit Sprite Animation",
                "NewUnitAnimation",
                "asset",
                "Select location",
                "Assets/Resources/Data/UnitAnimationData");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            UnitSpriteAnimation newAsset = CreateInstance<UnitSpriteAnimation>();
            AssetDatabase.CreateAsset(newAsset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SetAnimationData(newAsset);
            EditorGUIUtility.PingObject(newAsset);
        }

        private void SetAnimationData(UnitSpriteAnimation animationData)
        {
            _animationData = animationData;
            _serializedObject = animationData != null ? new SerializedObject(animationData) : null;
            _selectedClipIndex = 0;
            _previewPanel.ResetPlayback();
            _autoGeneratePanel.OnAnimationSelected(animationData);
        }
    }
}
