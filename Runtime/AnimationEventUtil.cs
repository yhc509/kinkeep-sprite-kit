using System;
using System.Collections;
using UnityEngine;

public static class AnimationEventUtil
{
    public static IEnumerator WaitAnimationComplete(SpriteAnimator animator)
    {
        bool done = false;
        void OnComplete() => done = true;
        
        animator.OnAnimationComplete += OnComplete;
        try
        {
            yield return new WaitUntil(() => done);
        }
        finally
        {
            animator.OnAnimationComplete -= OnComplete;
        }
    }
    
    public static IEnumerator WaitHit(SpriteAnimator animator)
    {
        bool hit = false;
        void OnHit() => hit = true;
        
        animator.OnHit += OnHit;
        try
        {
            yield return new WaitUntil(() => hit);
        }
        finally
        {
            animator.OnHit -= OnHit;
        }
    }
    
    public static IEnumerator WaitHitWithCallback(SpriteAnimator animator, Action onHit)
    {
        bool hit = false;
        void OnHit()
        {
            hit = true;
            onHit?.Invoke();
        }
        
        animator.OnHit += OnHit;
        try
        {
            yield return new WaitUntil(() => hit);
        }
        finally
        {
            animator.OnHit -= OnHit;
        }
    }
}
