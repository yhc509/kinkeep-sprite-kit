using KinKeep.SpriteKit;
using UnityEditor;
using UnityEngine;

namespace KinKeep.SpriteKit.Editor
{
    public sealed class UnitSpriteAnimationPreviewPanel
    {
        private static GUIStyle _timelineFrameLabelStyle;

        private int _currentFrame;
        private float _frameTime;
        private bool _isPlaying;
        private double _lastTime;

        public int CurrentFrame => _currentFrame;
        public bool IsPlaying => _isPlaying;

        public void ResetPlayback()
        {
            _currentFrame = 0;
            _frameTime = 0f;
            _isPlaying = false;
            _lastTime = 0d;
        }

        public void SelectFrame(int frameIndex)
        {
            _currentFrame = Mathf.Max(0, frameIndex);
            _frameTime = 0f;
            _isPlaying = false;
        }

        public void OnEditorUpdate(UnitSpriteAnimation animationData, int selectedClipIndex)
        {
            if (!_isPlaying || animationData == null)
            {
                return;
            }

            UnitAnimationClip[] clips = animationData.Clips;
            if (clips == null || clips.Length == 0 || selectedClipIndex >= clips.Length)
            {
                return;
            }

            UnitAnimationClip clip = clips[selectedClipIndex];
            if (clip == null || clip.Sprites == null || clip.Sprites.Length == 0)
            {
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastTime);
            _lastTime = currentTime;

            _frameTime += deltaTime;
            float frameDuration = clip.GetFrameDuration(_currentFrame);
            while (_frameTime >= frameDuration)
            {
                _frameTime -= frameDuration;
                _currentFrame++;

                if (_currentFrame >= clip.Sprites.Length)
                {
                    if (clip.Loop)
                    {
                        _currentFrame = 0;
                    }
                    else
                    {
                        _currentFrame = clip.Sprites.Length - 1;
                        _isPlaying = false;
                        break;
                    }
                }

                frameDuration = clip.GetFrameDuration(_currentFrame);
            }
        }

        public void Draw(UnitAnimationClip clip)
        {
            DrawPlaybackControls();
            DrawSpritePreview(clip);

            EditorGUILayout.Space(10);
            DrawTimeline(clip);
            DrawFrameInfo(clip);
        }

        private void DrawPlaybackControls()
        {
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button(_isPlaying ? "■ Stop" : "▶ Preview", GUILayout.Width(80)))
            {
                _isPlaying = !_isPlaying;
                if (_isPlaying)
                {
                    _lastTime = EditorApplication.timeSinceStartup;
                    _currentFrame = 0;
                    _frameTime = 0f;
                }
            }

            if (GUILayout.Button("↺ Reset", GUILayout.Width(70)))
            {
                ResetPlayback();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSpritePreview(UnitAnimationClip clip)
        {
            if (_currentFrame >= clip.Sprites.Length)
            {
                return;
            }

            Sprite sprite = clip.Sprites[_currentFrame];
            if (sprite == null || sprite.texture == null)
            {
                return;
            }

            bool isFlipped = clip.Frames != null
                && _currentFrame < clip.Frames.Length
                && clip.Frames[_currentFrame] != null
                && clip.Frames[_currentFrame].FlipX;

            float previewSize = 200f;
            Rect previewRect = GUILayoutUtility.GetRect(previewSize, previewSize);
            EditorGUI.DrawRect(previewRect, new Color(0.15f, 0.15f, 0.15f));

            Rect spriteRect = sprite.rect;
            Rect uvRect = new Rect(
                spriteRect.x / sprite.texture.width,
                spriteRect.y / sprite.texture.height,
                spriteRect.width / sprite.texture.width,
                spriteRect.height / sprite.texture.height);

            if (isFlipped)
            {
                uvRect.x += uvRect.width;
                uvRect.width = -uvRect.width;
            }

            float aspectRatio = spriteRect.width / spriteRect.height;
            Rect drawRect;
            if (aspectRatio > 1f)
            {
                float height = previewSize / aspectRatio;
                drawRect = new Rect(previewRect.x, previewRect.y + (previewSize - height) / 2f, previewSize, height);
            }
            else
            {
                float width = previewSize * aspectRatio;
                drawRect = new Rect(previewRect.x + (previewSize - width) / 2f, previewRect.y, width, previewSize);
            }

            GUI.DrawTextureWithTexCoords(drawRect, sprite.texture, uvRect);
        }

        private void DrawTimeline(UnitAnimationClip clip)
        {
            EditorGUILayout.LabelField("Timeline", EditorStyles.boldLabel);

            float totalDuration = clip.GetTotalDuration();
            if (totalDuration <= 0f)
            {
                return;
            }

            Rect timelineRect = GUILayoutUtility.GetRect(EditorGUIUtility.currentViewWidth - 500f, 40f);
            EditorGUI.DrawRect(timelineRect, new Color(0.25f, 0.25f, 0.25f));

            float x = timelineRect.x;
            for (int i = 0; i < clip.Sprites.Length; i++)
            {
                float frameDuration = clip.GetFrameDuration(i);
                float frameWidth = (frameDuration / totalDuration) * timelineRect.width;
                Rect frameRect = new Rect(x, timelineRect.y, frameWidth - 2f, timelineRect.height);

                bool hasEvents = clip.Frames != null
                    && i < clip.Frames.Length
                    && clip.Frames[i] != null
                    && clip.Frames[i].Events != null
                    && clip.Frames[i].Events.Length > 0;

                Color color = GetFrameColor(i, hasEvents);
                EditorGUI.DrawRect(frameRect, color);

                if (frameWidth > 20f)
                {
                    GUI.Label(frameRect, i.ToString(), TimelineFrameLabelStyle);
                }

                if (Event.current.type == EventType.MouseDown && frameRect.Contains(Event.current.mousePosition))
                {
                    SelectFrame(i);
                    Event.current.Use();
                }

                x += frameWidth;
            }
        }

        private void DrawFrameInfo(UnitAnimationClip clip)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField($"Frame: {_currentFrame} / {clip.Sprites.Length - 1}");
            EditorGUILayout.LabelField($"Duration: {clip.GetTotalDuration():F2}s");

            if (clip.Frames == null || _currentFrame >= clip.Frames.Length || clip.Frames[_currentFrame] == null)
            {
                return;
            }

            FrameEvent[] events = clip.Frames[_currentFrame].Events;
            if (events == null || events.Length == 0)
            {
                return;
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Events:", EditorStyles.boldLabel);
            for (int i = 0; i < events.Length; i++)
            {
                EditorGUILayout.LabelField($"  • {events[i].Type}: {events[i].Param}");
            }
        }

        private Color GetFrameColor(int frameIndex, bool hasEvents)
        {
            if (frameIndex == _currentFrame)
            {
                return hasEvents ? new Color(0.8f, 0.8f, 0.3f) : new Color(0.3f, 0.7f, 0.3f);
            }

            return hasEvents ? new Color(0.6f, 0.5f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
        }

        private static GUIStyle TimelineFrameLabelStyle
        {
            get
            {
                if (_timelineFrameLabelStyle == null)
                {
                    _timelineFrameLabelStyle = new GUIStyle(GUI.skin.label)
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontSize = 10
                    };
                }

                return _timelineFrameLabelStyle;
            }
        }
    }
}
