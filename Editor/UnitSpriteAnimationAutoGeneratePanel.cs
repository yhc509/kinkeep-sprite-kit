using KinKeep.SpriteKit;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace KinKeep.SpriteKit.Editor
{
    public sealed class UnitSpriteAnimationAutoGeneratePanel
    {
        private bool _isExpanded = true;
        private SpriteLibraryAsset _sourceLibrary;
        private string _outputFolder = UnitSpriteAnimationAutoGenerator.DefaultOutputFolder;
        private string _outputName;
        private Dictionary<string, string> _sourceActions;
        private GenerationReport _lastReport;

        public void EnsureConfig(UnitSpriteAnimation animationData)
        {
            if (_sourceLibrary == null)
            {
                _sourceLibrary = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(
                    UnitSpriteAnimationAutoGenerator.DefaultSourceLibraryPath);
            }

            if (string.IsNullOrWhiteSpace(_outputFolder))
            {
                _outputFolder = UnitSpriteAnimationAutoGenerator.DefaultOutputFolder;
            }

            if (_sourceActions == null || _sourceActions.Count == 0)
            {
                _sourceActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                AnimationMappingProfile defaultProfile = UnitSpriteAnimationAutoGenerator.CreateDefaultProfile();
                for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
                {
                    string typeName = UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i];
                    _sourceActions[typeName] = defaultProfile.GetSourceAction(typeName);
                }
            }

            if (string.IsNullOrWhiteSpace(_outputName))
            {
                if (animationData != null)
                {
                    _outputName = animationData.name;
                }
                else if (_sourceLibrary != null)
                {
                    _outputName = _sourceLibrary.name;
                }
            }
        }

        public void OnAnimationSelected(UnitSpriteAnimation animationData)
        {
            if (animationData != null && string.IsNullOrWhiteSpace(_outputName))
            {
                _outputName = animationData.name;
            }
        }

        public bool Draw(UnitSpriteAnimation animationData, out UnitSpriteAnimation generatedAsset)
        {
            generatedAsset = null;
            EnsureConfig(animationData);

            _isExpanded = EditorGUILayout.Foldout(_isExpanded, "Auto Generate", true);
            if (!_isExpanded)
            {
                return false;
            }

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            _sourceLibrary = (SpriteLibraryAsset)EditorGUILayout.ObjectField(
                "Source",
                _sourceLibrary,
                typeof(SpriteLibraryAsset),
                false);

            EditorGUILayout.BeginHorizontal();
            _outputFolder = EditorGUILayout.TextField("Output Folder", _outputFolder);
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                SelectOutputFolder();
            }
            EditorGUILayout.EndHorizontal();

            _outputName = EditorGUILayout.TextField("Output Name", _outputName);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Type -> Action Mapping", EditorStyles.boldLabel);
            for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
            {
                string typeName = UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i];
                if (!_sourceActions.TryGetValue(typeName, out string sourceAction))
                {
                    sourceAction = string.Empty;
                }

                _sourceActions[typeName] = EditorGUILayout.TextField(typeName, sourceAction);
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                "Direction suffix mapping and generated output directions come from the asset configuration.\n" +
                "Frame slices: Jab 0~2, Cast 0~3\n" +
                "Attack/Skill: middle frame gets Hit event automatically.\n" +
                "Existing target assets are updated to match the selected settings source.",
                MessageType.None);

            if (GUILayout.Button("Generate UnitSpriteAnimation", GUILayout.Height(28)))
            {
                generatedAsset = RunAutoGenerate(animationData);
            }

            if (_lastReport != null)
            {
                MessageType reportType = _lastReport.HasErrors ? MessageType.Error : MessageType.Info;
                EditorGUILayout.HelpBox(_lastReport.BuildSummary(), reportType);
            }

            EditorGUILayout.EndVertical();
            return generatedAsset != null;
        }

        private void SelectOutputFolder()
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath);
            string defaultPath = assetsRoot;

            if (!string.IsNullOrWhiteSpace(_outputFolder))
            {
                string relativeFolder = _outputFolder == "Assets"
                    ? string.Empty
                    : _outputFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)
                        ? _outputFolder.Substring("Assets/".Length)
                        : _outputFolder;
                string candidatePath = Path.GetFullPath(Path.Combine(assetsRoot, relativeFolder));
                if (Directory.Exists(candidatePath))
                {
                    defaultPath = candidatePath;
                }
            }

            string selectedPath = EditorUtility.OpenFolderPanel("Select Output Folder", defaultPath, string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            string normalizedRoot = assetsRoot.Replace("\\", "/");
            string normalizedSelected = Path.GetFullPath(selectedPath).Replace("\\", "/");
            if (!normalizedSelected.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Path Error", "Select a folder inside this project's Assets folder.", "OK");
                return;
            }

            string relativePath = normalizedSelected.Substring(normalizedRoot.Length).TrimStart('/');
            _outputFolder = string.IsNullOrEmpty(relativePath) ? "Assets" : $"Assets/{relativePath}";
        }

        private UnitSpriteAnimation RunAutoGenerate(UnitSpriteAnimation animationData)
        {
            AnimationMappingProfile profile = UnitSpriteAnimationAutoGenerator.CreateDefaultProfile();
            foreach (KeyValuePair<string, string> pair in _sourceActions)
            {
                profile.SetSourceAction(pair.Key, pair.Value);
            }

            _lastReport = UnitSpriteAnimationAutoGenerator.Generate(
                _sourceLibrary,
                _outputFolder,
                _outputName,
                profile,
                animationData);

            if (string.IsNullOrWhiteSpace(_lastReport.ResultAssetPath))
            {
                return null;
            }

            UnitSpriteAnimation createdAsset =
                AssetDatabase.LoadAssetAtPath<UnitSpriteAnimation>(_lastReport.ResultAssetPath);
            if (createdAsset == null)
            {
                return null;
            }

            EditorGUIUtility.PingObject(createdAsset);
            return createdAsset;
        }
    }
}
