using System.Runtime.Loader;
using ControlRoom.Missions;
using ControlRoomLib.Core;
using ControlRoomLib.Programs;
using SecretPlanCore.ArgumentParsing;
using SecretPlanCore.Core;


// -------------------------------------------------------------- //
Platform.Startup();

if (await ControlRoomConstants.CheckLocalIsLatest())
{
    var remainingArgs = new List<string>();
    remainingArgs.AddRange(args);

    var missionVariables = ArgumentBundle.Apply(Platform.SerializedState.MissionVariables with { }, remainingArgs);
    var gameDirectory = missionVariables.GameDirectory;
    if (gameDirectory != null)
    {
        var dataAssemblyName = $"{gameDirectory}Data";
        await OutPipe.LogMessage($"Building project: {dataAssemblyName}");
        var dotnet = new ProgramDotnet();
        await dotnet.BuildProjectAtPath($"./Data/{dataAssemblyName}");

        if (dotnet.MostRecentExitCode != 0)
        {
            throw new MissionFailedException("Could not build");
        }

        await OutPipe.LogMessage($"Loading assembly: {dataAssemblyName}");
        // Load from memory rather than LoadFrom so the dll isn't file-locked while the REPL is running (otherwise game builds can't overwrite it)
        var dataAssemblyPath = $"./Data/{dataAssemblyName}/bin/Debug/net8.0/{dataAssemblyName}";
        using var dllStream = new MemoryStream(File.ReadAllBytes($"{dataAssemblyPath}.dll"));
        using var pdbStream = File.Exists($"{dataAssemblyPath}.pdb") ? new MemoryStream(File.ReadAllBytes($"{dataAssemblyPath}.pdb")) : null;
        AssemblyLoadContext.Default.LoadFromStream(dllStream, pdbStream);
    }

    if (!remainingArgs.IsValidIndex(0))
    {
        await Platform.AttemptToRunMissionWithArgs([nameof(Repl)]);
    }
    else
    {
        await Platform.AttemptToRunMissionWithArgs(remainingArgs);
    }
}

Platform.Shutdown();

// -------------------------------------------------------------- //

