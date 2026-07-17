using UnityEngine;

namespace CharacterMatch3.Input
{
    [CreateAssetMenu(menuName = "Character Match-3/Input Settings", fileName = "Match3InputSettings")]
    public sealed class Match3InputSettings : ScriptableObject
    {
        [SerializeField, Min(1f)] private float minimumSwipeDistance = 48f;
        [SerializeField, Min(1f)] private float maximumTapMovementTolerance = 24f;
        [SerializeField, Min(0.01f)] private float maximumTapDuration = 0.3f;
        [SerializeField, Min(0.1f)] private float inputSensitivity = 1f;
        [SerializeField] private bool enableTapToSelect = true;

        public float MinimumSwipeDistance => Mathf.Max(1f, minimumSwipeDistance);
        public float MaximumTapMovementTolerance => Mathf.Max(1f, maximumTapMovementTolerance);
        public float MaximumTapDuration => Mathf.Max(0.01f, maximumTapDuration);
        public float InputSensitivity => Mathf.Max(0.1f, inputSensitivity);
        public bool EnableTapToSelect => enableTapToSelect;
    }
}
