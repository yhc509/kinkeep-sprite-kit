using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KinKeep.SpriteKit.Editor
{
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
                for (int i = 0; i < _warnings.Count; i++)
                    builder.AppendLine($"- {_warnings[i]}");
            }

            if (_errors.Count > 0)
            {
                builder.AppendLine("Errors:");
                for (int i = 0; i < _errors.Count; i++)
                    builder.AppendLine($"- {_errors[i]}");
            }

            return builder.ToString().TrimEnd();
        }

        public void LogToConsole()
        {
            for (int i = 0; i < _warnings.Count; i++)
                Debug.LogWarning($"[UnitSpriteAnimationAutoGenerator] {_warnings[i]}");

            if (_errors.Count > 0)
            {
                for (int i = 0; i < _errors.Count; i++)
                    Debug.LogError($"[UnitSpriteAnimationAutoGenerator] {_errors[i]}");
                return;
            }

            Debug.Log($"[UnitSpriteAnimationAutoGenerator]\n{BuildSummary()}");
        }
    }
}
