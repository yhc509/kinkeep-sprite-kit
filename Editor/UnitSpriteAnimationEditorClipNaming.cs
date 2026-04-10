using System.Collections.Generic;
using KinKeep.SpriteKit;
using UnityEngine;

namespace KinKeep.SpriteKit.Editor
{
    public static class UnitSpriteAnimationEditorClipNaming
    {
        public static string[] BuildDirectionLabels(UnitSpriteAnimation animationData)
        {
            IReadOnlyList<DirectionEntry> directions = GetDirections(animationData);
            var labels = new string[directions.Count];
            for (int i = 0; i < directions.Count; i++)
            {
                labels[i] = string.IsNullOrWhiteSpace(directions[i].Name)
                    ? directions[i].Index.ToString()
                    : directions[i].Name;
            }

            return labels;
        }

        public static void ParseClipName(
            UnitSpriteAnimation animationData,
            string clipName,
            out string typeName,
            out int directionPopupIndex)
        {
            typeName = GetTypeName(0);
            directionPopupIndex = 0;

            if (animationData == null)
            {
                return;
            }

            if (!animationData.TryParseClipName(clipName, out string parsedTypeName, out int directionIndex))
            {
                return;
            }

            typeName = NormalizeTypeName(parsedTypeName, typeName);
            directionPopupIndex = DirectionToPopupIndex(animationData, directionIndex);
        }

        public static string BuildClipName(UnitSpriteAnimation animationData, string typeName, int directionPopupIndex)
        {
            string normalizedTypeName = NormalizeTypeName(typeName, GetTypeName(0));
            IReadOnlyList<DirectionEntry> directions = GetDirections(animationData);
            if (directions.Count == 0)
            {
                return normalizedTypeName;
            }

            int safePopupIndex = Mathf.Clamp(directionPopupIndex, 0, directions.Count - 1);
            int directionIndex = directions[safePopupIndex].Index;
            if (animationData != null)
            {
                return animationData.BuildClipName(normalizedTypeName, directionIndex);
            }

            string directionName = directions[safePopupIndex].Name;
            return string.IsNullOrWhiteSpace(directionName)
                || string.Equals(directionName, "None", System.StringComparison.OrdinalIgnoreCase)
                ? normalizedTypeName
                : $"{normalizedTypeName}_{directionName}";
        }

        public static int TypeToIndex(string typeName)
        {
            for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
            {
                if (string.Equals(
                    UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i],
                    typeName,
                    System.StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return 0;
        }

        public static string GetTypeName(int index)
        {
            int safeIndex = Mathf.Clamp(index, 0, UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length - 1);
            return UnitSpriteAnimationAutoGenerator.AnimationTypeNames[safeIndex];
        }

        public static int DirectionToPopupIndex(UnitSpriteAnimation animationData, int directionIndex)
        {
            IReadOnlyList<DirectionEntry> directions = GetDirections(animationData);
            for (int i = 0; i < directions.Count; i++)
            {
                if (directions[i].Index == directionIndex)
                {
                    return i;
                }
            }

            return 0;
        }

        private static string NormalizeTypeName(string typeName, string fallback)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return fallback;
            }

            for (int i = 0; i < UnitSpriteAnimationAutoGenerator.AnimationTypeNames.Length; i++)
            {
                string candidate = UnitSpriteAnimationAutoGenerator.AnimationTypeNames[i];
                if (string.Equals(candidate, typeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }

            return fallback;
        }

        private static IReadOnlyList<DirectionEntry> GetDirections(UnitSpriteAnimation animationData)
        {
            return animationData != null ? animationData.DirectionEntries : UnitSpriteAnimation.DefaultDirections;
        }
    }
}
