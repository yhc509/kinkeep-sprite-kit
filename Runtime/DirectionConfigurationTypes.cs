using System;
using UnityEngine;

namespace KinKeep.SpriteKit
{
    [Serializable]
    public struct DirectionEntry
    {
        [SerializeField] private int _index;
        [SerializeField] private string _name;

        public int Index => _index;
        public string Name => string.IsNullOrWhiteSpace(_name) ? string.Empty : _name.Trim();

        public DirectionEntry(int index, string name)
        {
            _index = index;
            _name = name;
        }
    }

    [Serializable]
    public struct FlipEntry
    {
        [SerializeField] private int _sourceDirection;
        [SerializeField] private int _targetDirection;

        public int SourceDirection => _sourceDirection;
        public int TargetDirection => _targetDirection;

        public FlipEntry(int sourceDirection, int targetDirection)
        {
            _sourceDirection = sourceDirection;
            _targetDirection = targetDirection;
        }
    }

    [Serializable]
    public struct GeneratorDirectionEntry
    {
        [SerializeField] private string _suffix;
        [SerializeField] private int _directionIndex;

        public string Suffix => string.IsNullOrWhiteSpace(_suffix) ? string.Empty : _suffix.Trim();
        public int DirectionIndex => _directionIndex;

        public GeneratorDirectionEntry(string suffix, int directionIndex)
        {
            _suffix = suffix;
            _directionIndex = directionIndex;
        }
    }
}
