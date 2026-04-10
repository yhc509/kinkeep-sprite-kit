using System;
using System.Collections.Generic;
using UnityEditor;

namespace KinKeep.SpriteKit.Editor
{
    internal static class UnitSpriteAnimationEditorValidationUtility
    {
        public static void DrawDirectionWarnings(
            SerializedProperty directionEntriesProperty,
            SerializedProperty flipEntriesProperty,
            SerializedProperty generatorDirectionEntriesProperty,
            SerializedProperty clipsProperty)
        {
            List<string> warnings = CollectWarnings(
                directionEntriesProperty,
                flipEntriesProperty,
                generatorDirectionEntriesProperty,
                clipsProperty);

            for (int i = 0; i < warnings.Count; i++)
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
        }

        private static List<string> CollectWarnings(
            SerializedProperty directionEntriesProperty,
            SerializedProperty flipEntriesProperty,
            SerializedProperty generatorDirectionEntriesProperty,
            SerializedProperty clipsProperty)
        {
            var warnings = new List<string>();

            if (TryGetDuplicateIndexWarning(directionEntriesProperty, out string duplicateIndexWarning))
                warnings.Add(duplicateIndexWarning);

            if (TryGetDuplicateNameWarning(directionEntriesProperty, out string duplicateNameWarning))
                warnings.Add(duplicateNameWarning);

            if (TryGetEmptyDirectionNameWarning(directionEntriesProperty, out string emptyDirectionNameWarning))
                warnings.Add(emptyDirectionNameWarning);

            if (TryGetMissingDefaultDirectionWarning(directionEntriesProperty, out string missingDefaultDirectionWarning))
                warnings.Add(missingDefaultDirectionWarning);

            if (TryGetInvalidFlipDirectionWarning(directionEntriesProperty, flipEntriesProperty, out string invalidFlipDirectionWarning))
                warnings.Add(invalidFlipDirectionWarning);

            if (TryGetInvalidGeneratorDirectionWarning(
                    directionEntriesProperty,
                    generatorDirectionEntriesProperty,
                    out string invalidGeneratorDirectionWarning))
            {
                warnings.Add(invalidGeneratorDirectionWarning);
            }

            if (TryGetEmptyGeneratorSuffixWarning(generatorDirectionEntriesProperty, out string emptyGeneratorSuffixWarning))
                warnings.Add(emptyGeneratorSuffixWarning);

            if (TryGetDirectionNameMismatchWarning(directionEntriesProperty, clipsProperty, out string mismatchWarning))
                warnings.Add(mismatchWarning);

            return warnings;
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

        private static bool TryGetEmptyDirectionNameWarning(SerializedProperty directionEntriesProperty, out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null)
                return false;

            var emptyEntryIndices = new List<int>();
            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                string name = NormalizeName(
                    directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
                if (!string.IsNullOrEmpty(name))
                    continue;

                emptyEntryIndices.Add(i);
            }

            if (emptyEntryIndices.Count == 0)
                return false;

            warning = $"Direction Entry Name is empty at element(s): {string.Join(", ", emptyEntryIndices)}";
            return true;
        }

        private static bool TryGetMissingDefaultDirectionWarning(SerializedProperty directionEntriesProperty, out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null)
                return false;

            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                string name = NormalizeName(
                    directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
                if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            warning = "Default direction entry is missing. Add a direction named 'None' for base clip names.";
            return true;
        }

        private static bool TryGetInvalidFlipDirectionWarning(
            SerializedProperty directionEntriesProperty,
            SerializedProperty flipEntriesProperty,
            out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null || flipEntriesProperty == null)
                return false;

            HashSet<int> validDirectionIndices = CollectDirectionIndices(directionEntriesProperty);
            var invalidEntries = new List<string>();
            for (int i = 0; i < flipEntriesProperty.arraySize; i++)
            {
                SerializedProperty flipEntryProperty = flipEntriesProperty.GetArrayElementAtIndex(i);
                int sourceDirection = flipEntryProperty.FindPropertyRelative("_sourceDirection").intValue;
                int targetDirection = flipEntryProperty.FindPropertyRelative("_targetDirection").intValue;

                if (validDirectionIndices.Contains(sourceDirection) && validDirectionIndices.Contains(targetDirection))
                    continue;

                invalidEntries.Add($"[{i}] {sourceDirection} -> {targetDirection}");
            }

            if (invalidEntries.Count == 0)
                return false;

            warning = $"Flip Entry direction index is out of range: {string.Join(", ", invalidEntries)}";
            return true;
        }

        private static bool TryGetInvalidGeneratorDirectionWarning(
            SerializedProperty directionEntriesProperty,
            SerializedProperty generatorDirectionEntriesProperty,
            out string warning)
        {
            warning = string.Empty;
            if (directionEntriesProperty == null || generatorDirectionEntriesProperty == null)
                return false;

            HashSet<int> validDirectionIndices = CollectDirectionIndices(directionEntriesProperty);
            var invalidEntries = new List<string>();
            for (int i = 0; i < generatorDirectionEntriesProperty.arraySize; i++)
            {
                SerializedProperty generatorEntryProperty = generatorDirectionEntriesProperty.GetArrayElementAtIndex(i);
                int directionIndex = generatorEntryProperty.FindPropertyRelative("_directionIndex").intValue;
                if (validDirectionIndices.Contains(directionIndex))
                    continue;

                string suffix = NormalizeName(generatorEntryProperty.FindPropertyRelative("_suffix").stringValue);
                invalidEntries.Add($"[{i}] suffix='{suffix}' directionIndex={directionIndex}");
            }

            if (invalidEntries.Count == 0)
                return false;

            warning = $"Generator Direction Entry index is out of range: {string.Join(", ", invalidEntries)}";
            return true;
        }

        private static bool TryGetEmptyGeneratorSuffixWarning(
            SerializedProperty generatorDirectionEntriesProperty,
            out string warning)
        {
            warning = string.Empty;
            if (generatorDirectionEntriesProperty == null)
                return false;

            var emptyEntryIndices = new List<int>();
            for (int i = 0; i < generatorDirectionEntriesProperty.arraySize; i++)
            {
                string suffix = NormalizeName(
                    generatorDirectionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_suffix").stringValue);
                if (!string.IsNullOrEmpty(suffix))
                    continue;

                emptyEntryIndices.Add(i);
            }

            if (emptyEntryIndices.Count == 0)
                return false;

            warning = $"Generator Direction Entry suffix is empty at element(s): {string.Join(", ", emptyEntryIndices)}";
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
                if (string.Equals(directionName, "None", StringComparison.OrdinalIgnoreCase))
                    hasDefaultDirectionName = true;

                if (!string.IsNullOrEmpty(directionName))
                    configuredDirectionNames.Add(directionName);
            }

            var mismatchedClipDirectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < clipsProperty.arraySize; i++)
            {
                string clipName = NormalizeName(
                    clipsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_name").stringValue);
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
                $"Direction name changed, mismatched with existing clips: {string.Join(", ", mismatchedClipDirectionNames)}";
            return true;
        }

        private static HashSet<int> CollectDirectionIndices(SerializedProperty directionEntriesProperty)
        {
            var directionIndices = new HashSet<int>();
            for (int i = 0; i < directionEntriesProperty.arraySize; i++)
            {
                int directionIndex = directionEntriesProperty.GetArrayElementAtIndex(i).FindPropertyRelative("_index").intValue;
                directionIndices.Add(directionIndex);
            }

            return directionIndices;
        }

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }
}
