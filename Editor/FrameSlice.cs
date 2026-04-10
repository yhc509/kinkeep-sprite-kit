namespace KinKeep.SpriteKit.Editor
{
    public readonly struct FrameSlice
    {
        public int StartInclusive { get; }
        public int EndInclusive { get; }

        public FrameSlice(int startInclusive, int endInclusive)
        {
            StartInclusive = startInclusive;
            EndInclusive = endInclusive;
        }
    }
}
