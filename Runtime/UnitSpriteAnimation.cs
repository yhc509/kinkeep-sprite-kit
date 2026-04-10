using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("Type")]
        [SerializeField] private FrameEventType _type;
        [FormerlySerializedAs("Param")]
        [SerializeField] private string _param;

        public FrameEventType Type => _type;
        public string Param => _param;

        public FrameEvent(FrameEventType type, string param)
        {
            _type = type;
            _param = param;
        }
    }

    [Serializable]
    public class UnitAnimationFrame
    {
        [FormerlySerializedAs("Duration")]
        [SerializeField] private float _duration = 0.1f;
        [FormerlySerializedAs("FlipX")]
        [SerializeField] private bool _flipX;
        [FormerlySerializedAs("Events")]
        [SerializeField] private FrameEvent[] _events;

        public float Duration => _duration;
        public bool FlipX => _flipX;
        public FrameEvent[] Events => _events;

        public UnitAnimationFrame(float duration, bool flipX, FrameEvent[] events)
        {
            _duration = duration;
            _flipX = flipX;
            _events = events;
        }
    }

    [Serializable]
    public class UnitAnimationClip
    {
        [FormerlySerializedAs("Name")]
        [SerializeField] private string _name;
        [FormerlySerializedAs("Sprites")]
        [SerializeField] private Sprite[] _sprites;
        [FormerlySerializedAs("Loop")]
        [SerializeField] private bool _loop;
        [FormerlySerializedAs("DefaultDuration")]
        [SerializeField] private float _defaultDuration = 0.1f;
        [FormerlySerializedAs("Frames")]
        [SerializeField] private UnitAnimationFrame[] _frames;

        public string Name => _name;
        public Sprite[] Sprites => _sprites;
        public bool Loop => _loop;
        public float DefaultDuration => _defaultDuration;
        public UnitAnimationFrame[] Frames => _frames;

        public UnitAnimationClip(
            string name,
            Sprite[] sprites,
            bool loop,
            float defaultDuration,
            UnitAnimationFrame[] frames)
        {
            _name = name;
            _sprites = sprites;
            _loop = loop;
            _defaultDuration = defaultDuration;
            _frames = frames;
        }

        public bool SyncFrames()
        {
            int spriteCount = _sprites?.Length ?? 0;
            if (spriteCount == 0)
            {
                if (_frames == null)
                    return false;

                _frames = null;
                return true;
            }

            bool isDirty = _frames == null || _frames.Length != spriteCount;
            UnitAnimationFrame[] nextFrames = isDirty
                ? new UnitAnimationFrame[spriteCount]
                : _frames;

            for (int i = 0; i < spriteCount; i++)
            {
                UnitAnimationFrame frame = _frames != null && i < _frames.Length
                    ? _frames[i]
                    : null;

                if (frame == null)
                {
                    frame = new UnitAnimationFrame(
                        _defaultDuration,
                        false,
                        Array.Empty<FrameEvent>());
                    isDirty = true;
                }

                nextFrames[i] = frame;
            }

            if (isDirty)
                _frames = nextFrames;

            return isDirty;
        }

        public float GetFrameDuration(int index)
        {
            if (_frames == null || index < 0 || index >= _frames.Length)
                return _defaultDuration;

            UnitAnimationFrame frame = _frames[index];
            if (frame == null)
                return _defaultDuration;

            float duration = frame.Duration;
            return duration > 0f ? duration : _defaultDuration;
        }

        public float GetTotalDuration()
        {
            int count = _sprites?.Length ?? 0;
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

            for (int i = 0; i < flipEntries.Count; i++)
            {
                FlipEntry flipEntry = flipEntries[i];
                if (flipEntry.TargetDirection != directionIndex)
                    continue;

                targetDirectionIndex = flipEntry.SourceDirection;
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
