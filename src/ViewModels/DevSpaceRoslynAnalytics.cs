using System;

namespace DevBoard.ViewModels
{
    public sealed class DevSpaceRoslynAnalytics
    {
        public DevSpaceDashboard Dashboard { get; }

        public DevSpaceRoslynAnalytics(DevSpaceDashboard dashboard)
        {
            Dashboard = dashboard ?? throw new ArgumentNullException(nameof(dashboard));
        }
    }
}
