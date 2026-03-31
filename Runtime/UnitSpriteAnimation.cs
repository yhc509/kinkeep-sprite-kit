using System;
using UnityEngine;

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

    public void SyncFrames()
    {
        int spriteCount = Sprites?.Length ?? 0;
        
        if (spriteCount == 0)
        {
            Frames = null;
            return;
        }
        
        if (Frames == null || Frames.Length != spriteCount)
        {
            var newFrames = new UnitAnimationFrame[spriteCount];
            
            for (int i = 0; i < spriteCount; i++)
            {
                if (Frames != null && i < Frames.Length)
                    newFrames[i] = Frames[i];
                else
                    newFrames[i] = new UnitAnimationFrame { Duration = DefaultDuration };
            }
            
            Frames = newFrames;
        }
    }

    public float GetFrameDuration(int index)
    {
        if (Frames == null || index >= Frames.Length) 
            return DefaultDuration;
        
        float duration = Frames[index].Duration;
        return duration > 0 ? duration : DefaultDuration;
    }

    public float GetTotalDuration()
    {
        int count = Sprites?.Length ?? 0;
        if (count == 0) return 0f;
        
        float total = 0f;
        for (int i = 0; i < count; i++)
            total += GetFrameDuration(i);
        
        return total;
    }
}

[CreateAssetMenu(fileName = "UnitSpriteAnimation", menuName = "ScriptableObjects/UnitSpriteAnimation")]
public class UnitSpriteAnimation : ScriptableObject
{
    private static readonly string[] DirectionNames = { "None", "Up", "Down", "Left", "Right" };

    [SerializeField] private UnitAnimationClip[] _clips;

    public UnitAnimationClip[] Clips => _clips;

    public UnitAnimationClip GetClip(string typeName, int directionIndex)
    {
        if (_clips == null)
            return null;

        string directionName = directionIndex >= 0 && directionIndex < DirectionNames.Length
            ? DirectionNames[directionIndex]
            : string.Empty;

        for (int i = 0; i < _clips.Length; i++)
        {
            var clip = _clips[i];
            if (clip == null) continue;
            if (!TryParseClipName(clip.Name, out string clipType, out string clipDirection)) continue;
            if (!string.Equals(clipType, typeName, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(clipDirection, directionName, StringComparison.OrdinalIgnoreCase) && HasSprites(clip))
                return clip;
        }

        return null;
    }

    public UnitAnimationClip GetClip(string name)
    {
        if (_clips == null)
        {
            Debug.LogError("[UnitSpriteAnimation] Animation clip array is empty.", this);
            return null;
        }
        
        foreach (var clip in _clips)
        {
            if (clip.Name == name)
                return clip;
        }
        
        Debug.LogError($"[UnitSpriteAnimation] Animation clip not found. name={name}", this);
        return null;
    }

    public float GetClipDuration(string name)
    {
        var clip = GetClip(name);
        return clip?.GetTotalDuration() ?? 0f;
    }

    public float GetClipDuration(string typeName, int directionIndex)
    {
        var clip = GetClip(typeName, directionIndex);
        return clip?.GetTotalDuration() ?? 0f;
    }

    private void OnValidate()
    {
        if (_clips == null) return;

        foreach (var clip in _clips)
        {
            clip?.SyncFrames();
        }
    }

    private static bool TryParseClipName(string clipName, out string typeName, out string directionName)
    {
        typeName = string.Empty;
        directionName = string.Empty;

        if (string.IsNullOrEmpty(clipName))
            return false;

        string[] tokens = clipName.Split('_');
        typeName = tokens[0];

        if (tokens.Length >= 2)
            directionName = tokens[1];

        return !string.IsNullOrEmpty(typeName);
    }

    private static bool HasSprites(UnitAnimationClip clip)
    {
        if (clip == null) return false;
        return clip.Sprites != null && clip.Sprites.Length > 0;
    }
}
