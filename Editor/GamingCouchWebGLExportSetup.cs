using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEngine;

internal enum GCWebGLExportSetupStatus
{
    Ready,
    Warning,
    Blocked,
}

internal enum GCWebGLExportTemplateInstallStatus
{
    Ready,
    Blocked,
}

internal sealed class GCWebGLExportTemplateInstallResult
{
    internal readonly GCWebGLExportTemplateInstallStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] createdPaths;
    internal readonly string[] reusedPaths;
    internal readonly string[] blockedReasons;

    internal GCWebGLExportTemplateInstallResult(
        GCWebGLExportTemplateInstallStatus status,
        bool changed,
        string message,
        string[] createdPaths,
        string[] reusedPaths,
        string[] blockedReasons
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.createdPaths = createdPaths ?? new string[0];
        this.reusedPaths = reusedPaths ?? new string[0];
        this.blockedReasons = blockedReasons ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportTemplateInstallStatus.Blocked; }
    }
}

internal sealed class GCWebGLExportReadiness
{
    internal readonly GCWebGLExportSetupStatus status;
    internal readonly bool webGLModuleInstalled;
    internal readonly bool templateFolderReady;
    internal readonly bool templateFilesReady;
    internal readonly bool templateSelected;
    internal readonly bool releaseSettingsReady;
    internal readonly bool splashSettingsReady;
    internal readonly bool activeBuildTargetIsWebGL;
    internal readonly string message;
    internal readonly string[] details;

    // Backward-compatible overload for callers (mostly tests) that assume the WebGL Build
    // Support module is installed. Production inspection always sets the flag explicitly.
    internal GCWebGLExportReadiness(
        GCWebGLExportSetupStatus status,
        bool templateFolderReady,
        bool templateFilesReady,
        bool templateSelected,
        bool releaseSettingsReady,
        bool splashSettingsReady,
        bool activeBuildTargetIsWebGL,
        string message,
        string[] details
    )
        : this(
            status,
            true,
            templateFolderReady,
            templateFilesReady,
            templateSelected,
            releaseSettingsReady,
            splashSettingsReady,
            activeBuildTargetIsWebGL,
            message,
            details
        )
    {
    }

    internal GCWebGLExportReadiness(
        GCWebGLExportSetupStatus status,
        bool webGLModuleInstalled,
        bool templateFolderReady,
        bool templateFilesReady,
        bool templateSelected,
        bool releaseSettingsReady,
        bool splashSettingsReady,
        bool activeBuildTargetIsWebGL,
        string message,
        string[] details
    )
    {
        this.status = status;
        this.webGLModuleInstalled = webGLModuleInstalled;
        this.templateFolderReady = templateFolderReady;
        this.templateFilesReady = templateFilesReady;
        this.templateSelected = templateSelected;
        this.releaseSettingsReady = releaseSettingsReady;
        this.splashSettingsReady = splashSettingsReady;
        this.activeBuildTargetIsWebGL = activeBuildTargetIsWebGL;
        this.message = message;
        this.details = details ?? new string[0];
    }

    internal bool IsReady
    {
        get { return status == GCWebGLExportSetupStatus.Ready; }
    }

    internal bool HasWarning
    {
        get { return status == GCWebGLExportSetupStatus.Warning; }
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportSetupStatus.Blocked; }
    }
}

internal sealed class GCWebGLExportSetupResult
{
    internal readonly GCWebGLExportSetupStatus status;
    internal readonly bool changed;
    internal readonly string message;
    internal readonly string[] details;
    internal readonly GCWebGLExportReadiness readiness;

    internal GCWebGLExportSetupResult(
        GCWebGLExportSetupStatus status,
        bool changed,
        string message,
        string[] details,
        GCWebGLExportReadiness readiness
    )
    {
        this.status = status;
        this.changed = changed;
        this.message = message;
        this.details = details ?? new string[0];
        this.readiness = readiness;
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportSetupStatus.Blocked; }
    }

    internal bool HasWarning
    {
        get { return status == GCWebGLExportSetupStatus.Warning; }
    }
}

internal sealed class GCWebGLExportSetupPlan
{
    internal readonly GCWebGLExportSetupStatus status;
    internal readonly string message;
    internal readonly string sourceTemplateDirectoryFullPath;
    internal readonly string destinationTemplateDirectoryFullPath;
    internal readonly bool refreshAssetDatabase;
    internal readonly GCWebGLPreviewRow[] rows;
    internal readonly string[] details;

    internal GCWebGLExportSetupPlan(
        GCWebGLExportSetupStatus status,
        string message,
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase,
        GCWebGLPreviewRow[] rows,
        string[] details
    )
    {
        this.status = status;
        this.message = message;
        this.sourceTemplateDirectoryFullPath = sourceTemplateDirectoryFullPath;
        this.destinationTemplateDirectoryFullPath = destinationTemplateDirectoryFullPath;
        this.refreshAssetDatabase = refreshAssetDatabase;
        this.rows = rows ?? new GCWebGLPreviewRow[0];
        this.details = details ?? new string[0];
    }

    internal bool IsBlocked
    {
        get { return status == GCWebGLExportSetupStatus.Blocked; }
    }

    internal bool HasChanges
    {
        get { return GCWebGLPreviewRowQueries.HasChangedRows(rows); }
    }

    internal string[] GetDefaultSelectedSkippableRowIds()
    {
        return GCWebGLPreviewRowQueries.GetDefaultSelectedSkippableRowIds(rows);
    }
}

internal static class GamingCouchWebGLExportSetup
{
    internal const string TemplateName = "GamingCouch";
    internal const string ProjectTemplateIdentifier = "PROJECT:" + TemplateName;
    internal const string PackageTemplateAssetPath = "Editor/WebGLTemplates/" + TemplateName;
    internal const string ProjectTemplatesFolderAssetPath = "Assets/WebGLTemplates";
    internal const string ProjectTemplateAssetPath = ProjectTemplatesFolderAssetPath + "/" + TemplateName;
    internal const string TemplateSelectionRowId = "webgl-template-selection";
    internal const string ActiveBuildTargetRowId = "active-webgl-build-target";
    internal const string SplashScreenRowId = "unity-splash-screen";
    internal const string SplashLogoRowId = "unity-splash-logo";

    private static readonly string[] ExpectedTemplateFiles = { "index.html" };

    internal static GCWebGLExportSetupResult EnsureWebGLExportSetup()
    {
        var packageTemplatePath = LocatePackageTemplatePath();
        var destinationPath = AssetPathToFullPath(ProjectTemplateAssetPath);
        var plan = CreateWebGLExportSetupPlan(packageTemplatePath, destinationPath, true);
        return ApplyWebGLExportSetupPlan(plan, null);
    }

    internal static GCWebGLExportSetupResult EnsureWebGLExportSetup(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase
    )
    {
        var plan = CreateWebGLExportSetupPlan(
            sourceTemplateDirectoryFullPath,
            destinationTemplateDirectoryFullPath,
            refreshAssetDatabase
        );
        return ApplyWebGLExportSetupPlan(plan, null);
    }

    internal static GCWebGLExportSetupPlan CreateWebGLExportSetupPlan()
    {
        return CreateWebGLExportSetupPlan(
            LocatePackageTemplatePath(),
            AssetPathToFullPath(ProjectTemplateAssetPath),
            true
        );
    }

    internal static GCWebGLExportSetupPlan CreateWebGLExportSetupPlan(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase
    )
    {
        var rows = new List<GCWebGLPreviewRow>();
        var details = new List<string>();

        AddTemplatePlanRows(
            sourceTemplateDirectoryFullPath,
            destinationTemplateDirectoryFullPath,
            rows,
            details
        );
        AddTemplateSelectionPlanRow(rows, details);
        AddActiveBuildTargetPlanRow(rows, details, EditorUserBuildSettings.activeBuildTarget);
        AddSplashPlanRows(rows, details);
        AddProfilePlanRows(GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan(), rows, details);

        var status = GCWebGLPreviewRowQueries.HasBlockedRows(rows)
            ? GCWebGLExportSetupStatus.Blocked
            : GCWebGLExportSetupStatus.Ready;
        var hasChanges = GCWebGLPreviewRowQueries.HasChangedRows(rows);
        var message = status == GCWebGLExportSetupStatus.Blocked
            ? "Gaming Couch web export settings preview is blocked."
            : hasChanges
                ? "Review Gaming Couch web export settings changes before applying them."
                : "Gaming Couch web export settings are already configured.";

        return new GCWebGLExportSetupPlan(
            status,
            message,
            sourceTemplateDirectoryFullPath,
            destinationTemplateDirectoryFullPath,
            refreshAssetDatabase,
            rows.ToArray(),
            details.ToArray()
        );
    }

    internal static GCWebGLExportSetupResult ApplyWebGLExportSetupPlan(
        GCWebGLExportSetupPlan plan,
        IEnumerable<string> selectedSkippableRowIds
    )
    {
        var details = new List<string>();
        if (plan == null)
        {
            var currentReadiness = InspectReadiness();
            return new GCWebGLExportSetupResult(
                GCWebGLExportSetupStatus.Blocked,
                false,
                "Gaming Couch web export settings are blocked.",
                new[] { "No Gaming Couch web export settings plan was provided." },
                currentReadiness
            );
        }

        if (plan.IsBlocked)
        {
            return new GCWebGLExportSetupResult(
                GCWebGLExportSetupStatus.Blocked,
                false,
                "Gaming Couch web export settings are blocked.",
                plan.details,
                InspectReadiness(plan.destinationTemplateDirectoryFullPath)
            );
        }

        var installResult = InstallTemplateFiles(
            plan.sourceTemplateDirectoryFullPath,
            plan.destinationTemplateDirectoryFullPath,
            plan.refreshAssetDatabase
        );

        details.Add(installResult.message);
        details.AddRange(installResult.createdPaths);
        details.AddRange(installResult.reusedPaths);
        if (installResult.IsBlocked)
        {
            details.AddRange(installResult.blockedReasons);
            return new GCWebGLExportSetupResult(
                GCWebGLExportSetupStatus.Blocked,
                installResult.changed,
                "Gaming Couch web export settings are blocked.",
                details.ToArray(),
                InspectReadiness(plan.destinationTemplateDirectoryFullPath)
            );
        }

        var selectedIds = GamingCouchWebGLBuildSettingsProfiles.CreateSelectedIdSet(selectedSkippableRowIds);
        var changed = installResult.changed;
        changed |= SelectTemplate(details);
        changed |= ApplyWebGLExportReleaseDefaults(details, selectedIds);
        changed |= SwitchActiveBuildTargetToWebGL(details);

        var readiness = InspectReadiness(plan.destinationTemplateDirectoryFullPath);
        details.AddRange(readiness.details);

        var status = readiness.status;
        var message = readiness.IsReady
            ? "Gaming Couch web export settings are ready."
            : readiness.message;
        if (readiness.IsBlocked && RemainingGapsAreDeliberateSkips(readiness, selectedIds))
        {
            status = GCWebGLExportSetupStatus.Warning;
            message = "Gaming Couch web export settings were applied; skipped rows were left unapplied.";
        }

        return new GCWebGLExportSetupResult(
            status,
            changed,
            message,
            details.ToArray(),
            readiness
        );
    }

    // Rows the caller deselected in the preview are meant to stay unapplied, so the gaps they
    // leave behind must not surface as a failed apply. Readiness itself stays honest — its flags
    // and details still report the gaps, so the start screen keeps asking for the missing
    // settings; only the status the caller sees for this apply run softens to a warning.
    // Template and build-target rows are never skippable, so only the settings rows can be
    // left behind on purpose.
    private static bool RemainingGapsAreDeliberateSkips(
        GCWebGLExportReadiness readiness,
        HashSet<string> selectedIds
    )
    {
        if (selectedIds == null ||
            !readiness.templateFolderReady ||
            !readiness.templateFilesReady ||
            !readiness.templateSelected)
        {
            return false;
        }

        var settingsRows = new List<GCWebGLPreviewRow>();
        AddSplashPlanRows(settingsRows, null);
        AddProfilePlanRows(
            GamingCouchWebGLBuildSettingsProfiles.BuildReleaseProfilePlan(),
            settingsRows,
            null
        );

        var hasDeliberateSkip = false;
        for (var index = 0; index < settingsRows.Count; index++)
        {
            var row = settingsRows[index];
            if (!row.isChanged)
            {
                continue;
            }

            if (!row.isSkippable || selectedIds.Contains(row.id))
            {
                return false;
            }

            hasDeliberateSkip = true;
        }

        return hasDeliberateSkip;
    }

    private static void AddTemplatePlanRows(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        List<GCWebGLPreviewRow> rows,
        List<string> details
    )
    {
        if (string.IsNullOrEmpty(sourceTemplateDirectoryFullPath) ||
            !Directory.Exists(sourceTemplateDirectoryFullPath))
        {
            AddPlanRow(
                rows,
                details,
                new GCWebGLPreviewRow(
                    "web-export-template-source",
                    GCWebGLPreviewRowKind.Template,
                    "Package Gaming Couch web export template source",
                    "Missing",
                    "Available package folder",
                    true,
                    false,
                    true
                )
            );
            return;
        }

        foreach (var fileName in ExpectedTemplateFiles)
        {
            var sourcePath = Path.Combine(sourceTemplateDirectoryFullPath, fileName);
            if (Directory.Exists(sourcePath))
            {
                AddPlanRow(
                    rows,
                    details,
                new GCWebGLPreviewRow(
                        "web-export-template-source-" + fileName,
                        GCWebGLPreviewRowKind.Template,
                        "Package Gaming Couch web export template file " + fileName,
                        "Folder",
                        "File",
                        true,
                        false,
                        true
                    )
                );
            }
            else if (!File.Exists(sourcePath))
            {
                AddPlanRow(
                    rows,
                    details,
                new GCWebGLPreviewRow(
                        "web-export-template-source-" + fileName,
                        GCWebGLPreviewRowKind.Template,
                        "Package Gaming Couch web export template file " + fileName,
                        "Missing",
                        "File",
                        true,
                        false,
                        true
                    )
                );
            }
        }

        if (string.IsNullOrEmpty(destinationTemplateDirectoryFullPath))
        {
            AddPlanRow(
                rows,
                details,
                new GCWebGLPreviewRow(
                    "web-export-template-destination",
                    GCWebGLPreviewRowKind.Template,
                    "Project-local web export template folder",
                    "Missing path",
                    ProjectTemplateAssetPath,
                    true,
                    false,
                    true
                )
            );
            return;
        }

        var fullDestinationPath = Path.GetFullPath(destinationTemplateDirectoryFullPath);
        if (File.Exists(fullDestinationPath))
        {
            AddPlanRow(
                rows,
                details,
                new GCWebGLPreviewRow(
                    "web-export-template-folder",
                    GCWebGLPreviewRowKind.Template,
                    "Project-local web export template folder",
                    "File",
                    "Folder",
                    true,
                    false,
                    true
                )
            );
            return;
        }

        var parentCollisionPath = FindTemplateFolderParentFileCollision(fullDestinationPath);
        if (!string.IsNullOrEmpty(parentCollisionPath))
        {
            AddPlanRow(
                rows,
                details,
                new GCWebGLPreviewRow(
                    "web-export-template-parent-folder",
                    GCWebGLPreviewRowKind.Template,
                    "Project-local web export template parent folder",
                    "File at " + parentCollisionPath,
                    "Folder",
                    true,
                    false,
                    true
                )
            );
            return;
        }

        if (Directory.Exists(fullDestinationPath))
        {
            GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Project-local web export template folder already exists: " + fullDestinationPath);
        }
        else
        {
            AddPlanRow(
                rows,
                details,
                new GCWebGLPreviewRow(
                    "web-export-template-folder",
                    GCWebGLPreviewRowKind.Template,
                    "Project-local web export template folder",
                    "Missing",
                    "Create folder",
                    true,
                    false
                )
            );
        }

        foreach (var fileName in ExpectedTemplateFiles)
        {
            var destinationFilePath = Path.Combine(fullDestinationPath, fileName);
            if (Directory.Exists(destinationFilePath))
            {
                AddPlanRow(
                    rows,
                    details,
                new GCWebGLPreviewRow(
                        "web-export-template-file-" + fileName,
                        GCWebGLPreviewRowKind.Template,
                        "Project-local web export template file " + fileName,
                        "Folder",
                        "File from package",
                        true,
                        false,
                        true
                    )
                );
            }
            else if (File.Exists(destinationFilePath))
            {
                GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Project-local web export template file will be reused: " + destinationFilePath);
            }
            else
            {
                AddPlanRow(
                    rows,
                    details,
                    new GCWebGLPreviewRow(
                        "web-export-template-file-" + fileName,
                        GCWebGLPreviewRowKind.Template,
                        "Project-local web export template file " + fileName,
                        "Missing",
                        "Install package template file",
                        true,
                        false
                    )
                );
            }
        }
    }

    private static void AddTemplateSelectionPlanRow(List<GCWebGLPreviewRow> rows, List<string> details)
    {
        AddPlanRow(
            rows,
            details,
            new GCWebGLPreviewRow(
                TemplateSelectionRowId,
                GCWebGLPreviewRowKind.Template,
                "Web export template selection",
                PlayerSettings.WebGL.template,
                ProjectTemplateIdentifier,
                !string.Equals(PlayerSettings.WebGL.template, ProjectTemplateIdentifier, StringComparison.Ordinal),
                false
            )
        );
    }

    private static void AddActiveBuildTargetPlanRow(
        List<GCWebGLPreviewRow> rows,
        List<string> details,
        BuildTarget activeBuildTarget
    )
    {
        AddPlanRow(
            rows,
            details,
            new GCWebGLPreviewRow(
                ActiveBuildTargetRowId,
                GCWebGLPreviewRowKind.BuildTarget,
                "Active build target",
                activeBuildTarget.ToString(),
                BuildTarget.WebGL.ToString(),
                activeBuildTarget != BuildTarget.WebGL,
                false
            )
        );
    }

    private static void AddSplashPlanRows(List<GCWebGLPreviewRow> rows, List<string> details)
    {
        AddPlanRow(
            rows,
            details,
            new GCWebGLPreviewRow(
                SplashScreenRowId,
                GCWebGLPreviewRowKind.Splash,
                "Unity splash screen",
                GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.show),
                GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(false),
                PlayerSettings.SplashScreen.show,
                true
            )
        );
        AddPlanRow(
            rows,
            details,
            new GCWebGLPreviewRow(
                SplashLogoRowId,
                GCWebGLPreviewRowKind.Splash,
                "Unity splash logo",
                GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.showUnityLogo),
                GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(false),
                PlayerSettings.SplashScreen.showUnityLogo,
                true
            )
        );
    }

    private static void AddProfilePlanRows(
        GCWebGLBuildSettingsProfilePlan profilePlan,
        List<GCWebGLPreviewRow> rows,
        List<string> details
    )
    {
        if (profilePlan == null || profilePlan.rows == null)
        {
            return;
        }

        for (var index = 0; index < profilePlan.rows.Length; index++)
        {
            AddPlanRow(rows, details, profilePlan.rows[index]);
        }
    }

    private static void AddPlanRow(
        List<GCWebGLPreviewRow> rows,
        List<string> details,
        GCWebGLPreviewRow row
    )
    {
        if (row == null)
        {
            return;
        }

        rows.Add(row);
        if (row.isChanged)
        {
            GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, row.DiffText);
        }
    }

    private static string FindTemplateFolderParentFileCollision(string destinationTemplateDirectoryFullPath)
    {
        var currentPath = destinationTemplateDirectoryFullPath;
        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                return currentPath;
            }

            currentPath = Path.GetDirectoryName(currentPath);
        }

        return null;
    }

    internal static GCWebGLExportTemplateInstallResult InstallTemplateFiles(
        string sourceTemplateDirectoryFullPath,
        string destinationTemplateDirectoryFullPath,
        bool refreshAssetDatabase
    )
    {
        var createdPaths = new List<string>();
        var reusedPaths = new List<string>();
        var blockedReasons = new List<string>();

        if (string.IsNullOrEmpty(sourceTemplateDirectoryFullPath) ||
            !Directory.Exists(sourceTemplateDirectoryFullPath))
        {
            blockedReasons.Add("Template source folder does not exist: " + sourceTemplateDirectoryFullPath);
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Gaming Couch web export template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        if (string.IsNullOrEmpty(destinationTemplateDirectoryFullPath))
        {
            blockedReasons.Add("Template destination folder path is empty.");
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Gaming Couch web export template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        ValidateTemplateSourceFiles(sourceTemplateDirectoryFullPath, blockedReasons);
        ValidateTemplateDestinationPaths(destinationTemplateDirectoryFullPath, blockedReasons);
        if (blockedReasons.Count > 0)
        {
            return CreateInstallResult(
                GCWebGLExportTemplateInstallStatus.Blocked,
                false,
                "Gaming Couch web export template installation is blocked.",
                createdPaths,
                reusedPaths,
                blockedReasons
            );
        }

        // Installation is reached from OnGUI, where a filesystem failure (a read-only Assets
        // folder, a locked file) must surface as a blocked reason rather than throwing out of the
        // repaint.
        try
        {
            EnsureDirectory(destinationTemplateDirectoryFullPath, createdPaths, reusedPaths, blockedReasons);
            if (blockedReasons.Count == 0)
            {
                foreach (var fileName in ExpectedTemplateFiles)
                {
                    EnsureTemplateFile(
                        Path.Combine(sourceTemplateDirectoryFullPath, fileName),
                        Path.Combine(destinationTemplateDirectoryFullPath, fileName),
                        createdPaths,
                        reusedPaths,
                        blockedReasons
                    );
                }
            }
        }
        catch (Exception exception)
        {
            blockedReasons.Add(
                "Could not install the Gaming Couch web export template into " +
                destinationTemplateDirectoryFullPath +
                ": " +
                exception.Message
            );
        }

        var changed = createdPaths.Count > 0;
        if (changed && refreshAssetDatabase)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        return CreateInstallResult(
            blockedReasons.Count > 0
                ? GCWebGLExportTemplateInstallStatus.Blocked
                : GCWebGLExportTemplateInstallStatus.Ready,
            changed,
            blockedReasons.Count > 0
                ? "Gaming Couch web export template installation is blocked."
                : changed
                    ? "Installed missing Gaming Couch web export template files."
                    : "Gaming Couch web export template files already exist; existing files were reused.",
            createdPaths,
            reusedPaths,
            blockedReasons
        );
    }

    internal static GCWebGLExportReadiness InspectReadiness()
    {
        return InspectReadiness(AssetPathToFullPath(ProjectTemplateAssetPath));
    }

    internal static GCWebGLExportReadiness InspectReadiness(string destinationTemplateDirectoryFullPath)
    {
        return InspectReadiness(destinationTemplateDirectoryFullPath, EditorUserBuildSettings.activeBuildTarget);
    }

    internal static GCWebGLExportReadiness InspectReadiness(
        string destinationTemplateDirectoryFullPath,
        BuildTarget activeBuildTarget
    )
    {
        var details = new List<string>();
        var webGLModuleInstalled = IsWebGLModuleInstalled();
        var templateFolderPathIsEmpty = string.IsNullOrEmpty(destinationTemplateDirectoryFullPath);
        var templateFolderIsWrongKind = !templateFolderPathIsEmpty &&
                                        File.Exists(destinationTemplateDirectoryFullPath);
        var templateFolderReady = !templateFolderPathIsEmpty &&
                                  Directory.Exists(destinationTemplateDirectoryFullPath);
        if (templateFolderIsWrongKind)
        {
            details.Add("Project-local template path is a file, expected a folder: " + destinationTemplateDirectoryFullPath);
        }
        else if (!templateFolderReady)
        {
            details.Add("Project-local template folder is missing: " + ProjectTemplateAssetPath);
        }

        var templateFilesReady = templateFolderReady;
        if (templateFolderReady)
        {
            foreach (var fileName in ExpectedTemplateFiles)
            {
                var filePath = Path.Combine(destinationTemplateDirectoryFullPath, fileName);
                if (Directory.Exists(filePath))
                {
                    templateFilesReady = false;
                    details.Add("Project-local template path is a folder, expected a file: " + filePath);
                }
                else if (!File.Exists(filePath))
                {
                    templateFilesReady = false;
                    details.Add("Project-local template file is missing: " + ProjectTemplateAssetPath + "/" + fileName);
                }
            }
        }

        var templateSelected = string.Equals(
            PlayerSettings.WebGL.template,
            ProjectTemplateIdentifier,
            StringComparison.Ordinal
        );
        if (!templateSelected)
        {
            details.Add("Selected WebGL template is '" + PlayerSettings.WebGL.template + "', expected '" + ProjectTemplateIdentifier + "'.");
        }

        var releaseSettingsReady = AreReleaseDefaultsApplied(details);
        var splashSettingsReady = AreSplashSettingsApplied(details);
        var activeBuildTargetIsWebGL = activeBuildTarget == BuildTarget.WebGL;
        if (!activeBuildTargetIsWebGL)
        {
            details.Add("Active build target is " + activeBuildTarget + "; run Gaming Couch web export settings or switch to WebGL before building.");
        }

        var blockingReady = templateFolderReady &&
                            templateFilesReady &&
                            templateSelected &&
                            releaseSettingsReady &&
                            splashSettingsReady;
        if (!blockingReady)
        {
            return new GCWebGLExportReadiness(
                GCWebGLExportSetupStatus.Blocked,
                webGLModuleInstalled,
                templateFolderReady,
                templateFilesReady,
                templateSelected,
                releaseSettingsReady,
                splashSettingsReady,
                activeBuildTargetIsWebGL,
                "Gaming Couch web export settings are incomplete.",
                details.ToArray()
            );
        }

        return new GCWebGLExportReadiness(
            activeBuildTargetIsWebGL
                ? GCWebGLExportSetupStatus.Ready
                : GCWebGLExportSetupStatus.Warning,
            webGLModuleInstalled,
            templateFolderReady,
            templateFilesReady,
            templateSelected,
            releaseSettingsReady,
            splashSettingsReady,
            activeBuildTargetIsWebGL,
            activeBuildTargetIsWebGL
                ? "Gaming Couch web export settings are ready."
                : "Gaming Couch web export settings are ready, but the active build target is not WebGL.",
            details.ToArray()
        );
    }

    // WebGL Build Support is an optional Unity Hub module and is not installed by default.
    // Without it the WebGL build target is unavailable: switching to it fails and builds error
    // out with "Build Target WebGL not supported". Public API since Unity 2021.2 (Unity 6 here).
    internal static bool IsWebGLModuleInstalled()
    {
        return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL);
    }

    internal static string LocatePackageTemplatePath()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GamingCouchWebGLExportSetup).Assembly);
        if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
        {
            var packageTemplatePath = Path.Combine(packageInfo.resolvedPath, PackageTemplateAssetPath);
            if (Directory.Exists(packageTemplatePath))
            {
                return packageTemplatePath;
            }
        }

        var scriptPath = LocateOwnScriptAssetPath();
        if (!string.IsNullOrEmpty(scriptPath))
        {
            var editorFolderPath = Path.GetFullPath(Path.Combine(AssetPathToFullPath(scriptPath), ".."));
            var templatePath = Path.Combine(editorFolderPath, "WebGLTemplates", TemplateName);
            if (Directory.Exists(templatePath))
            {
                return templatePath;
            }
        }

        var workingTreeTemplatePath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), PackageTemplateAssetPath));
        return Directory.Exists(workingTreeTemplatePath) ? workingTreeTemplatePath : null;
    }

    private static bool SelectTemplate(List<string> details)
    {
        if (string.Equals(PlayerSettings.WebGL.template, ProjectTemplateIdentifier, StringComparison.Ordinal))
        {
            details.Add("Web export template selection already uses " + ProjectTemplateIdentifier + ".");
            return false;
        }

        PlayerSettings.WebGL.template = ProjectTemplateIdentifier;
        details.Add("Selected web export template " + ProjectTemplateIdentifier + ".");
        return true;
    }

    private static bool ApplyWebGLExportReleaseDefaults(List<string> details, HashSet<string> selectedIds)
    {
        var changed = false;
        changed |= GamingCouchWebGLBuildSettingsProfiles.ApplyReleaseProfile(details, selectedIds);
        changed |= SetSplashScreen(false, false, details, selectedIds);

        return changed;
    }

    private static bool SwitchActiveBuildTargetToWebGL(List<string> details)
    {
        if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
        {
            details.Add("Active build target already uses WebGL.");
            return false;
        }

        if (!IsWebGLModuleInstalled())
        {
            details.Add("Cannot switch to Web (WebGL) because Web Build Support is not installed. Install it from Unity Hub (Add modules) and reopen the project.");
            return false;
        }

        var previousBuildTarget = EditorUserBuildSettings.activeBuildTarget;
        if (EditorUserBuildSettings.SwitchActiveBuildTarget(NamedBuildTarget.WebGL, BuildTarget.WebGL) &&
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
        {
            details.Add("Switched active build target from " + previousBuildTarget + " to WebGL.");
            return true;
        }

        details.Add("Could not switch active build target from " + previousBuildTarget + " to WebGL.");
        return false;
    }

    private static bool AreReleaseDefaultsApplied(List<string> details)
    {
        return GamingCouchWebGLBuildSettingsProfiles.IsReleaseProfileApplied(details);
    }

    private static bool AreSplashSettingsApplied(List<string> details)
    {
        var ready = true;
        ready &= Expect(
            !PlayerSettings.SplashScreen.show,
            "Unity splash screen: " +
            GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.show) +
            " -> " +
            GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(false),
            details
        );
        ready &= Expect(
            !PlayerSettings.SplashScreen.showUnityLogo,
            "Unity splash logo: " +
            GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.showUnityLogo) +
            " -> " +
            GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(false),
            details
        );
        return ready;
    }

    private static void EnsureDirectory(
        string directoryPath,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        var fullDirectoryPath = Path.GetFullPath(directoryPath);
        if (Directory.Exists(fullDirectoryPath))
        {
            reusedPaths.Add("Reused folder: " + fullDirectoryPath);
            return;
        }

        var missingDirectories = new Stack<string>();
        var currentPath = fullDirectoryPath;
        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                blockedReasons.Add("Expected a folder, but a file already exists: " + currentPath);
                return;
            }

            missingDirectories.Push(currentPath);
            currentPath = Path.GetDirectoryName(currentPath);
        }

        while (missingDirectories.Count > 0)
        {
            var directoryToCreate = missingDirectories.Pop();
            Directory.CreateDirectory(directoryToCreate);
            createdPaths.Add("Created folder: " + directoryToCreate);
        }
    }

    private static void ValidateTemplateSourceFiles(
        string sourceTemplateDirectoryFullPath,
        List<string> blockedReasons
    )
    {
        foreach (var fileName in ExpectedTemplateFiles)
        {
            var sourcePath = Path.Combine(sourceTemplateDirectoryFullPath, fileName);
            if (Directory.Exists(sourcePath))
            {
                blockedReasons.Add("Expected template source file, but a folder exists: " + sourcePath);
            }
            else if (!File.Exists(sourcePath))
            {
                blockedReasons.Add("Expected template source file is missing: " + sourcePath);
            }
        }
    }

    private static void ValidateTemplateDestinationPaths(
        string destinationTemplateDirectoryFullPath,
        List<string> blockedReasons
    )
    {
        var fullDirectoryPath = Path.GetFullPath(destinationTemplateDirectoryFullPath);
        if (File.Exists(fullDirectoryPath))
        {
            blockedReasons.Add("Expected a folder, but a file already exists: " + fullDirectoryPath);
            return;
        }

        var currentPath = fullDirectoryPath;
        while (!string.IsNullOrEmpty(currentPath) && !Directory.Exists(currentPath))
        {
            if (File.Exists(currentPath))
            {
                blockedReasons.Add("Expected a folder, but a file already exists: " + currentPath);
                return;
            }

            currentPath = Path.GetDirectoryName(currentPath);
        }

        foreach (var fileName in ExpectedTemplateFiles)
        {
            var destinationPath = Path.Combine(fullDirectoryPath, fileName);
            if (Directory.Exists(destinationPath))
            {
                blockedReasons.Add("Expected a file, but a folder already exists: " + destinationPath);
            }
        }
    }

    private static void EnsureTemplateFile(
        string sourcePath,
        string destinationPath,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        if (!File.Exists(sourcePath))
        {
            blockedReasons.Add("Expected template source file is missing: " + sourcePath);
            return;
        }

        if (Directory.Exists(destinationPath))
        {
            blockedReasons.Add("Expected a file, but a folder already exists: " + destinationPath);
            return;
        }

        if (File.Exists(destinationPath))
        {
            reusedPaths.Add("Reused file: " + destinationPath);
            if (!MatchesPackageTemplateFile(sourcePath, destinationPath))
            {
                reusedPaths.Add("Reused file differs from package template: " + destinationPath);
            }

            return;
        }

        File.Copy(sourcePath, destinationPath, false);
        createdPaths.Add("Created file: " + destinationPath);
    }

    // The user's own file always wins, so drift after a package upgrade is only reported, never
    // repaired. A file that cannot be read is reported as matching so an unreadable path does not
    // masquerade as drift.
    private static bool MatchesPackageTemplateFile(string sourcePath, string destinationPath)
    {
        try
        {
            var sourceBytes = File.ReadAllBytes(sourcePath);
            var destinationBytes = File.ReadAllBytes(destinationPath);
            if (sourceBytes.Length != destinationBytes.Length)
            {
                return false;
            }

            for (var index = 0; index < sourceBytes.Length; index++)
            {
                if (sourceBytes[index] != destinationBytes[index])
                {
                    return false;
                }
            }

            return true;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static GCWebGLExportTemplateInstallResult CreateInstallResult(
        GCWebGLExportTemplateInstallStatus status,
        bool changed,
        string message,
        List<string> createdPaths,
        List<string> reusedPaths,
        List<string> blockedReasons
    )
    {
        return new GCWebGLExportTemplateInstallResult(
            status,
            changed,
            message,
            createdPaths.ToArray(),
            reusedPaths.ToArray(),
            blockedReasons.ToArray()
        );
    }

    private static bool SetSplashScreen(
        bool showSplash,
        bool showUnityLogo,
        List<string> details,
        HashSet<string> selectedIds
    )
    {
        var changed = false;
        if (PlayerSettings.SplashScreen.show != showSplash)
        {
            var detail = "Unity splash screen: " +
                         GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.show) +
                         " -> " +
                         GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(showSplash);
            if (selectedIds != null && !selectedIds.Contains(SplashScreenRowId))
            {
                GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Skipped " + detail + ".");
            }
            else
            {
                PlayerSettings.SplashScreen.show = showSplash;
                GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Applied " + detail + ".");
                changed = true;
            }
        }

        if (PlayerSettings.SplashScreen.showUnityLogo != showUnityLogo)
        {
            var detail = "Unity splash logo: " +
                         GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(PlayerSettings.SplashScreen.showUnityLogo) +
                         " -> " +
                         GamingCouchWebGLBuildSettingsProfiles.FormatEnabled(showUnityLogo);
            if (selectedIds != null && !selectedIds.Contains(SplashLogoRowId))
            {
                GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Skipped " + detail + ".");
            }
            else
            {
                PlayerSettings.SplashScreen.showUnityLogo = showUnityLogo;
                GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, "Applied " + detail + ".");
                changed = true;
            }
        }

        return changed;
    }

    private static bool Expect(bool condition, string detail, List<string> details)
    {
        if (condition)
        {
            return true;
        }

        GamingCouchWebGLBuildSettingsProfiles.AddDetail(details, detail);
        return false;
    }

    private static string LocateOwnScriptAssetPath()
    {
        var scriptGuids = AssetDatabase.FindAssets("GamingCouchWebGLExportSetup t:Script");
        for (var index = 0; index < scriptGuids.Length; index++)
        {
            var path = AssetDatabase.GUIDToAssetPath(scriptGuids[index]);
            if (path.EndsWith("GamingCouchWebGLExportSetup.cs", StringComparison.Ordinal))
            {
                return path;
            }
        }

        return null;
    }

    private static string AssetPathToFullPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return null;
        }

        if (Path.IsPathRooted(assetPath))
        {
            return Path.GetFullPath(assetPath);
        }

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }
}
