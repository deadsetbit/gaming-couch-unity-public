using System;
using System.Collections.Generic;
using UnityEngine;

namespace DSB.GC
{
    internal enum GCPlayerTransitionKind
    {
        PlayerStatusChanged = 1,
        PlayerEliminationStateChanged = 2,
        PlayerFinishStateChanged = 3,
        PlayerScoreChanged = 4,
        PlayerLivesChanged = 5,
        PlayerMeterChanged = 6,
    }

    internal enum GCPlayerTransitionRejectionReason
    {
        None = 0,
        DuplicateValue = 1,
        InvalidTransition = 2,
        InvalidRevoke = 3,
    }

    internal enum GCPlayerTransitionOutcome
    {
        Accepted = 1,
        NoOp = 2,
        Rejected = 3,
    }

    [Flags]
    internal enum GCPlayerLatestStateDirtyFlags
    {
        None = 0,
        RuntimeStateSnapshot = 1,
        PlayersHud = 2,
    }

    internal readonly struct GCPlayerStatusValue : IEquatable<GCPlayerStatusValue>
    {
        internal GCPlayerStatusValue(GCPlayerStatus status, string statusText)
        {
            Status = status;
            StatusText = statusText ?? "";
        }

        internal GCPlayerStatus Status { get; }
        internal string StatusText { get; }

        public bool Equals(GCPlayerStatusValue other)
        {
            return Status == other.Status &&
                string.Equals(StatusText, other.StatusText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is GCPlayerStatusValue other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Status * 397) ^ StringComparer.Ordinal.GetHashCode(StatusText);
            }
        }
    }

    internal readonly struct GCPlayerTransitionResult<TValue>
    {
        private GCPlayerTransitionResult(
            GCPlayerTransitionOutcome outcome,
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue previousValue,
            TValue value,
            string reasonText,
            float changedAtGameTime,
            bool emitsSemanticTransition,
            GCPlayerLatestStateDirtyFlags latestStateDirtyFlags,
            bool wasClamped,
            GCPlayerTransitionRejectionReason rejectionReason
        )
        {
            Outcome = outcome;
            Kind = kind;
            PlayerIndex = playerIndex;
            PreviousValue = previousValue;
            Value = value;
            ReasonText = reasonText;
            ChangedAtGameTime = changedAtGameTime;
            EmitsSemanticTransition = emitsSemanticTransition;
            LatestStateDirtyFlags = latestStateDirtyFlags;
            WasClamped = wasClamped;
            RejectionReason = rejectionReason;
        }

        internal GCPlayerTransitionOutcome Outcome { get; }
        internal bool Accepted => Outcome == GCPlayerTransitionOutcome.Accepted;
        internal bool IsNoOp => Outcome == GCPlayerTransitionOutcome.NoOp;
        internal bool IsRejected => Outcome == GCPlayerTransitionOutcome.Rejected;
        internal GCPlayerTransitionKind Kind { get; }
        internal int PlayerIndex { get; }
        internal TValue PreviousValue { get; }
        internal TValue Value { get; }
        internal string ReasonText { get; }
        internal float ChangedAtGameTime { get; }
        internal bool EmitsSemanticTransition { get; }
        internal GCPlayerLatestStateDirtyFlags LatestStateDirtyFlags { get; }
        internal bool WasClamped { get; }
        internal GCPlayerTransitionRejectionReason RejectionReason { get; }
        internal bool MarksLatestStateDirty => LatestStateDirtyFlags != GCPlayerLatestStateDirtyFlags.None;
        internal bool MarksRuntimeStateSnapshotDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot) != 0;
        internal bool MarksPlayersHudDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.PlayersHud) != 0;

        internal static GCPlayerTransitionResult<TValue> Accept(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue previousValue,
            TValue value,
            string reasonText,
            float changedAtGameTime,
            bool emitsSemanticTransition,
            GCPlayerLatestStateDirtyFlags latestStateDirtyFlags,
            bool wasClamped = false
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.Accepted,
                kind,
                playerIndex,
                previousValue,
                value,
                reasonText,
                changedAtGameTime,
                emitsSemanticTransition,
                latestStateDirtyFlags,
                wasClamped,
                GCPlayerTransitionRejectionReason.None
            );
        }

        internal static GCPlayerTransitionResult<TValue> NoOp(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue currentValue,
            TValue requestedValue,
            string reasonText,
            GCPlayerTransitionRejectionReason rejectionReason,
            bool wasClamped = false
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.NoOp,
                kind,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                -1f,
                false,
                GCPlayerLatestStateDirtyFlags.None,
                wasClamped,
                rejectionReason
            );
        }

        internal static GCPlayerTransitionResult<TValue> Reject(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TValue currentValue,
            TValue requestedValue,
            string reasonText,
            GCPlayerTransitionRejectionReason rejectionReason
        )
        {
            return new GCPlayerTransitionResult<TValue>(
                GCPlayerTransitionOutcome.Rejected,
                kind,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                -1f,
                false,
                GCPlayerLatestStateDirtyFlags.None,
                false,
                rejectionReason
            );
        }
    }

    internal readonly struct GCPlayerAcceptedTransition
    {
        private GCPlayerAcceptedTransition(
            GCPlayerTransitionKind kind,
            int playerIndex,
            string reasonText,
            float changedAtGameTime,
            bool emitsSemanticTransition,
            GCPlayerLatestStateDirtyFlags latestStateDirtyFlags,
            int previousIntValue,
            int intValue,
            GCPlayerStatusValue previousStatusValue,
            GCPlayerStatusValue statusValue,
            GCPlayerEliminationState previousEliminationState,
            GCPlayerEliminationState eliminationState,
            GCPlayerFinishState previousFinishState,
            GCPlayerFinishState finishState
        )
        {
            Kind = kind;
            PlayerIndex = playerIndex;
            ReasonText = reasonText;
            ChangedAtGameTime = changedAtGameTime;
            EmitsSemanticTransition = emitsSemanticTransition;
            LatestStateDirtyFlags = latestStateDirtyFlags;
            PreviousIntValue = previousIntValue;
            IntValue = intValue;
            PreviousStatusValue = previousStatusValue;
            StatusValue = statusValue;
            PreviousEliminationState = previousEliminationState;
            EliminationState = eliminationState;
            PreviousFinishState = previousFinishState;
            FinishState = finishState;
        }

        internal GCPlayerTransitionKind Kind { get; }
        internal int PlayerIndex { get; }
        internal string ReasonText { get; }
        internal float ChangedAtGameTime { get; }
        internal bool EmitsSemanticTransition { get; }
        internal GCPlayerLatestStateDirtyFlags LatestStateDirtyFlags { get; }
        internal bool MarksRuntimeStateSnapshotDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot) != 0;
        internal bool MarksPlayersHudDirty =>
            (LatestStateDirtyFlags & GCPlayerLatestStateDirtyFlags.PlayersHud) != 0;
        internal int PreviousIntValue { get; }
        internal int IntValue { get; }
        internal GCPlayerStatusValue PreviousStatusValue { get; }
        internal GCPlayerStatusValue StatusValue { get; }
        internal GCPlayerEliminationState PreviousEliminationState { get; }
        internal GCPlayerEliminationState EliminationState { get; }
        internal GCPlayerFinishState PreviousFinishState { get; }
        internal GCPlayerFinishState FinishState { get; }

        internal static GCPlayerAcceptedTransition FromElimination(
            GCPlayerTransitionResult<GCPlayerEliminationState> transition
        )
        {
            return new GCPlayerAcceptedTransition(
                transition.Kind,
                transition.PlayerIndex,
                transition.ReasonText,
                transition.ChangedAtGameTime,
                transition.EmitsSemanticTransition,
                transition.LatestStateDirtyFlags,
                0,
                0,
                default,
                default,
                transition.PreviousValue,
                transition.Value,
                default,
                default
            );
        }

        internal static GCPlayerAcceptedTransition FromFinish(
            GCPlayerTransitionResult<GCPlayerFinishState> transition
        )
        {
            return new GCPlayerAcceptedTransition(
                transition.Kind,
                transition.PlayerIndex,
                transition.ReasonText,
                transition.ChangedAtGameTime,
                transition.EmitsSemanticTransition,
                transition.LatestStateDirtyFlags,
                0,
                0,
                default,
                default,
                default,
                default,
                transition.PreviousValue,
                transition.Value
            );
        }

        internal static GCPlayerAcceptedTransition FromInt(GCPlayerTransitionResult<int> transition)
        {
            return new GCPlayerAcceptedTransition(
                transition.Kind,
                transition.PlayerIndex,
                transition.ReasonText,
                transition.ChangedAtGameTime,
                transition.EmitsSemanticTransition,
                transition.LatestStateDirtyFlags,
                transition.PreviousValue,
                transition.Value,
                default,
                default,
                default,
                default,
                default,
                default
            );
        }

        internal static GCPlayerAcceptedTransition FromStatus(
            GCPlayerTransitionResult<GCPlayerStatusValue> transition
        )
        {
            return new GCPlayerAcceptedTransition(
                transition.Kind,
                transition.PlayerIndex,
                transition.ReasonText,
                transition.ChangedAtGameTime,
                transition.EmitsSemanticTransition,
                transition.LatestStateDirtyFlags,
                0,
                0,
                transition.PreviousValue,
                transition.Value,
                default,
                default,
                default,
                default
            );
        }
    }

    internal static class GCPlayerTransitions
    {
        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetEliminatedPermanent(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            return SetPermanentState(
                GCPlayerTransitionKind.PlayerEliminationStateChanged,
                playerIndex,
                currentState,
                permanentState: GCPlayerEliminationState.Permanent,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetEliminatedRevokable(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            return SetRevokableState(
                GCPlayerTransitionKind.PlayerEliminationStateChanged,
                playerIndex,
                currentState,
                revokableState: GCPlayerEliminationState.Revokable,
                permanentState: GCPlayerEliminationState.Permanent,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerEliminationState> SetRevokeEliminated(
            int playerIndex,
            GCPlayerEliminationState currentState,
            string reasonText
        )
        {
            return RevokeState(
                GCPlayerTransitionKind.PlayerEliminationStateChanged,
                playerIndex,
                currentState,
                revokableState: GCPlayerEliminationState.Revokable,
                noneState: GCPlayerEliminationState.None,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetFinishedPermanent(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            return SetPermanentState(
                GCPlayerTransitionKind.PlayerFinishStateChanged,
                playerIndex,
                currentState,
                permanentState: GCPlayerFinishState.Permanent,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetFinishedRevokable(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            return SetRevokableState(
                GCPlayerTransitionKind.PlayerFinishStateChanged,
                playerIndex,
                currentState,
                revokableState: GCPlayerFinishState.Revokable,
                permanentState: GCPlayerFinishState.Permanent,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerFinishState> SetRevokeFinished(
            int playerIndex,
            GCPlayerFinishState currentState,
            string reasonText
        )
        {
            return RevokeState(
                GCPlayerTransitionKind.PlayerFinishStateChanged,
                playerIndex,
                currentState,
                revokableState: GCPlayerFinishState.Revokable,
                noneState: GCPlayerFinishState.None,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<GCPlayerStatusValue> SetStatus(
            int playerIndex,
            GCPlayerStatus currentStatus,
            string currentStatusText,
            GCPlayerStatus requestedStatus,
            string requestedStatusText,
            string reasonText
        )
        {
            var currentValue = new GCPlayerStatusValue(currentStatus, currentStatusText);
            var requestedValue = new GCPlayerStatusValue(requestedStatus, requestedStatusText);

            if (currentValue.Equals(requestedValue))
            {
                return GCPlayerTransitionResult<GCPlayerStatusValue>.NoOp(
                    GCPlayerTransitionKind.PlayerStatusChanged,
                    playerIndex,
                    currentValue,
                    requestedValue,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            return GCPlayerTransitionResult<GCPlayerStatusValue>.Accept(
                GCPlayerTransitionKind.PlayerStatusChanged,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud
            );
        }

        internal static GCPlayerTransitionResult<int> SetScore(
            int playerIndex,
            int currentScore,
            int requestedScore,
            string reasonText
        )
        {
            return SetIntValue(
                GCPlayerTransitionKind.PlayerScoreChanged,
                playerIndex,
                currentScore,
                requestedScore,
                reasonText
            );
        }

        internal static GCPlayerTransitionResult<int> SetLives(
            int playerIndex,
            int currentLives,
            int requestedLives,
            string reasonText
        )
        {
            var value = requestedLives;
            var wasClamped = false;

            if (value < 0)
            {
                value = 0;
                wasClamped = true;
            }

            return SetIntValue(
                GCPlayerTransitionKind.PlayerLivesChanged,
                playerIndex,
                currentLives,
                value,
                reasonText,
                wasClamped
            );
        }

        internal static GCPlayerTransitionResult<int> SetMeter(
            int playerIndex,
            int currentMeter,
            int requestedMeter,
            string reasonText
        )
        {
            var value = requestedMeter;
            var wasClamped = false;

            if (value < -1)
            {
                value = -1;
                wasClamped = true;
            }
            else if (value > 100)
            {
                value = 100;
                wasClamped = true;
            }

            return SetIntValue(
                GCPlayerTransitionKind.PlayerMeterChanged,
                playerIndex,
                currentMeter,
                value,
                reasonText,
                wasClamped
            );
        }

        // ADR 0003 keeps elimination and finish as independent enums, not independent rules: both
        // families promote in place, reject a permanent-to-revokable step, and revoke only from
        // revokable. One generic shape parameterized on the enum and the transition kind carries them.
        private static GCPlayerTransitionResult<TState> SetPermanentState<TState>(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TState currentState,
            TState permanentState,
            string reasonText
        ) where TState : struct, Enum
        {
            if (StateEquals(currentState, permanentState))
            {
                return GCPlayerTransitionResult<TState>.NoOp(
                    kind,
                    playerIndex,
                    currentState,
                    permanentState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            return AcceptStateChange(kind, playerIndex, currentState, permanentState, reasonText);
        }

        private static GCPlayerTransitionResult<TState> SetRevokableState<TState>(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TState currentState,
            TState revokableState,
            TState permanentState,
            string reasonText
        ) where TState : struct, Enum
        {
            if (StateEquals(currentState, revokableState))
            {
                return GCPlayerTransitionResult<TState>.NoOp(
                    kind,
                    playerIndex,
                    currentState,
                    revokableState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue
                );
            }

            if (StateEquals(currentState, permanentState))
            {
                return GCPlayerTransitionResult<TState>.Reject(
                    kind,
                    playerIndex,
                    currentState,
                    revokableState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidTransition
                );
            }

            return AcceptStateChange(kind, playerIndex, currentState, revokableState, reasonText);
        }

        private static GCPlayerTransitionResult<TState> RevokeState<TState>(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TState currentState,
            TState revokableState,
            TState noneState,
            string reasonText
        ) where TState : struct, Enum
        {
            if (!StateEquals(currentState, revokableState))
            {
                return GCPlayerTransitionResult<TState>.Reject(
                    kind,
                    playerIndex,
                    currentState,
                    noneState,
                    reasonText,
                    GCPlayerTransitionRejectionReason.InvalidRevoke
                );
            }

            return AcceptStateChange(kind, playerIndex, currentState, noneState, reasonText);
        }

        private static GCPlayerTransitionResult<TState> AcceptStateChange<TState>(
            GCPlayerTransitionKind kind,
            int playerIndex,
            TState currentState,
            TState requestedState,
            string reasonText
        ) where TState : struct, Enum
        {
            return GCPlayerTransitionResult<TState>.Accept(
                kind,
                playerIndex,
                currentState,
                requestedState,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud
            );
        }

        private static bool StateEquals<TState>(TState left, TState right) where TState : struct, Enum
        {
            return EqualityComparer<TState>.Default.Equals(left, right);
        }

        private static GCPlayerTransitionResult<int> SetIntValue(
            GCPlayerTransitionKind kind,
            int playerIndex,
            int currentValue,
            int requestedValue,
            string reasonText,
            bool wasClamped = false
        )
        {
            if (currentValue == requestedValue)
            {
                return GCPlayerTransitionResult<int>.NoOp(
                    kind,
                    playerIndex,
                    currentValue,
                    requestedValue,
                    reasonText,
                    GCPlayerTransitionRejectionReason.DuplicateValue,
                    wasClamped
                );
            }

            return GCPlayerTransitionResult<int>.Accept(
                kind,
                playerIndex,
                currentValue,
                requestedValue,
                reasonText,
                Time.time,
                emitsSemanticTransition: true,
                latestStateDirtyFlags: GCPlayerLatestStateDirtyFlags.RuntimeStateSnapshot |
                    GCPlayerLatestStateDirtyFlags.PlayersHud,
                wasClamped: wasClamped
            );
        }
    }
}
