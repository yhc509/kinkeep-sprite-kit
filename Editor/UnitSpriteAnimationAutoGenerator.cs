using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;
using KinKeep.SpriteKit;

namespace KinKeep.SpriteKit.Editor
{
    public static class UnitSpriteAnimationAutoGenerator
    {
        public const string DefaultSourceLibraryPath = "Assets/SpriteSheet.asset";
        public const string DefaultOutputFolder = "Assets/Resources/Data/UnitAnimationData";
        private const float DefaultDuration = 0.15f;

        public static readonly string[] AnimationTypeNames =
        {
            "Idle",
            "Move",
            "Attack",
            "Skill",
            "Damaged",
            "Die",
            "Concentrate",
            "Debuff",
            "Buff"
        };

        public static AnimationMappingProfile CreateDefaultProfile()
        {
            return AnimationMappingProfile.CreateDefault();
        }

        public static GenerationReport Generate(
            SpriteLibraryAsset sourceLibrary,
            string outputFolder,
            string outputName,
            AnimationMappingProfile mappingProfile,
            UnitSpriteAnimation settingsSource = null)
        {
            var report = new GenerationReport();

            if (sourceLibrary == null)
            {
                report.AddError("Source SpriteLibraryAsset is null.");
                report.LogToConsole();
                return report;
            }

            string normalizedOutputName = UnitSpriteAnimationGeneratorPathUtility.NormalizeName(outputName);
            if (string.IsNullOrEmpty(normalizedOutputName))
            {
                report.AddError("Output name is empty.");
                report.LogToConsole();
                return report;
            }

            if (normalizedOutputName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                report.AddError($"Output name contains invalid file name characters: {normalizedOutputName}");
                report.LogToConsole();
                return report;
            }

            string normalizedOutputFolder = UnitSpriteAnimationGeneratorPathUtility.NormalizeFolderPath(outputFolder);
            if (!UnitSpriteAnimationGeneratorPathUtility.EnsureOutputFolder(normalizedOutputFolder, report))
            {
                report.LogToConsole();
                return report;
            }

            string outputAssetPath = $"{normalizedOutputFolder}/{normalizedOutputName}.asset";
            UnitSpriteAnimation existingAsset = AssetDatabase.LoadAssetAtPath<UnitSpriteAnimation>(outputAssetPath);
            AnimationMappingProfile profile = (mappingProfile ?? CreateDefaultProfile()).Clone();

            UnitSpriteAnimation settingsAsset = ResolveSettingsAsset(existingAsset, settingsSource, out bool shouldDestroySettingsAsset);
            try
            {
                Dictionary<string, Dictionary<int, List<Sprite>>> frameSource =
                    BuildFrameSource(sourceLibrary, settingsAsset, report);

                List<UnitAnimationClip> clips = BuildClips(frameSource, profile, settingsAsset, report);
                report.MarkGeneratedClipCount(clips.Count);

                if (clips.Count == 0)
                {
                    report.AddError("No clips were generated.");
                    report.LogToConsole();
                    return report;
                }

                if (existingAsset != null)
                {
                    UpdateAsset(existingAsset, clips, settingsAsset);
                    report.MarkUpdated(outputAssetPath);
                }
                else
                {
                    CreateAsset(outputAssetPath, clips, settingsAsset);
                    report.MarkCreated(outputAssetPath);
                }
            }
            finally
            {
                if (shouldDestroySettingsAsset)
                    UnityEngine.Object.DestroyImmediate(settingsAsset);
            }

            report.LogToConsole();
            return report;
        }

        private static UnitSpriteAnimation ResolveSettingsAsset(
            UnitSpriteAnimation existingAsset,
            UnitSpriteAnimation settingsSource,
            out bool shouldDestroySettingsAsset)
        {
            if (settingsSource != null)
            {
                // Explicit settings from the selected asset win so regenerate/update uses the requested direction schema.
                shouldDestroySettingsAsset = false;
                return settingsSource;
            }

            if (existingAsset != null)
            {
                shouldDestroySettingsAsset = false;
                return existingAsset;
            }

            shouldDestroySettingsAsset = true;
            return ScriptableObject.CreateInstance<UnitSpriteAnimation>();
        }

        private static Dictionary<string, Dictionary<int, List<Sprite>>> BuildFrameSource(
            SpriteLibraryAsset sourceLibrary,
            UnitSpriteAnimation settingsAsset,
            GenerationReport report)
        {
            var result = new Dictionary<string, Dictionary<int, List<Sprite>>>(StringComparer.OrdinalIgnoreCase);
            IEnumerable<string> categories = sourceLibrary.GetCategoryNames();
            if (categories == null)
                return result;

            foreach (string category in categories)
            {
                if (!TryParseGeneratorDirection(settingsAsset, category, out string action, out int directionIndex))
                    continue;

                List<string> labels = GetSortedLabels(sourceLibrary, category);
                var sprites = new List<Sprite>(labels.Count);
                for (int i = 0; i < labels.Count; i++)
                {
                    string label = labels[i];
                    Sprite sprite = sourceLibrary.GetSprite(category, label);
                    if (sprite == null)
                    {
                        report.AddWarning($"MissingSprite: category={category} label={label}");
                        continue;
                    }

                    sprites.Add(sprite);
                }

                if (sprites.Count == 0)
                {
                    report.AddWarning($"EmptyCategory: category={category}");
                    continue;
                }

                if (!result.TryGetValue(action, out Dictionary<int, List<Sprite>> byDirection))
                {
                    byDirection = new Dictionary<int, List<Sprite>>();
                    result[action] = byDirection;
                }

                byDirection[directionIndex] = sprites;
            }

            return result;
        }

        private static List<string> GetSortedLabels(SpriteLibraryAsset sourceLibrary, string category)
        {
            IEnumerable<string> categoryLabels = sourceLibrary.GetCategoryLabelNames(category);
            var labels = new List<string>();
            if (categoryLabels == null)
                return labels;

            foreach (string label in categoryLabels)
                labels.Add(label);

            labels.Sort(CompareLabels);
            return labels;
        }

        private static int CompareLabels(string left, string right)
        {
            int leftOrder = ParseLabelOrder(left);
            int rightOrder = ParseLabelOrder(right);
            int orderComparison = leftOrder.CompareTo(rightOrder);
            if (orderComparison != 0)
                return orderComparison;

            return string.CompareOrdinal(left, right);
        }

        private static List<UnitAnimationClip> BuildClips(
            Dictionary<string, Dictionary<int, List<Sprite>>> frameSource,
            AnimationMappingProfile profile,
            UnitSpriteAnimation settingsAsset,
            GenerationReport report)
        {
            var clips = new List<UnitAnimationClip>();
            List<int> generatedDirections = CollectGeneratedDirectionIndices(settingsAsset);

            for (int typeIndex = 0; typeIndex < AnimationTypeNames.Length; typeIndex++)
            {
                string typeName = AnimationTypeNames[typeIndex];
                string sourceAction = profile.GetSourceAction(typeName);
                if (string.IsNullOrEmpty(sourceAction))
                {
                    report.AddWarning($"MissingMapping: type={typeName}");
                    continue;
                }

                if (!frameSource.TryGetValue(sourceAction, out Dictionary<int, List<Sprite>> byDirection))
                {
                    report.AddWarning($"MissingAction: type={typeName} action={sourceAction}");
                    continue;
                }

                for (int directionListIndex = 0; directionListIndex < generatedDirections.Count; directionListIndex++)
                {
                    int directionIndex = generatedDirections[directionListIndex];
                    string directionName = GetDirectionLabel(settingsAsset, directionIndex);

                    if (!byDirection.TryGetValue(directionIndex, out List<Sprite> sourceFrames) || sourceFrames == null || sourceFrames.Count == 0)
                    {
                        report.AddWarning(
                            $"MissingDirection: type={typeName} action={sourceAction} direction={directionName}");
                        continue;
                    }

                    List<Sprite> resolvedFrames = ResolveFrames(sourceFrames, sourceAction, profile);
                    if (resolvedFrames.Count == 0)
                    {
                        report.AddWarning(
                            $"EmptyFramesAfterSlice: type={typeName} action={sourceAction} direction={directionName}");
                        continue;
                    }

                    string clipName = settingsAsset.BuildClipName(typeName, directionIndex);
                    if (string.IsNullOrEmpty(clipName))
                    {
                        report.AddWarning(
                            $"InvalidDirectionConfig: type={typeName} directionIndex={directionIndex}");
                        continue;
                    }

                    var clip = new UnitAnimationClip(
                        clipName,
                        resolvedFrames.ToArray(),
                        IsLoopAnimationType(typeName),
                        DefaultDuration,
                        CreateFrames(typeName, resolvedFrames.Count));

                    clips.Add(clip);
                }
            }

            return clips;
        }

        private static List<int> CollectGeneratedDirectionIndices(UnitSpriteAnimation settingsAsset)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            IReadOnlyList<GeneratorDirectionEntry> entries = settingsAsset.GeneratorDirectionEntries;

            for (int i = 0; i < entries.Count; i++)
            {
                GeneratorDirectionEntry entry = entries[i];
                if (string.IsNullOrEmpty(entry.Suffix))
                    continue;

                if (!seen.Add(entry.DirectionIndex))
                    continue;

                result.Add(entry.DirectionIndex);
            }

            return result;
        }

        private static List<Sprite> ResolveFrames(
            List<Sprite> sourceFrames,
            string sourceAction,
            AnimationMappingProfile profile)
        {
            if (sourceFrames == null || sourceFrames.Count == 0)
                return new List<Sprite>();

            if (!profile.TryGetFrameSlice(sourceAction, out FrameSlice slice))
                return new List<Sprite>(sourceFrames);

            int start = Mathf.Clamp(slice.StartInclusive, 0, sourceFrames.Count - 1);
            int end = Mathf.Clamp(slice.EndInclusive, 0, sourceFrames.Count - 1);
            if (end < start)
                return new List<Sprite>();

            var resolved = new List<Sprite>(end - start + 1);
            for (int i = start; i <= end; i++)
                resolved.Add(sourceFrames[i]);

            return resolved;
        }

        private static UnitAnimationFrame[] CreateFrames(string typeName, int frameCount)
        {
            var frames = new UnitAnimationFrame[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                frames[i] = new UnitAnimationFrame(
                    DefaultDuration,
                    false,
                    Array.Empty<FrameEvent>());
            }

            if (IsHitAnimationType(typeName) && frameCount > 0)
            {
                int hitIndex = frameCount / 2;
                frames[hitIndex] = new UnitAnimationFrame(
                    DefaultDuration,
                    false,
                    new[]
                    {
                        new FrameEvent(FrameEventType.Hit, string.Empty)
                    });
            }

            return frames;
        }

        private static void CreateAsset(
            string outputAssetPath,
            List<UnitAnimationClip> clips,
            UnitSpriteAnimation settingsAsset)
        {
            var asset = ScriptableObject.CreateInstance<UnitSpriteAnimation>();
            UnitSpriteAnimationAutoGeneratorAssetWriter.ApplySettingsToAsset(asset, settingsAsset);
            UnitSpriteAnimationAutoGeneratorAssetWriter.ApplyClipsToAsset(asset, clips);
            AssetDatabase.CreateAsset(asset, outputAssetPath);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void UpdateAsset(
            UnitSpriteAnimation asset,
            List<UnitAnimationClip> clips,
            UnitSpriteAnimation settingsAsset)
        {
            UnitSpriteAnimationAutoGeneratorAssetWriter.ApplySettingsToAsset(asset, settingsAsset);
            UnitSpriteAnimationAutoGeneratorAssetWriter.ApplyClipsToAsset(asset, clips);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static int ParseLabelOrder(string label)
        {
            return int.TryParse(label, out int value) ? value : int.MaxValue;
        }

        private static bool IsLoopAnimationType(string typeName)
        {
            return string.Equals(typeName, "Idle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Move", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsHitAnimationType(string typeName)
        {
            return string.Equals(typeName, "Attack", StringComparison.OrdinalIgnoreCase)
                || string.Equals(typeName, "Skill", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetDirectionLabel(UnitSpriteAnimation settingsAsset, int directionIndex)
        {
            return settingsAsset.TryGetDirectionName(directionIndex, out string directionName)
                ? directionName
                : directionIndex.ToString();
        }

        private static bool TryParseGeneratorDirection(
            UnitSpriteAnimation settingsAsset,
            string categoryName,
            out string actionName,
            out int directionIndex)
        {
            actionName = string.Empty;
            directionIndex = GetFallbackDirectionIndex(settingsAsset);

            string normalizedCategoryName = UnitSpriteAnimationGeneratorPathUtility.NormalizeName(categoryName);
            if (string.IsNullOrEmpty(normalizedCategoryName))
                return false;

            IReadOnlyList<GeneratorDirectionEntry> generatorDirections = settingsAsset.GeneratorDirectionEntries;
            var sortedEntries = new List<SortedGeneratorDirectionEntry>(generatorDirections.Count);
            for (int i = 0; i < generatorDirections.Count; i++)
            {
                GeneratorDirectionEntry entry = generatorDirections[i];
                string suffix = UnitSpriteAnimationGeneratorPathUtility.NormalizeName(entry.Suffix);
                if (string.IsNullOrEmpty(suffix))
                    continue;

                sortedEntries.Add(new SortedGeneratorDirectionEntry(entry, suffix));
            }

            sortedEntries.Sort(SortedGeneratorDirectionEntryComparer.Instance);
            for (int i = 0; i < sortedEntries.Count; i++)
            {
                SortedGeneratorDirectionEntry sortedEntry = sortedEntries[i];
                if (!normalizedCategoryName.EndsWith(sortedEntry.Suffix, StringComparison.OrdinalIgnoreCase))
                    continue;

                int actionLength = normalizedCategoryName.Length - sortedEntry.Suffix.Length;
                if (actionLength <= 0)
                    continue;

                actionName = UnitSpriteAnimationGeneratorPathUtility.NormalizeName(
                    normalizedCategoryName.Substring(0, actionLength));
                if (string.IsNullOrEmpty(actionName))
                    continue;

                directionIndex = sortedEntry.Entry.DirectionIndex;
                return true;
            }

            return false;
        }

        private static int GetFallbackDirectionIndex(UnitSpriteAnimation settingsAsset)
        {
            IReadOnlyList<DirectionEntry> directions = settingsAsset.DirectionEntries;
            return directions.Count > 0 ? directions[0].Index : 0;
        }

        private readonly struct SortedGeneratorDirectionEntry
        {
            public readonly GeneratorDirectionEntry Entry;
            public readonly string Suffix;

            public SortedGeneratorDirectionEntry(GeneratorDirectionEntry entry, string suffix)
            {
                Entry = entry;
                Suffix = suffix;
            }
        }

        private sealed class SortedGeneratorDirectionEntryComparer : IComparer<SortedGeneratorDirectionEntry>
        {
            public static readonly SortedGeneratorDirectionEntryComparer Instance = new SortedGeneratorDirectionEntryComparer();

            public int Compare(SortedGeneratorDirectionEntry left, SortedGeneratorDirectionEntry right)
            {
                int lengthComparison = right.Suffix.Length.CompareTo(left.Suffix.Length);
                if (lengthComparison != 0)
                    return lengthComparison;

                return string.Compare(left.Suffix, right.Suffix, StringComparison.OrdinalIgnoreCase);
            }
        }

    }
}
