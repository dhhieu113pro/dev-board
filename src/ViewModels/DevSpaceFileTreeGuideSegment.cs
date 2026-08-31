namespace DevBoard.ViewModels
{
    public sealed class DevSpaceFileTreeGuideSegment
    {
        public bool ShowTop { get; }
        public bool ShowBottom { get; }
        public bool ShowHorizontal { get; }

        internal DevSpaceFileTreeGuideSegment(bool showTop, bool showBottom, bool showHorizontal)
        {
            ShowTop = showTop;
            ShowBottom = showBottom;
            ShowHorizontal = showHorizontal;
        }
    }
}
