using System;
using System.Collections.Generic;
using UnityEditor;

namespace KinKeep.SpriteKit.Editor
{
    internal static class UnitSpriteAnimationEditorValidationUtility
    {
        public static void DrawDirectionWarnings(
            SerializedProperty directionEntriesProperty,
            SerializedProperty clipsProperty)
        {
            if (TryGetDuplicateIndexWarning(directionEntriesProperty, out string duplicateIndexWarning))
                EditorGUILayout.HelpBox(duplicateIndexWarning, MessageType.Warning);

            if (TryGetDuplicateNameWarning(directionEntriesProperty, out string duplicateNameWarning))
                EditorGUILayout.HelpBox(duplicateNameWarning, MessageType.Warning);

            if (TryGetDirectionNameMismatchWarning(directionEntriesProperty, clipsProperty, out string mismatchWarning))
                EditorGUILayout.HelpBox(mismatchWarning, MessageType.Warning);
        }

        private static bool TryGetDuplicateIndexWarning(SerializedProperty directionEntriesProperty, out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null)
                return false;

            var seen = new HashSet<int>();
            var duplicates = new HashSet<int>();
            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                int index = directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_index").intValue;
                if (!seen.Add(index))
                    duplicates.Add(index);
            }

            if (duplicates.Count == 0)
                return false;

            warning = $"Duplicate direction Index detected: {string.Join(", ", duplicates)}";
            return true;
        }

        private static bool TryGetDuplicateNameWarning(SerializedProperty directionEntriesProperty, out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null)
                return false;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                string name = NormalizeName(
                    directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
                if (string.IsNullOrEmpty(name))
                    continue;

                if (!seen.Add(name))
                    duplicates.Add(name);
            }

            if (duplicates.Count == 0)
                return false;

            warning = $"Duplicate direction Name detected: {string.Join(", ", duplicates)}";
            return true;
        }

        private static bool TryGetDirectionNameMismatchWarning(
            SerializedProperty directionEntriesProperty,
            SerializedProperty clipsProperty,
            out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null || clipsProperty == null)
                return false;

            var configuredDirectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool hasDefaultDirectionName = false;
            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                string directionName = NormalizeName(
                    directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
                if (string.IsNullOrEmpty(directionName) || string.Equals(directionName, "None", StringComparison.OrdinalIgnoreCase))
                    hasDefaultDirectionName = true;

                if (!string.IsNullOrEmpty(directionName))
                    configuredDirectionNames.Add(directionName);
            }

            var mismatchedClipDirectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < clipsProperty.arraySize; i++)
            {
                string clipName = NormalizeName(
                    clipsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue);
                int separatorIndex = clipName.LastIndexOf('_');
                if (separatorIndex < 0 || separatorIndex >= clipName.Length - 1)
                {
                    if (!string.IsNullOrEmpty(clipName) && !hasDefaultDirectionName)
                        mismatchedClipDirectionNames.Add(clipName);
                    continue;
                }

                string clipDirectionName = NormalizeName(clipName.Substring(separatorIndex + 1));
                if (string.IsNullOrEmpty(clipDirectionName) || configuredDirectionNames.Contains(clipDirectionName))
                    continue;

                mismatchedClipDirectionNames.Add(clipDirectionName);
            }

            if (mismatchedClipDirectionNames.Count == 0)
                return false;

            warning =
                $"방향 이름이 변경되어 기존 클립과 불일치합니다: {string.Join(", ", mismatchedClipDirectionNames)}";
            return true;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
