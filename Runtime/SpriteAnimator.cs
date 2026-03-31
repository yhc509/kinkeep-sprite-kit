using System;
using UnityEngine;

public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private UnitSpriteAnimation _animation;

    private int _directionIndex = 0;
    private bool _flipX = false;
    private UnitAnimationClip _currentClip;
    private int _frameIndex;
    private float _frameTime;
    private bool _isPlaying;
    private string _currentTypeName = string.Empty;
    private string _currentClipName = string.Empty;

    public event Action OnHit;
    public event Action OnAnimationComplete;
    public event Action<FrameEvent> OnFrameEvent;

    public bool IsPlaying => _isPlaying;
    public UnitAnimationClip CurrentClip => _currentClip;

    private void Awake()
    {
        EnsureRenderer();
    }

    private void Update()
    {
        if (!_isPlaying || _currentClip == null) return;
        if (_currentClip.Sprites == null || _currentClip.Sprites.Length == 0) return;

        _frameTime += Time.deltaTime;
        
        float frameDuration = _currentClip.GetFrameDuration(_frameIndex);
        
        while (_frameTime >= frameDuration)
        {
            _frameTime -= frameDuration;
            _frameIndex++;

            if (_frameIndex >= _currentClip.Sprites.Length)
            {
                if (_currentClip.Loop)
                {
                    _frameIndex = 0;
                }
                else
                {
                    _frameIndex = _currentClip.Sprites.Length - 1;
                    _isPlaying = false;
                    OnAnimationComplete?.Invoke();
                    break;
                }
            }

            ProcessFrameEvents(_frameIndex);
            frameDuration = _currentClip.GetFrameDuration(_frameIndex);
        }

        ApplyFrame();
    }

    public void SetAnimation(UnitSpriteAnimation animation)
    {
        EnsureRenderer();
        _animation = animation;
        _currentClip = null;
        _frameIndex = 0;
        _frameTime = 0f;
        _isPlaying = false;
        _currentTypeName = string.Empty;
        _currentClipName = string.Empty;
    }

    public void SetRenderer(SpriteRenderer renderer)
    {
        _renderer = renderer;
    }

    public void Play(string name)
    {
        EnsureAnimationAssigned();

        var clip = _animation.GetClip(name, _directionIndex);
        if (clip != null && TryStartClip(clip))
        {
            _currentTypeName = name;
            _currentClipName = string.Empty;
            return;
        }

        clip = _animation.GetClip(name);
        if (!TryStartClip(clip))
        {
            throw new InvalidOperationException($"[SpriteAnimator] Clip not found. name={name}, directionIndex={_directionIndex}");
        }

        _currentTypeName = string.Empty;
        _currentClipName = name;
    }

    public void Stop()
    {
        _isPlaying = false;
    }

    public float GetDuration(string name)
    {
        EnsureAnimationAssigned();

        var clip = _animation.GetClip(name, _directionIndex);
        if (clip != null && clip.Sprites != null && clip.Sprites.Length > 0)
            return clip.GetTotalDuration();

        clip = _animation.GetClip(name);
        if (clip == null || clip.Sprites == null || clip.Sprites.Length == 0)
        {
            throw new InvalidOperationException($"[SpriteAnimator] Duration target clip missing. name={name}");
        }

        return clip.GetTotalDuration();
    }

    public void SetColor(Color color)
    {
        EnsureRenderer();
        if (_renderer != null)
            _renderer.color = color;
    }

    public void SetFlipX(bool flip)
    {
        EnsureRenderer();
        if (_renderer != null)
            _renderer.flipX = flip;
    }

    public void SetDirection(int directionIndex, bool flipX)
    {
        _directionIndex = directionIndex;
        _flipX = flipX;
        ReevaluateCurrentClipForDirection();
        ApplyFrame();
    }

    private void ApplyFrame()
    {
        EnsureRenderer();
        if (_renderer == null) return;

        if (_currentClip == null)
        {
            _renderer.flipX = _flipX;
            return;
        }

        if (_frameIndex >= _currentClip.Sprites.Length) return;

        _renderer.sprite = _currentClip.Sprites[_frameIndex];

        bool frameFlipX = false;
        if (_currentClip.Frames != null && _frameIndex < _currentClip.Frames.Length)
            frameFlipX = _currentClip.Frames[_frameIndex].FlipX;

        _renderer.flipX = _flipX ^ frameFlipX;
    }

    private void ProcessFrameEvents(int frameIndex)
    {
        if (_currentClip.Frames == null) return;
        if (frameIndex >= _currentClip.Frames.Length) return;
        
        var frame = _currentClip.Frames[frameIndex];
        if (frame.Events == null) return;

        foreach (var evt in frame.Events)
        {
            switch (evt.Type)
            {
                case FrameEventType.Hit:
                case FrameEventType.Skill:
                    OnHit?.Invoke();
                    break;
                default:
                    OnFrameEvent?.Invoke(evt);
                    break;
            }
        }
    }

    private bool TryStartClip(UnitAnimationClip clip)
    {
        if (clip == null) return false;
        if (clip.Sprites == null || clip.Sprites.Length == 0) return false;

        _currentClip = clip;
        _frameIndex = 0;
        _frameTime = 0f;
        _isPlaying = true;

        ProcessFrameEvents(0);
        ApplyFrame();
        return true;
    }

    private void ReevaluateCurrentClipForDirection()
    {
        if (_animation == null || _currentClip == null) return;

        var directionalClip = ResolveCurrentClipByDirection();
        if (directionalClip == null) return;
        if (directionalClip.Sprites == null || directionalClip.Sprites.Length == 0) return;
        if (ReferenceEquals(directionalClip, _currentClip)) return;

        _currentClip = directionalClip;
        _frameIndex = Mathf.Clamp(_frameIndex, 0, _currentClip.Sprites.Length - 1);
        _frameTime = Mathf.Clamp(_frameTime, 0f, _currentClip.GetFrameDuration(_frameIndex));
    }

    private UnitAnimationClip ResolveCurrentClipByDirection()
    {
        if (!string.IsNullOrEmpty(_currentTypeName))
            return _animation.GetClip(_currentTypeName, _directionIndex);

        if (!string.IsNullOrEmpty(_currentClipName))
            return _animation.GetClip(_currentClipName);

        return _currentClip;
    }

    private void EnsureRenderer()
    {
        if (_renderer == null)
            _renderer = GetComponent<SpriteRenderer>();
    }

    private void EnsureAnimationAssigned()
    {
        if (_animation == null)
        {
            throw new InvalidOperationException("[SpriteAnimator] UnitSpriteAnimation is not assigned.");
        }
    }
}
