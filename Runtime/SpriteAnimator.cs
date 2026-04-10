using System;
using UnityEngine;

namespace KinKeep.SpriteKit
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class SpriteAnimator : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _renderer;
        [SerializeField] private UnitSpriteAnimation _animation;

        private int _directionIndex;
        private bool _isExternallyFlipped;
        private bool _isDirectionResolvedFlipped;
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
            if (_renderer == null)
                _renderer = GetComponent<SpriteRenderer>();
        }

        private void Update()
        {
            if (!_isPlaying || _currentClip == null)
                return;

            if (_currentClip.Sprites == null || _currentClip.Sprites.Length == 0)
                return;

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
            _animation = animation;
            _currentClip = null;
            _frameIndex = 0;
            _frameTime = 0f;
            _isPlaying = false;
            _currentTypeName = string.Empty;
            _currentClipName = string.Empty;
            _isDirectionResolvedFlipped = false;
        }

        public void SetRenderer(SpriteRenderer renderer)
        {
            _renderer = renderer;
        }

        public void Play(string name)
        {
            EnsureAnimationAssigned();

            if (_animation.TryGetDirectionalClip(name, _directionIndex, out UnitAnimationClip directionalClip, out bool isDirectionResolvedFlipped)
                && TryStartClip(directionalClip, isDirectionResolvedFlipped))
            {
                _currentTypeName = name;
                _currentClipName = string.Empty;
                return;
            }

            if (!_animation.TryGetClip(name, out UnitAnimationClip clip) || !TryStartClip(clip, false))
            {
                throw new InvalidOperationException(
                    $"[SpriteAnimator] Clip not found. name={name}, directionIndex={_directionIndex}");
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

            if (_animation.TryGetDirectionalClip(name, _directionIndex, out UnitAnimationClip directionalClip, out _))
                return directionalClip.GetTotalDuration();

            if (!_animation.TryGetClip(name, out UnitAnimationClip clip))
                throw new InvalidOperationException($"[SpriteAnimator] Duration target clip missing. name={name}");

            return clip.GetTotalDuration();
        }

        public void SetColor(Color color)
        {
            _renderer.color = color;
        }

        public void SetFlipX(bool isFlipped)
        {
            _isExternallyFlipped = isFlipped;
            ApplyFrame();
        }

        public void SetDirection(int directionIndex, bool isFlipped)
        {
            _directionIndex = directionIndex;
            // SetFlipX and SetDirection(bool isFlipped) both control the same external flip override.
            // Direction-based fallback flip is resolved only through TryGetDirectionalClip.
            _isExternallyFlipped = isFlipped;
            ReevaluateCurrentClipForDirection();
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_currentClip == null)
            {
                _renderer.flipX = _isExternallyFlipped;
                return;
            }

            if (_frameIndex >= _currentClip.Sprites.Length)
                return;

            _renderer.sprite = _currentClip.Sprites[_frameIndex];

            bool frameFlipX = false;
            if (_currentClip.Frames != null && _frameIndex < _currentClip.Frames.Length && _currentClip.Frames[_frameIndex] != null)
                frameFlipX = _currentClip.Frames[_frameIndex].FlipX;

            _renderer.flipX = _isExternallyFlipped ^ _isDirectionResolvedFlipped ^ frameFlipX;
        }

        private void ProcessFrameEvents(int frameIndex)
        {
            if (_currentClip.Frames == null || frameIndex >= _currentClip.Frames.Length)
                return;

            UnitAnimationFrame frame = _currentClip.Frames[frameIndex];
            if (frame == null || frame.Events == null)
                return;

            for (int i = 0; i < frame.Events.Length; i++)
            {
                FrameEvent frameEvent = frame.Events[i];
                switch (frameEvent.Type)
                {
                    case FrameEventType.Hit:
                    case FrameEventType.Skill:
                        OnHit?.Invoke();
                        break;
                    default:
                        OnFrameEvent?.Invoke(frameEvent);
                        break;
                }
            }
        }

        private bool TryStartClip(UnitAnimationClip clip, bool isDirectionResolvedFlipped)
        {
            if (clip == null || clip.Sprites == null || clip.Sprites.Length == 0)
                return false;

            _currentClip = clip;
            _isDirectionResolvedFlipped = isDirectionResolvedFlipped;
            _frameIndex = 0;
            _frameTime = 0f;
            _isPlaying = true;

            ProcessFrameEvents(0);
            ApplyFrame();
            return true;
        }

        private void ReevaluateCurrentClipForDirection()
        {
            if (_animation == null || _currentClip == null)
                return;

            if (!TryResolveCurrentClipByDirection(out UnitAnimationClip directionalClip, out bool isDirectionResolvedFlipped))
                return;

            if (directionalClip == null || directionalClip.Sprites == null || directionalClip.Sprites.Length == 0)
                return;

            bool isSameClip = ReferenceEquals(directionalClip, _currentClip);
            if (isSameClip && _isDirectionResolvedFlipped == isDirectionResolvedFlipped)
                return;

            _currentClip = directionalClip;
            _isDirectionResolvedFlipped = isDirectionResolvedFlipped;
            _frameIndex = Mathf.Clamp(_frameIndex, 0, _currentClip.Sprites.Length - 1);
            _frameTime = Mathf.Clamp(_frameTime, 0f, _currentClip.GetFrameDuration(_frameIndex));
        }

        private bool TryResolveCurrentClipByDirection(out UnitAnimationClip clip, out bool isDirectionResolvedFlipped)
        {
            clip = _currentClip;
            isDirectionResolvedFlipped = _isDirectionResolvedFlipped;

            if (!string.IsNullOrEmpty(_currentTypeName))
                return _animation.TryGetDirectionalClip(_currentTypeName, _directionIndex, out clip, out isDirectionResolvedFlipped);

            if (!string.IsNullOrEmpty(_currentClipName))
            {
                isDirectionResolvedFlipped = false;
                return _animation.TryGetClip(_currentClipName, out clip);
            }

            return clip != null;
        }

        private void EnsureAnimationAssigned()
        {
            if (_animation == null)
                throw new InvalidOperationException("[SpriteAnimator] UnitSpriteAnimation is not assigned.");
        }
    }
}
