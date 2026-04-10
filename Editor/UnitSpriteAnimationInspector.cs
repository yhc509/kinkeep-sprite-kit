using UnityEditor;

namespace KinKeep.SpriteKit.Editor
{
    [CustomEditor(typeof(UnitSpriteAnimation))]
    public sealed class UnitSpriteAnimationInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (!EditorGUI.EndChangeCheck())
                return;

            bool hasSerializedChanges = serializedObject.ApplyModifiedProperties();
            UnitSpriteAnimation animation = (UnitSpriteAnimation)target;

            Undo.RecordObject(animation, "Sync Clip Frames");
            bool hasSyncedFrames = animation.SyncClipFrames();
            if (!hasSerializedChanges && !hasSyncedFrames)
                return;

            EditorUtility.SetDirty(animation);
            serializedObject.UpdateIfRequiredOrScript();
        }
    }
}
