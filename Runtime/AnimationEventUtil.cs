using System;
using System.Collections;
using UnityEngine;

namespace KinKeep.SpriteKit
{
    public static class AnimationEventUtil
    {
        public static IEnumerator WaitAnimationComplete(SpriteAnimator animator)
        {
            bool isDone = false;
            void OnComplete() => isDone = true;

            animator.OnAnimationComplete += OnComplete;
            try
            {
                yield return new WaitUntil(() => isDone);
            }
            finally
            {
                animator.OnAnimationComplete -= OnComplete;
            }
        }

        public static IEnumerator WaitHit(SpriteAnimator animator)
        {
            bool hasHit = false;
            void OnHit() => hasHit = true;

            animator.OnHit += OnHit;
            try
            {
                yield return new WaitUntil(() => hasHit);
            }
            finally
            {
                animator.OnHit -= OnHit;
            }
        }

        public static IEnumerator WaitHitWithCallback(SpriteAnimator animator, Action onHit)
        {
            bool hasHit = false;
            void OnHit()
            {
                hasHit = true;
                onHit?.Invoke();
            }

            animator.OnHit += OnHit;
            try
            {
                yield return new WaitUntil(() => hasHit);
            }
            finally
            {
                animator.OnHit -= OnHit;
            }
        }
    }
}
