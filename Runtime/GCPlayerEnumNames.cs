namespace DSB.GC
{
    /// <summary>
    /// Allocation-free enum-to-wire-name lookups for runtime output paths.
    /// Enum.ToString() allocates a new string on every call; these return
    /// cached const strings (the enum member names, i.e. the current wire
    /// representation). The default case falls back to ToString() so behavior
    /// is unchanged for any unexpected value.
    /// </summary>
    internal static class GCPlayerEnumNames
    {
        public static string Status(GCPlayerStatus status)
        {
            switch (status)
            {
                case GCPlayerStatus.Neutral:
                    return "Neutral";
                case GCPlayerStatus.Pending:
                    return "Pending";
                case GCPlayerStatus.Success:
                    return "Success";
                case GCPlayerStatus.Failure:
                    return "Failure";
                case GCPlayerStatus.Warning:
                    return "Warning";
                case GCPlayerStatus.Alert:
                    return "Alert";
                default:
                    return status.ToString();
            }
        }

        public static string EliminationState(GCPlayerEliminationState state)
        {
            switch (state)
            {
                case GCPlayerEliminationState.None:
                    return "None";
                case GCPlayerEliminationState.Revokable:
                    return "Revokable";
                case GCPlayerEliminationState.Permanent:
                    return "Permanent";
                default:
                    return state.ToString();
            }
        }

        public static string FinishState(GCPlayerFinishState state)
        {
            switch (state)
            {
                case GCPlayerFinishState.None:
                    return "None";
                case GCPlayerFinishState.Revokable:
                    return "Revokable";
                case GCPlayerFinishState.Permanent:
                    return "Permanent";
                default:
                    return state.ToString();
            }
        }
    }
}
