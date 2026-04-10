using System;
using System.Collections.Generic;
using KinKeep.SpriteKit;
using UnityEditor;

namespace KinKeep.SpriteKit.Editor
{
    internal static class UnitSpriteAnimationAutoGeneratorAssetWriter
    {
        public static void ApplySettingsToAsset(UnitSpriteAnimation asset, UnitSpriteAnimation settingsAsset)
        {
            if (asset == null || settingsAsset == null)
            {
                return;
            }

            SerializedObject serializedObject = new SerializedObject(asset);
            ApplyDirectionEntries(serializedObject.FindProperty("_directionEntries"), settingsAsset.DirectionEntries);
            ApplyFlipEntries(serializedObject.FindProperty("_flipEntries"), settingsAsset.FlipEntries);
            ApplyGeneratorDirectionEntries(
                serializedObject.FindProperty("_generatorDirectionEntries"),
                settingsAsset.GeneratorDirectionEntries);
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ApplyClipsToAsset(UnitSpriteAnimation asset, List<UnitAnimationClip> clips)
        {
            SerializedObject serializedObject = new SerializedObject(asset);
            SerializedProperty clipsProperty = serializedObject.FindProperty("_clips");
            clipsProperty.arraySize = clips.Count;

            for (int clipIndex = 0; clipIndex < clips.Count; clipIndex++)
            {
                UnitAnimationClip clip = clips[clipIndex];
                SerializedProperty clipProperty = clipsProperty.GetArrayElementAtIndex(clipIndex);

                clipProperty.FindPropertyRelative("_name").stringValue = clip.Name;
                clipProperty.FindPropertyRelative("_loop").boolValue = clip.Loop;
                clipProperty.FindPropertyRelative("_defaultDuration").floatValue = clip.DefaultDuration;

                SerializedProperty spritesProperty = clipProperty.FindPropertyRelative("_sprites");
                spritesProperty.arraySize = clip.Sprites.Length;
                for (int i = 0; i < clip.Sprites.Length; i++)
                {
                    spritesProperty.GetArrayElementAtIndex(i).objectReferenceValue = clip.Sprites[i];
                }

                SerializedProperty framesProperty = clipProperty.FindPropertyRelative("_frames");
                framesProperty.arraySize = clip.Frames.Length;
                for (int frameIndex = 0; frameIndex < clip.Frames.Length; frameIndex++)
                {
                    UnitAnimationFrame frame = clip.Frames[frameIndex];
                    SerializedProperty frameProperty = framesProperty.GetArrayElementAtIndex(frameIndex);
                    frameProperty.FindPropertyRelative("_duration").floatValue = frame.Duration;
                    frameProperty.FindPropertyRelative("_flipX").boolValue = frame.FlipX;

                    FrameEvent[] events = frame.Events ?? Array.Empty<FrameEvent>();
                    SerializedProperty eventsProperty = frameProperty.FindPropertyRelative("_events");
                    eventsProperty.arraySize = events.Length;
                    for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
                    {
                        SerializedProperty eventProperty = eventsProperty.GetArrayElementAtIndex(eventIndex);
                        eventProperty.FindPropertyRelative("_type").enumValueIndex = (int)events[eventIndex].Type;
                        eventProperty.FindPropertyRelative("_param").stringValue = events[eventIndex].Param ?? string.Empty;
                    }
                }
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            asset.SyncClipFrames();
        }

        private static void ApplyDirectionEntries(
            SerializedProperty property,
            IReadOnlyList<DirectionEntry> directionEntries)
        {
            property.arraySize = directionEntries.Count;
            for (int i = 0; i < directionEntries.Count; i++)
            {
                DirectionEntry directionEntry = directionEntries[i];
                SerializedProperty entryProperty = property.GetArrayElementAtIndex(i);
                entryProperty.FindPropertyRelative("_index").intValue = directionEntry.Index;
                entryProperty.FindPropertyRelative("_name").stringValue = directionEntry.Name ?? string.Empty;
            }
        }

        private static void ApplyFlipEntries(
            SerializedProperty property,
            IReadOnlyList<FlipEntry> flipEntries)
        {
            property.arraySize = flipEntries.Count;
            for (int i = 0; i < flipEntries.Count; i++)
            {
                FlipEntry flipEntry = flipEntries[i];
                SerializedProperty entryProperty = property.GetArrayElementAtIndex(i);
                entryProperty.FindPropertyRelative("_sourceDirection").intValue = flipEntry.SourceDirection;
                entryProperty.FindPropertyRelative("_targetDirection").intValue = flipEntry.TargetDirection;
            }
        }

        private static void ApplyGeneratorDirectionEntries(
            SerializedProperty property,
            IReadOnlyList<GeneratorDirectionEntry> generatorEntries)
        {
            property.arraySize = generatorEntries.Count;
            for (int i = 0; i < generatorEntries.Count; i++)
            {
                GeneratorDirectionEntry generatorEntry = generatorEntries[i];
                SerializedProperty entryProperty = property.GetArrayElementAtIndex(i);
                entryProperty.FindPropertyRelative("_suffix").stringValue = generatorEntry.Suffix ?? string.Empty;
                entryProperty.FindPropertyRelative("_directionIndex").intValue = generatorEntry.DirectionIndex;
            }
        }
    }
}
