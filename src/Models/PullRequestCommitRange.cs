using System.Collections.Generic;

namespace DevBoard.Models
{
    public static class PullRequestCommitRange
    {
        public static string FromMergeRef(string mergeLocalRef)
        {
            return $"--reverse {mergeLocalRef}^1..{mergeLocalRef}^2";
        }

        public static string FromHeadFallback(string mergeBase, string headLocalRef)
        {
            return $"--reverse {mergeBase}..{headLocalRef}";
        }

        public static bool ContainsMergeCommit(IReadOnlyList<Commit> commits)
        {
            foreach (var commit in commits)
            {
                if (commit.Parents is { Count: > 1 })
                    return true;
            }

            return false;
        }
    }
}
