using System;
using System.Collections.Generic;

namespace KinKeep.SpriteKit.Editor
{
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
            foreach (KeyValuePair<string, string> pair in _sourceActions)
                clone._sourceActions[pair.Key] = pair.Value;

            foreach (KeyValuePair<string, FrameSlice> pair in _frameSlices)
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
}
