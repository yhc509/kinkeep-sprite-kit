using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

public readonly struct FrameSlice
{
    public int StartInclusive { get; }
    public int EndInclusive { get; }

    public FrameSlice(int startInclusive, int endInclusive)
    {
        StartInclusive = startInclusive;
        EndInclusive = endInclusive;
    }
}

public sealed class AnimationMappingProfile
{
    private readonly Dictionary<string, string> _sourceActions;
    private readonly Dictionary<string, FrameSlice> _frameSlices;

    public AnimationMappingProfile()
    {
        _sourceActions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _frameSlices = new Dictionary<string, FrameSlice>(StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyDictionary<string, string> SourceActions => _sourceActions;

    public static AnimationMappingProfile CreateDefault()
    {
        var profile = new AnimationMappingProfile();

        profile.SetSourceAction("Idle", "Idle");
        profile.SetSourceAction("Move", "Run");
        profile.SetSourceAction("Attack", "Slash");
        profile.SetSourceAction("Skill", "Cast");
        profile.SetSourceAction("Damaged", "Block");
        profile.SetSourceAction("Die", "Death");
        profile.SetSourceAction("Concentrate", "Cast");
        profile.SetSourceAction("Debuff", "Cast");
        profile.SetSourceAction("Buff", "Cast");

        profile.SetFrameSlice("Jab", new FrameSlice(0, 2));
        profile.SetFrameSlice("Cast", new FrameSlice(0, 3));
        return profile;
    }

    public AnimationMappingProfile Clone()
    {
        var clone = new AnimationMappingProfile();
        foreach (var pair in _sourceActions)
            clone._sourceActions[pair.Key] = pair.Value;
        foreach (var pair in _frameSlices)
            clone._frameSlices[pair.Key] = pair.Value;
        return clone;
    }

    public string GetSourceAction(string typeName)
    {
        string normalizedType = NormalizeName(typeName);
        return _sourceActions.TryGetValue(normalizedType, out string action) ? action : string.Empty;
    }

    public void SetSourceAction(string typeName, string sourceAction)
    {
        string normalizedType = NormalizeName(typeName);
        if (string.IsNullOrEmpty(normalizedType))
            return;

        _sourceActions[normalizedType] = NormalizeName(sourceAction);
    }

    public void SetFrameSlice(string sourceAction, FrameSlice slice)
    {
        string normalizedAction = NormalizeName(sourceAction);
        if (string.IsNullOrEmpty(normalizedAction))
            return;

        _frameSlices[normalizedAction] = slice;
    }

    public bool TryGetFrameSlice(string sourceAction, out FrameSlice slice)
    {
        string normalizedAction = NormalizeName(sourceAction);
        if (string.IsNullOrEmpty(normalizedAction))
        {
            slice = default;
            return false;
        }

        return _frameSlices.TryGetValue(normalizedAction, out slice);
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public sealed class GenerationReport
{
    private readonly List<string> _warnings = new List<string>();
    private readonly List<string> _errors = new List<string>();

    public int CreatedCount { get; private set; }
    public int UpdatedCount { get; private set; }
    public int SkippedExistsCount { get; private set; }
    public int GeneratedClipCount { get; internal set; }
    public string CreatedAssetPath { get; private set; }
    public string ResultAssetPath { get; private set; }

    public IReadOnlyList<string> Warnings => _warnings;
    public IReadOnlyList<string> Errors => _errors;
    public bool HasErrors => _errors.Count > 0;

    public void AddWarning(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _warnings.Add(message);
    }

    public void AddError(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
            _errors.Add(message);
    }

    public void MarkCreated(string path)
    {
        CreatedCount++;
        CreatedAssetPath = path;
        ResultAssetPath = path;
    }

    public void MarkUpdated(string path)
    {
        UpdatedCount++;
        ResultAssetPath = path;
    }

    public void MarkSkippedExists(string path)
    {
        SkippedExistsCount++;
        AddWarning($"Skipped(Exists): {path}");
    }

    public string BuildSummary()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Created: {CreatedCount}");
        builder.AppendLine($"Updated: {UpdatedCount}");
        builder.AppendLine($"Skipped(Exists): {SkippedExistsCount}");
        builder.AppendLine($"Generated Clips: {GeneratedClipCount}");

        if (_warnings.Count > 0)
        {
            builder.AppendLine("Warnings:");
            foreach (string warning in _warnings)
                builder.AppendLine($"- {warning}");
        }

        if (_errors.Count > 0)
        {
            builder.AppendLine("Errors:");
            foreach (string error in _errors)
                builder.AppendLine($"- {error}");
        }

        return builder.ToString().TrimEnd();
    }

    public void LogToConsole()
    {
        if (_warnings.Count > 0)
        {
            foreach (string warning in _warnings)
                Debug.LogWarning($"[UnitSpriteAnimationAutoGenerator] {warning}");
        }

        if (_errors.Count > 0)
        {
            foreach (string error in _errors)
                Debug.LogError($"[UnitSpriteAnimationAutoGenerator] {error}");
        }
        else
        {
            Debug.Log($"[UnitSpriteAnimationAutoGenerator]\n{BuildSummary()}");
        }
    }
}

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

    private static readonly string[] OutputDirectionNames =
    {
        "Up",
        "Down",
        "Right"
    };

    public static AnimationMappingProfile CreateDefaultProfile()
    {
        return AnimationMappingProfile.CreateDefault();
    }

    public static GenerationReport Generate(
        SpriteLibraryAsset sourceLibrary,
        string outputFolder,
        string outputName,
        AnimationMappingProfile mappingProfile)
    {
        var report = new GenerationReport();

        if (sourceLibrary == null)
        {
            report.AddError("Source SpriteLibraryAsset is null.");
            report.LogToConsole();
            return report;
        }

        string normalizedOutputName = NormalizeName(outputName);
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

        string normalizedOutputFolder = NormalizeFolderPath(outputFolder);
        if (!EnsureOutputFolder(normalizedOutputFolder, report))
        {
            report.LogToConsole();
            return report;
        }

        string outputAssetPath = $"{normalizedOutputFolder}/{normalizedOutputName}.asset";
        UnitSpriteAnimation existingAsset = AssetDatabase.LoadAssetAtPath<UnitSpriteAnimation>(outputAssetPath);

        AnimationMappingProfile profile = (mappingProfile ?? CreateDefaultProfile()).Clone();
        var frameSource = BuildFrameSource(sourceLibrary, report);
        List<UnitAnimationClip> clips = BuildClips(frameSource, profile, report);
        report.GeneratedClipCount = clips.Count;

        if (clips.Count == 0)
        {
            report.AddError("No clips were generated.");
            report.LogToConsole();
            return report;
        }

        if (existingAsset != null)
        {
            UpdateAsset(existingAsset, clips);
            report.MarkUpdated(outputAssetPath);
        }
        else
        {
            CreateAsset(outputAssetPath, clips);
            report.MarkCreated(outputAssetPath);
        }

        report.LogToConsole();
        return report;
    }

    private static Dictionary<string, Dictionary<string, List<Sprite>>> BuildFrameSource(
        SpriteLibraryAsset sourceLibrary,
        GenerationReport report)
    {
        var result = new Dictionary<string, Dictionary<string, List<Sprite>>>(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> categories = sourceLibrary.GetCategoryNames();
        if (categories == null)
            return result;

        foreach (string category in categories)
        {
            if (!TryParseCategory(category, out string action, out string directionName))
                continue;

            IEnumerable<string> categoryLabels = sourceLibrary.GetCategoryLabelNames(category) ?? Array.Empty<string>();
            List<string> labels = categoryLabels
                .OrderBy(ParseLabelOrder)
                .ThenBy(l => l, StringComparer.Ordinal)
                .ToList();

            var sprites = new List<Sprite>(labels.Count);
            foreach (string label in labels)
            {
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

            if (!result.TryGetValue(action, out Dictionary<string, List<Sprite>> byDirection))
            {
                byDirection = new Dictionary<string, List<Sprite>>(StringComparer.OrdinalIgnoreCase);
                result[action] = byDirection;
            }

            byDirection[directionName] = sprites;
        }

        return result;
    }

    private static List<UnitAnimationClip> BuildClips(
        Dictionary<string, Dictionary<string, List<Sprite>>> frameSource,
        AnimationMappingProfile profile,
        GenerationReport report)
    {
        var clips = new List<UnitAnimationClip>();

        foreach (string typeName in AnimationTypeNames)
        {
            string sourceAction = profile.GetSourceAction(typeName);
            if (string.IsNullOrEmpty(sourceAction))
            {
                report.AddWarning($"MissingMapping: type={typeName}");
                continue;
            }

            if (!frameSource.TryGetValue(sourceAction, out Dictionary<string, List<Sprite>> byDirection))
            {
                report.AddWarning($"MissingAction: type={typeName} action={sourceAction}");
                continue;
            }

            foreach (string directionName in OutputDirectionNames)
            {
                if (!byDirection.TryGetValue(directionName, out List<Sprite> sourceFrames) || sourceFrames == null || sourceFrames.Count == 0)
                {
                    report.AddWarning($"MissingDirection: type={typeName} action={sourceAction} direction={directionName}");
                    continue;
                }

                List<Sprite> resolvedFrames = ResolveFrames(sourceFrames, sourceAction, profile);
                if (resolvedFrames.Count == 0)
                {
                    report.AddWarning($"EmptyFramesAfterSlice: type={typeName} action={sourceAction} direction={directionName}");
                    continue;
                }

                var clip = new UnitAnimationClip
                {
                    Name = $"{typeName}_{directionName}",
                    Sprites = resolvedFrames.ToArray(),
                    Loop = IsLoopAnimationType(typeName),
                    DefaultDuration = DefaultDuration,
                    Frames = CreateFrames(typeName, resolvedFrames.Count)
                };

                clips.Add(clip);
            }
        }

        return clips;
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
            frames[i] = new UnitAnimationFrame
            {
                Duration = DefaultDuration,
                FlipX = false,
                Events = Array.Empty<FrameEvent>()
            };
        }

        if (IsHitAnimationType(typeName) && frameCount > 0)
        {
            int hitIndex = frameCount / 2;
            frames[hitIndex].Events = new[]
            {
                new FrameEvent { Type = FrameEventType.Hit, Param = string.Empty }
            };
        }

        return frames;
    }

    private static void CreateAsset(string outputAssetPath, List<UnitAnimationClip> clips)
    {
        var asset = ScriptableObject.CreateInstance<UnitSpriteAnimation>();
        ApplyClipsToAsset(asset, clips);
        AssetDatabase.CreateAsset(asset, outputAssetPath);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void UpdateAsset(UnitSpriteAnimation asset, List<UnitAnimationClip> clips)
    {
        ApplyClipsToAsset(asset, clips);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void ApplyClipsToAsset(UnitSpriteAnimation asset, List<UnitAnimationClip> clips)
    {
        var serializedObject = new SerializedObject(asset);
        SerializedProperty clipsProperty = serializedObject.FindProperty("_clips");
        clipsProperty.arraySize = clips.Count;

        for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
        {
            UnitAnimationClip clip = clips[clipIndex];
            SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(clipIndex);

            clipProperty.FindPropertyRelative("Name").stringValue = clip.Name;
            clipProperty.FindPropertyRelative("Loop").boolValue = clip.Loop;
            clipProperty.FindPropertyRelative("DefaultDuration").floatValue = clip.DefaultDuration;

            SerializedProperty spritesProperty = clipProperty.FindPropertyRelative("Sprites");
            spritesProperty.arraySize = clip.Sprites.Length;
            for (int i = 0; i < clip.Sprites.Length; i++)
                spritesProperty.GetArrayElementAtIndex(i).objectReferenceValue = clip.Sprites[i];

            SerializedProperty framesProperty = clipProperty.FindPropertyRelative("Frames");
            framesProperty.arraySize = clip.Frames.Length;
            for (int frameIndex = 0; frameIndex < clip.Frames.Length; frameIndex++)
            {
                UnitAnimationFrame frame = clip.Frames[frameIndex];
                SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(frameIndex);
                frameProperty.FindPropertyRelative("Duration").floatValue = frame.Duration;
                frameProperty.FindPropertyRelative("FlipX").boolValue = frame.FlipX;

                FrameEvent[] events = frame.Events ?? Array.Empty<FrameEvent>();
                SerializedProperty eventsProperty = frameProperty.FindPropertyRelative("Events");
                eventsProperty.arraySize = events.Length;
                for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                {
                    SerializedProperty eventProperty = eventsProperty.GetArrayElementAtIndex(eventIndex);
                    eventProperty.FindPropertyRelative("Type").enumValueIndex = (int)events[eventIndex].Type;
                    eventProperty.FindPropertyRelative("Param").stringValue = events[eventIndex].Param ?? string.Empty;
                }
            }
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool TryParseCategory(string category, out string action, out string directionName)
    {
        action = string.Empty;
        directionName = string.Empty;

        if (string.IsNullOrWhiteSpace(category) || category.Length < 2)
            return false;

        char suffix = category[category.Length - 1];
        directionName = suffix switch
        {
            'B' => "Up",
            'F' => "Down",
            'S' => "Right",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(directionName))
            return false;

        action = NormalizeName(category.Substring(0, category.Length - 1));
        return !string.IsNullOrEmpty(action);
    }

    private static int ParseLabelOrder(string label)
    {
        if (int.TryParse(label, out int value))
            return value;

        return int.MaxValue;
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

    private static string NormalizeFolderPath(string folderPath)
    {
        string normalized = string.IsNullOrWhiteSpace(folderPath) ? DefaultOutputFolder : folderPath.Trim();
        normalized = normalized.Replace("\\", "/");
        return normalized.TrimEnd('/');
    }

    private static string NormalizeName(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static bool EnsureOutputFolder(string folderPath, GenerationReport report)
    {
        if (string.IsNullOrEmpty(folderPath))
        {
            report.AddError("Output folder is empty.");
            return false;
        }

        bool isAssetsRoot = folderPath == "Assets";
        bool isAssetsChild = folderPath.StartsWith("Assets/", StringComparison.Ordinal);
        if (!isAssetsRoot && !isAssetsChild)
        {
            report.AddError($"Output folder must start with 'Assets': {folderPath}");
            return false;
        }

        if (AssetDatabase.IsValidFolder(folderPath))
            return true;

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string relativeFolder = folderPath == "Assets"
            ? string.Empty
            : folderPath.Substring("Assets/".Length);
        string absoluteFolder = Path.Combine(projectRoot, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);
        AssetDatabase.Refresh();

        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            report.AddError($"Failed to create output folder: {folderPath}");
            return false;
        }

        return true;
    }
}
