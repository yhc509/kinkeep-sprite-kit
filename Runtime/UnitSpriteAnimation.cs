using System;
using System.Collections.Generic;
using UnityEngine;

namespace KinKeep.SpriteKit
{
    public enum FrameEventType
    {
        Sound,
        Hit,
        Skill
    }

    [Serializable]
    public struct FrameEvent
    {
        public FrameEventType Type;
        public string Param;
    }

    [Serializable]
    public class UnitAnimationFrame
    {
        public float Duration = 0.1f;
        public bool FlipX;
        public FrameEvent[] Events;
    }

    [Serializable]
    public class UnitAnimationClip
    {
        public string Name;
        public Sprite[] Sprites;
        public bool Loop;
        public float DefaultDuration = 0.1f;
        public UnitAnimationFrame[] Frames;

        public bool SyncFrames()
        {
            int spriteCount = Sprites?.Length ?? 0;
            if (spriteCount == 0)
            {
                if (Frames == null)
                    return false;

                Frames = null;
                return true;
            }

            bool isDirty = Frames == null || Frames.Length != spriteCount;
            UnitAnimationFrame[] nextFrames = isDirty
                ? new UnitAnimationFrame[spriteCount]
                : Frames;

            for (int i = 0; i < spriteCount; i++)
            {
                UnitAnimationFrame frame = Frames != null && i < Frames.Length
                    ? Frames[i]
                    : null;

                if (frame == null)
                {
                    frame = new UnitAnimationFrame
                    {
                        Duration = DefaultDuration,
                        FlipX = false,
                        Events = Array.Empty<FrameEvent>()
                    };
                    isDirty = true;
                }

                nextFrames[i] = frame;
            }

            if (isDirty)
                Frames = nextFrames;

            return isDirty;
        }

        public float GetFrameDuration(int index)
        {
            if (Frames == null || index < 0 || index >= Frames.Length)
                return DefaultDuration;

            UnitAnimationFrame frame = Frames[index];
            if (frame == null)
                return DefaultDuration;

            float duration = frame.Duration;
            return duration > 0f ? duration : DefaultDuration;
        }

        public float GetTotalDuration()
        {
            int count = Sprites?.Length ?? 0;
            if (count == 0)
                return 0f;

            float total = 0f;
            for (int i = 0; i < count; i++)
                total += GetFrameDuration(i);

            return total;
        }
    }

    [CreateAssetMenu(fileName = "UnitSpriteAnimation", menuName = "ScriptableObjects/UnitSpriteAnimation")]
    public class UnitSpriteAnimation : ScriptableObject
    {
        private static readonly DirectionEntry[] DefaultDirectionEntries =
        {
            new DirectionEntry(0, "None"),
            new DirectionEntry(1, "Up"),
            new DirectionEntry(2, "Down"),
            new DirectionEntry(3, "Left"),
            new DirectionEntry(4, "Right")
        };

        private static readonly FlipEntry[] DefaultFlipEntries =
        {
            new FlipEntry(3, 4)
        };

        private static readonly GeneratorDirectionEntry[] DefaultGeneratorDirectionEntries =
        {
            new GeneratorDirectionEntry("B", 1),
            new GeneratorDirectionEntry("F", 2),
            new GeneratorDirectionEntry("S", 4)
        };

        [SerializeField] private DirectionEntry[] _directionEntries =
        {
            new DirectionEntry(0, "None"),
            new DirectionEntry(1, "Up"),
            new DirectionEntry(2, "Down"),
            new DirectionEntry(3, "Left"),
            new DirectionEntry(4, "Right")
        };

        [SerializeField] private FlipEntry[] _flipEntries =
        {
            new FlipEntry(3, 4)
        };

        [SerializeField] private GeneratorDirectionEntry[] _generatorDirectionEntries =
        {
            new GeneratorDirectionEntry("B", 1),
            new GeneratorDirectionEntry("F", 2),
            new GeneratorDirectionEntry("S", 4)
        };

        [SerializeField] private UnitAnimationClip[] _clips;

        public static IReadOnlyList<DirectionEntry> DefaultDirections => DefaultDirectionEntries;
        public static IReadOnlyList<FlipEntry> DefaultFlipMappings => DefaultFlipEntries;
        public static IReadOnlyList<GeneratorDirectionEntry> DefaultGeneratorDirections => DefaultGeneratorDirectionEntries;

        public IReadOnlyList<DirectionEntry> DirectionEntries => GetDirectionEntries();
        public IReadOnlyList<FlipEntry> FlipEntries => GetFlipEntries();
        public IReadOnlyList<GeneratorDirectionEntry> GeneratorDirectionEntries => GetGeneratorDirectionEntries();
        public UnitAnimationClip[] Clips => _clips;

        public bool SyncClipFrames()
        {
            if (_clips == null)
                return false;

            bool isDirty = false;
            for (int i = 0; i < _clips.Length; i++)
            {
                UnitAnimationClip clip = _clips[i];
                if (clip != null && clip.SyncFrames())
                    isDirty = true;
            }

            return isDirty;
        }

        public bool TryGetClip(string name, out UnitAnimationClip clip)
        {
            clip = null;
            if (_clips == null || string.IsNullOrWhiteSpace(name))
                return false;

            for (int i = 0; i < _clips.Length; i++)
            {
                UnitAnimationClip candidate = _clips[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    clip = candidate;
                    return true;
                }
            }

            return false;
        }

        public UnitAnimationClip GetClip(string name)
        {
            if (_clips == null)
            {
                Debug.LogError("[UnitSpriteAnimation] Animation clip array is empty.", this);
                return null;
            }

            if (TryGetClip(name, out UnitAnimationClip clip))
                return clip;

            Debug.LogError($"[UnitSpriteAnimation] Animation clip not found. name={name}", this);
            return null;
        }

        public bool TryGetDirectionalClip(string typeName, int directionIndex, out UnitAnimationClip clip, out bool isFlipX)
        {
            clip = null;
            isFlipX = false;

            if (string.IsNullOrWhiteSpace(typeName))
                return false;

            string directClipName = BuildClipName(typeName, directionIndex);
            if (!string.IsNullOrEmpty(directClipName) && TryGetPlayableClip(directClipName, out clip))
                return true;

            if (!TryGetFlipTargetDirection(directionIndex, out int targetDirectionIndex))
                return false;

            string flippedClipName = BuildClipName(typeName, targetDirectionIndex);
            if (string.IsNullOrEmpty(flippedClipName))
                return false;

            if (!TryGetPlayableClip(flippedClipName, out clip))
                return false;

            isFlipX = true;
            return true;
        }

        public UnitAnimationClip GetClip(string typeName, int directionIndex)
        {
            return TryGetDirectionalClip(typeName, directionIndex, out UnitAnimationClip clip, out _)
                ? clip
                : null;
        }

        public float GetClipDuration(string name)
        {
            return TryGetClip(name, out UnitAnimationClip clip)
                ? clip.GetTotalDuration()
                : 0f;
        }

        public float GetClipDuration(string typeName, int directionIndex)
        {
            return TryGetDirectionalClip(typeName, directionIndex, out UnitAnimationClip clip, out _)
                ? clip.GetTotalDuration()
                : 0f;
        }

        public bool TryGetDirectionName(int directionIndex, out string directionName)
        {
            IReadOnlyList<DirectionEntry> directions = GetDirectionEntries();
            for (int i = 0; i < directions.Count; i++)
            {
                DirectionEntry direction = directions[i];
                if (direction.Index != directionIndex)
                    continue;

                string normalizedDirectionName = NormalizeName(direction.Name);
                if (string.IsNullOrEmpty(normalizedDirectionName))
                    continue;

                directionName = normalizedDirectionName;
                return true;
            }

            directionName = string.Empty;
            return false;
        }

        public bool TryGetDirectionIndex(string directionName, out int directionIndex)
        {
            string normalizedDirectionName = NormalizeName(directionName);
            IReadOnlyList<DirectionEntry> directions = GetDirectionEntries();
            for (int i = 0; i < directions.Count; i++)
            {
                DirectionEntry direction = directions[i];
                if (!string.Equals(
                        NormalizeName(direction.Name),
                        normalizedDirectionName,
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                directionIndex = direction.Index;
                return true;
            }

            directionIndex = GetFallbackDirectionIndex();
            return false;
        }

        public string BuildClipName(string typeName, int directionIndex)
        {
            string normalizedTypeName = NormalizeName(typeName);
            if (string.IsNullOrEmpty(normalizedTypeName))
                return string.Empty;

            if (!TryGetDirectionName(directionIndex, out string directionName))
                return string.Empty;

            if (IsNoDirectionName(directionName))
                return normalizedTypeName;

            return $"{normalizedTypeName}_{directionName}";
        }

        public bool TryParseClipName(string clipName, out string typeName, out int directionIndex)
        {
            typeName = string.Empty;
            directionIndex = GetFallbackDirectionIndex();

            string normalizedClipName = NormalizeName(clipName);
            if (string.IsNullOrEmpty(normalizedClipName))
                return false;

            int separatorIndex = normalizedClipName.IndexOf('_');
            if (separatorIndex < 0)
            {
                typeName = normalizedClipName;
                return true;
            }

            typeName = NormalizeName(normalizedClipName.Substring(0, separatorIndex));
            if (string.IsNullOrEmpty(typeName))
                return false;

            string directionName = NormalizeName(normalizedClipName.Substring(separatorIndex + 1));
            if (string.IsNullOrEmpty(directionName))
                return true;

            TryGetDirectionIndex(directionName, out directionIndex);
            return true;
        }

        public bool TryParseGeneratorDirection(string categoryName, out string actionName, out int directionIndex)
        {
            actionName = string.Empty;
            directionIndex = GetFallbackDirectionIndex();

            string normalizedCategoryName = NormalizeName(categoryName);
            if (string.IsNullOrEmpty(normalizedCategoryName))
                return false;

            IReadOnlyList<GeneratorDirectionEntry> generatorDirections = GetGeneratorDirectionEntries();
            var sortedEntries = new List<SortedGeneratorDirectionEntry>(generatorDirections.Count);
            for (int i = 0; i < generatorDirections.Count; i++)
            {
                GeneratorDirectionEntry entry = generatorDirections[i];
                string suffix = NormalizeName(entry.Suffix);
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

                actionName = NormalizeName(normalizedCategoryName.Substring(0, actionLength));
                if (string.IsNullOrEmpty(actionName))
                    continue;

                directionIndex = sortedEntry.Entry.DirectionIndex;
                return true;
            }

            return false;
        }

        public bool TryGetFlipTargetDirection(int directionIndex, out int targetDirectionIndex)
        {
            IReadOnlyList<FlipEntry> flipEntries = GetFlipEntries();
            for (int i = 0; i < flipEntries.Count; i++)
            {
                FlipEntry flipEntry = flipEntries[i];
                if (flipEntry.SourceDirection != directionIndex)
                    continue;

                targetDirectionIndex = flipEntry.TargetDirection;
                return true;
            }

            targetDirectionIndex = directionIndex;
            return false;
        }

        private IReadOnlyList<DirectionEntry> GetDirectionEntries()
        {
            if (_directionEntries != null && _directionEntries.Length > 0)
                return _directionEntries;

            return DefaultDirectionEntries;
        }

        private IReadOnlyList<FlipEntry> GetFlipEntries()
        {
            if (_flipEntries != null && _flipEntries.Length > 0)
                return _flipEntries;

            return DefaultFlipEntries;
        }

        private IReadOnlyList<GeneratorDirectionEntry> GetGeneratorDirectionEntries()
        {
            if (_generatorDirectionEntries != null && _generatorDirectionEntries.Length > 0)
                return _generatorDirectionEntries;

            return DefaultGeneratorDirectionEntries;
        }

        private int GetFallbackDirectionIndex()
        {
            IReadOnlyList<DirectionEntry> directions = GetDirectionEntries();
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

        private static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static bool IsNoDirectionName(string directionName)
        {
            return string.IsNullOrEmpty(directionName)
                || string.Equals(directionName, "None", StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasSprites(UnitAnimationClip clip)
        {
            return clip != null && clip.Sprites != null && clip.Sprites.Length > 0;
        }

        private bool TryGetPlayableClip(string name, out UnitAnimationClip clip)
        {
            if (TryGetClip(name, out clip) && HasSprites(clip))
                return true;

            clip = null;
            return false;
        }
    }
}
