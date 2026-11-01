using System;
using System.Collections.Generic;
using System.Text;
using DATA_ASSEMBLY.Distributable;
using DATA_ASSEMBLY.DistributableConfig;
using FriendShapedPlugin;
using Godot;
using SecretPlan.Generated;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;
using SecretPlanGodot.Navigation;
using SecretPlanGodot.Serialization;

namespace FriendShapedDistributable;

public class CoreState
{
    public delegate void LoadSceneRequestDelegate(CachedPackedScene<Node> scene, Action<Node> onLoad);

    private readonly Dictionary<int, ShaderMaterial> _materialsByPage = new();

    public CoreState()
    {
        LocalClient.Print("Core State generated!");
        NavigationSystem = new NavigationSystem(new BoolProvider(() => Debug.LogUiNavigation));
    }

    public SerializedState<SaveFile, Settings> SerializedState { get; } =
        new(UserDataMigration.LoadSave, UserDataMigration.LoadSettings);

    public StringProvider ConsoleLogHistory { get; } = new();
    public MouseLock MouseLock { get; } = new();
    public CoreDebug Debug { get; } = new();
    public NavigationSystem NavigationSystem { get; }
    public LoadingStatus LoadingStatus { get; } = new();
    public PopupManager PopupManager { get; } = new();
    public bool IsGameInFocus { get; set; } = true;
    public WorldState? CurrentWorldState { get; private set; }

    public void GetLogs()
    {
        var writer = new ZipPacker();
        var zipFileName = $"logs_{DateTime.UtcNow.ToFileTimeUtc()}.zip";
        var zipUserPath = $"user://{zipFileName}";
        var err = writer.Open(zipUserPath);
        if (err != Error.Ok)
        {
            LocalClient.Error($"Failed to write zip: {err}");
            return;
        }

        var files = CommonSerializationConstants.AppDataFiles;
        foreach (var path in files.GetFilesAt("."))
        {
            if (path.EndsWith(".zip"))
            {
                // prevents us from reading the file we're currently writing to
                continue;
            }

            if (path.StartsWith("vulkan") || path.StartsWith("Telemetry") || path.StartsWith("shader_cache") ||
                path.StartsWith("Exports") || path.StartsWith("objectdb_snapshots"))
            {
                continue;
            }

            if (path.StartsWith("Photos/"))
            {
                continue;
            }

            if (path.Contains("godot.log"))
            {
                // this means we lose the most recent log, so we'll need some other way to get it.
                continue;
            }

            writer.StartFile(path);
            writer.WriteFile(files.ReadBytes(path));
            writer.CloseFile();
        }

        writer.StartFile("logs/MESSAGE_HISTORY.log");
        writer.WriteFile(Encoding.UTF8.GetBytes(ConsoleLogHistory));
        writer.CloseFile();

        writer.Close();

        LocalClient.Print($"Wrote logs to: {zipFileName}");

        GodotUtilities.ShellShowFile(ProjectSettings.GlobalizePath(zipUserPath));
    }

    public void OpenSaveData()
    {
        GodotUtilities.ShellOpen(ProjectSettings.GlobalizePath("user://"));
    }

    public TranslatedString TranslatedFromId(LocalizationTableIds id, params object[] templateArguments)
    {
        return LocalizationServer.Instance.GetReferenceFromId((uint)id)
            .Translated(SerializedState.Settings.Locale, templateArguments);
    }

    public TranslatedString Translated(string slug, params object[] templateArguments)
    {
        return SerializationConstants.Translated(slug, SerializedState.Settings.Locale, templateArguments);
    }

    public TranslatedString TranslatedFromReference(LocalizedStringReference reference,
        params object[] templateArguments)
    {
        return reference.Translated(SerializedState.Settings.Locale, templateArguments);
    }

    public void LoadScene(CachedPackedScene<Node> cachedPackedScene, Action<Node>? action = null)
    {
        void DoNothing(Node node)
        {
        }

        LoadSceneRequested?.Invoke(cachedPackedScene, action ?? DoNothing);
    }

    public event LoadSceneRequestDelegate? LoadSceneRequested;

    public IEnumerable<SecretPlanDebugger> AllDebuggers()
    {
        yield return Debug;
        if (CurrentWorldState != null)
        {
            yield return CurrentWorldState.Debug;
        }
    }

    /// <summary>
    ///     If the given state is our current world state, set current to null, otherwise nothing
    /// </summary>
    public void ClearCurrentWorldState(WorldState state)
    {
        if (CurrentWorldState == state)
        {
            CurrentWorldState = null;
        }
    }

    public void SetCurrentWorldState(WorldState state)
    {
        CurrentWorldState = state;
    }

    /// <summary>
    ///     Returns true if the player generally should have control at the bottom level
    /// </summary>
    public bool PlayerUiHasControl()
    {
        if (!IsGameInFocus)
        {
            return false;
        }

        if (PopupManager.IsPopupOpen)
        {
            return false;
        }

        if (Debug.IsConsoleOpen)
        {
            return false;
        }

        return true;
    }
}