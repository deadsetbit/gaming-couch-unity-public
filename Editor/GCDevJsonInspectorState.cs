using System;
using DSB.GC.Dev;
using UnityEditor;

internal sealed class GCDevJsonInspectorState
{
    private const double PollIntervalSeconds = 0.25;

    private readonly GCDevJsonStore devStore;
    private readonly GCPlatformDataStore platformDataStore;
    private readonly Func<GCRootJsonFileStamp> devFileStampReader;
    private readonly Func<GCRootJsonFileStamp> platformDataFileStampReader;

    private GCDevJsonFile cleanData;
    private GCDevJsonReadResult devReadResult;
    private GCPlatformDataReadResult platformDataReadResult;
    private GCDevJsonValidationResult draftValidation;
    private GCDevJsonWriteResult lastWriteResult;
    private GCRootJsonFileStamp devFileStamp;
    private GCRootJsonFileStamp platformDataFileStamp;
    private bool hasConflict;
    private bool hasPendingPlayChange;
    private bool hasUnloadedPlayDevJsonChange;
    private double nextPollTime;

    internal GCDevJsonInspectorState()
    {
        var projectRootResolver = new GCUnityLocalProjectRootResolver();
        devStore = new GCDevJsonStore(projectRootResolver);
        platformDataStore = new GCPlatformDataStore(projectRootResolver);
        devFileStampReader = devStore.ReadFileStamp;
        platformDataFileStampReader = platformDataStore.ReadFileStamp;
        Reload();
    }

    internal GCDevJsonInspectorState(
        GCDevJsonStore devStore,
        GCPlatformDataStore platformDataStore,
        Func<GCRootJsonFileStamp> devFileStampReader,
        Func<GCRootJsonFileStamp> platformDataFileStampReader)
    {
        if (devStore == null)
        {
            throw new ArgumentNullException(nameof(devStore));
        }

        if (platformDataStore == null)
        {
            throw new ArgumentNullException(nameof(platformDataStore));
        }

        this.devStore = devStore;
        this.platformDataStore = platformDataStore;
        this.devFileStampReader = devFileStampReader ?? devStore.ReadFileStamp;
        this.platformDataFileStampReader = platformDataFileStampReader ?? platformDataStore.ReadFileStamp;
        Reload();
    }

    internal GCDevJsonDraft Draft { get; private set; }

    internal GCDevJsonReadResult DevReadResult
    {
        get { return devReadResult; }
    }

    internal GCPlatformDataReadResult PlatformDataReadResult
    {
        get { return platformDataReadResult; }
    }

    internal GCDevJsonValidationResult DraftValidation
    {
        get { return draftValidation; }
    }

    internal GCDevJsonWriteResult LastWriteResult
    {
        get { return lastWriteResult; }
    }

    internal bool HasDraft
    {
        get { return Draft != null; }
    }

    internal bool HasValidPlatformData
    {
        get { return platformDataReadResult != null && platformDataReadResult.IsValid; }
    }

    internal bool IsDirty
    {
        get { return Draft != null && !AreEqual(cleanData, Draft.ToFile()); }
    }

    internal bool HasConflict
    {
        get { return hasConflict; }
    }

    internal bool HasPendingPlayChange
    {
        get { return hasPendingPlayChange; }
    }

    internal bool CanApply
    {
        get { return Draft != null && IsDirty && !hasConflict && draftValidation != null && draftValidation.IsValid; }
    }

    internal bool CanWriteDraft
    {
        get { return Draft != null && hasConflict && draftValidation != null && draftValidation.IsValid; }
    }

    internal string DevJsonPath
    {
        get
        {
            if (devReadResult != null && devReadResult.parsedFile != null)
            {
                return devReadResult.parsedFile.path;
            }

            return devStore.ResolveFilePath();
        }
    }

    internal void Reload()
    {
        ReloadFromDisk(EditorApplication.isPlaying && hasPendingPlayChange);
    }

    internal bool PollForExternalChanges()
    {
        return PollForExternalChanges(false);
    }

    internal bool PollForExternalChanges(bool force)
    {
        var now = EditorApplication.timeSinceStartup;
        if (!force && now < nextPollTime)
        {
            return false;
        }

        nextPollTime = now + PollIntervalSeconds;

        var nextPlatformDataFileStamp = platformDataFileStampReader();
        var nextDevFileStamp = devFileStampReader();
        var platformDataChanged = nextPlatformDataFileStamp.IsExternalChangeFrom(platformDataFileStamp);
        var devChanged = nextDevFileStamp.IsExternalChangeFrom(devFileStamp);
        if (!platformDataChanged && !devChanged)
        {
            return false;
        }

        var changedState = false;
        var isPlaying = EditorApplication.isPlaying;

        if (platformDataChanged)
        {
            platformDataFileStamp = nextPlatformDataFileStamp;
            RefreshPlatformData();
            changedState = true;
            if (isPlaying)
            {
                hasPendingPlayChange = true;
            }
        }

        if (devChanged)
        {
            devFileStamp = nextDevFileStamp;
            changedState = true;
            lastWriteResult = null;

            if (isPlaying)
            {
                hasPendingPlayChange = true;
                if (IsDirty || hasConflict)
                {
                    EnterConflict();
                }
                else
                {
                    hasUnloadedPlayDevJsonChange = true;
                }
            }
            else if (IsDirty || hasConflict)
            {
                EnterConflict();
            }
            else
            {
                ReloadFromDisk(false);
            }
        }

        return changedState;
    }

    internal GCLocalPlaySessionPreflightResult PrepareForPlayBoundary(GCLocalPlaySessionBoundary context)
    {
        var validationResult = ValidateForPlayBoundary(context);
        if (!validationResult.success)
        {
            return validationResult;
        }

        var boundaryName = GCLocalPlaySession.GetBoundaryDisplayName(context);
        if (!IsDirty)
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }

        if (WriteDraftToDisk())
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }

        return GCLocalPlaySessionPreflightResult.Failed(
            boundaryName + " blocked because the current gc.dev.json inspector draft could not be auto-applied.",
            lastWriteResult != null ? lastWriteResult.path : DevJsonPath,
            GCDevJsonLocalPlaySessionProvider.MapValidation(
                lastWriteResult != null ? lastWriteResult.validation : draftValidation
            )
        );
    }

    internal GCLocalPlaySessionPreflightResult ValidateForPlayBoundary(GCLocalPlaySessionBoundary context)
    {
        PollForExternalChanges(true);
        ValidateDraft();

        var boundaryName = GCLocalPlaySession.GetBoundaryDisplayName(context);
        if (hasConflict)
        {
            return GCLocalPlaySessionPreflightResult.Failed(
                boundaryName + " blocked because the current gc.dev.json inspector draft is conflicted. Reload from disk or write the draft before continuing.",
                DevJsonPath,
                GCDevJsonLocalPlaySessionProvider.MapValidation(draftValidation)
            );
        }

        if (!IsDirty)
        {
            return GCLocalPlaySessionPreflightResult.Succeeded();
        }

        if (draftValidation == null || !draftValidation.IsValid)
        {
            return GCLocalPlaySessionPreflightResult.Failed(
                boundaryName + " blocked because the current gc.dev.json inspector draft is invalid.",
                DevJsonPath,
                GCDevJsonLocalPlaySessionProvider.MapValidation(draftValidation)
            );
        }

        return GCLocalPlaySessionPreflightResult.Succeeded();
    }

    internal bool MarkPlayChangesCaptured()
    {
        var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
        hasPendingPlayChange = false;
        hasUnloadedPlayDevJsonChange = false;

        if (!hasConflict && !IsDirty)
        {
            ReloadFromDisk(false);
            return true;
        }

        UpdateFileStamps();
        return changed;
    }

    internal bool HandlePlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
            hasPendingPlayChange = false;
            hasUnloadedPlayDevJsonChange = false;
            return changed;
        }

        if (change == PlayModeStateChange.EnteredEditMode)
        {
            var shouldReloadPendingDiskState = hasUnloadedPlayDevJsonChange && !hasConflict && !IsDirty;
            var changed = hasPendingPlayChange || hasUnloadedPlayDevJsonChange;
            hasPendingPlayChange = false;
            hasUnloadedPlayDevJsonChange = false;
            if (shouldReloadPendingDiskState)
            {
                ReloadFromDisk(false);
                return true;
            }

            return changed;
        }

        return false;
    }

    internal bool Apply()
    {
        lastWriteResult = null;
        ValidateDraft();
        if (!CanApply)
        {
            return false;
        }

        return WriteDraftToDisk();
    }

    internal bool WriteDraft()
    {
        lastWriteResult = null;
        ValidateDraft();
        if (!CanWriteDraft)
        {
            return false;
        }

        return WriteDraftToDisk();
    }

    internal GCDevJsonIssue[] GetDisplayIssues()
    {
        if (lastWriteResult != null && !lastWriteResult.success)
        {
            return GetIssues(lastWriteResult.validation);
        }

        if (Draft != null)
        {
            return GetIssues(draftValidation);
        }

        return CombineIssues(
            devReadResult != null ? devReadResult.validation : null,
            platformDataReadResult != null ? platformDataReadResult.validation : null
        );
    }

    private void ReloadFromDisk(bool preservePendingPlayChange)
    {
        var pendingPlayChange = preservePendingPlayChange && hasPendingPlayChange;
        lastWriteResult = null;
        hasConflict = false;
        hasPendingPlayChange = pendingPlayChange;
        hasUnloadedPlayDevJsonChange = false;
        platformDataReadResult = platformDataStore.Read();
        devReadResult = devStore.Read(platformDataReadResult);
        cleanData = devReadResult != null && devReadResult.data != null ? devReadResult.data.Clone() : null;
        Draft = cleanData != null ? GCDevJsonDraft.FromFile(cleanData) : null;
        ValidateDraft();
        UpdateFileStamps();
    }

    internal void NotifyDraftChanged()
    {
        lastWriteResult = null;
        if (hasUnloadedPlayDevJsonChange)
        {
            hasConflict = true;
        }

        ValidateDraft();
    }

    private bool WriteDraftToDisk()
    {
        lastWriteResult = devStore.Write(Draft.ToFile(), platformDataReadResult);
        if (lastWriteResult != null && lastWriteResult.success)
        {
            if (EditorApplication.isPlaying)
            {
                hasPendingPlayChange = true;
            }

            ReloadFromDisk(hasPendingPlayChange);
            return true;
        }

        return false;
    }

    private void RefreshPlatformData()
    {
        lastWriteResult = null;
        platformDataReadResult = platformDataStore.Read();
        if (Draft == null)
        {
            devReadResult = devStore.Read(platformDataReadResult);
            cleanData = devReadResult != null && devReadResult.data != null ? devReadResult.data.Clone() : null;
            Draft = cleanData != null ? GCDevJsonDraft.FromFile(cleanData) : null;
        }

        ValidateDraft();
    }

    private void EnterConflict()
    {
        hasConflict = true;
        lastWriteResult = null;
        ValidateDraft();
    }

    private void UpdateFileStamps()
    {
        platformDataFileStamp = platformDataFileStampReader();
        devFileStamp = devFileStampReader();
    }

    private void ValidateDraft()
    {
        if (Draft == null)
        {
            draftValidation = devReadResult != null ? devReadResult.validation : null;
            return;
        }

        draftValidation = GCDevJsonValidation.ValidateData(Draft.ToFile(), DevJsonPath, platformDataReadResult);
    }

    private static GCDevJsonIssue[] GetIssues(GCDevJsonValidationResult validation)
    {
        return validation != null && validation.issues != null ? validation.issues : new GCDevJsonIssue[0];
    }

    private static GCDevJsonIssue[] CombineIssues(GCDevJsonValidationResult first, GCDevJsonValidationResult second)
    {
        var firstIssues = GetIssues(first);
        var secondIssues = GetIssues(second);
        if (firstIssues.Length == 0)
        {
            return secondIssues;
        }

        if (secondIssues.Length == 0)
        {
            return firstIssues;
        }

        var combined = new GCDevJsonIssue[firstIssues.Length + secondIssues.Length];
        Array.Copy(firstIssues, combined, firstIssues.Length);
        Array.Copy(secondIssues, 0, combined, firstIssues.Length, secondIssues.Length);
        return combined;
    }

    private static bool AreEqual(GCDevJsonFile left, GCDevJsonFile right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        if (left.devVersion != right.devVersion ||
            !string.Equals(left.entryKey, right.entryKey, StringComparison.Ordinal) ||
            !string.Equals(left.seed, right.seed, StringComparison.Ordinal))
        {
            return false;
        }

        if (left.seats == null || right.seats == null || left.seats.Length != right.seats.Length)
        {
            return left.seats == right.seats;
        }

        for (var index = 0; index < left.seats.Length; index++)
        {
            if (!AreEqual(left.seats[index], right.seats[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool AreEqual(GCDevJsonSeat left, GCDevJsonSeat right)
    {
        if (left == null || right == null)
        {
            return left == right;
        }

        return string.Equals(left.name, right.name, StringComparison.Ordinal) &&
               left.enabled == right.enabled &&
               left.isBot == right.isBot;
    }
}
