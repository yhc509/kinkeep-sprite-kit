using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KinKeep.SpriteKit.Editor
{
    public static class UnitSpriteAnimationGeneratorPathUtility
    {
        public static string NormalizeFolderPath(string folderPath)
        {
            string normalized = string.IsNullOrWhiteSpace(folderPath)
                ? UnitSpriteAnimationAutoGenerator.DefaultOutputFolder
                : folderPath.Trim();
            normalized = normalized.Replace("\\", "/");
            return normalized.TrimEnd('/');
        }

        public static string NormalizeName(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        public static bool EnsureOutputFolder(string folderPath, GenerationReport report)
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
            {
                return true;
            }

            string relativeFolder = folderPath == "Assets"
                ? string.Empty
                : folderPath.Substring("Assets/".Length);
            string absoluteFolder = Path.Combine(Application.dataPath, relativeFolder);
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
}
