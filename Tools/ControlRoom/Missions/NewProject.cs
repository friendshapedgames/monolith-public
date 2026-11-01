using System.Text;
using ControlRoomLib.Core;
using ControlRoomLib.Missions;
using ControlRoomLib.Programs;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class NewProject : Mission
{
    public NewProject(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var newAssemblyName = PositionalArgs.Get(0, "Assembly Name").ParseAsString();
        var dataAssemblyName = newAssemblyName + "Data";

        if (Directory.Exists(newAssemblyName))
        {
            throw new MissionFailedException($"Directory already exists: {newAssemblyName}");
        }

        var projectDirectory = new RealFileSystem(newAssemblyName);

        var copyToRootFiles = ControlRoomConstants.WorkingDirectoryFiles.GetDirectory("Templates/Godot/CopyToRoot");
        foreach (var path in copyToRootFiles.GetFilesAt("."))
        {
            var content = await copyToRootFiles.ReadFileAsync(path);
            await projectDirectory.WriteToFileAsync(path, content.Replace("DATA_ASSEMBLY", dataAssemblyName));
        }

        var defaultAssetsFiles = ControlRoomConstants.WorkingDirectoryFiles.GetDirectory("Templates/Godot/DefaultAssets");
        foreach (var path in defaultAssetsFiles.GetFilesAt("."))
        {
            var content = defaultAssetsFiles.ReadBytes(path);
            projectDirectory.WriteToFileBytes(Path.Join("DefaultAssets", path), content);
        }

        await ApplyAssemblyToTemplateAndCopy("project.godot", newAssemblyName);
        await ApplyAssemblyToTemplateAndCopy("export_presets.cfg", newAssemblyName);

        var sampleCsFile = new StringBuilder();
        sampleCsFile.AppendLine($"namespace {newAssemblyName};");
        sampleCsFile.AppendLine();
        sampleCsFile.AppendLine("public class Sample");
        sampleCsFile.AppendLine("{");
        sampleCsFile.AppendLine();
        sampleCsFile.AppendLine("}");

        await projectDirectory.WriteToFileAsync($"{newAssemblyName}/Sample.cs", sampleCsFile.ToString());

        VersionChange.WriteVersionFile(projectDirectory, new SecretPlanVersion());

        await OutPipe.LogMessage("Please run Project > Tools > C# > Create C# Solution");

        var godot = new ProgramGodotLatest(newAssemblyName);
        await godot.Run("--editor");

        if (!projectDirectory.HasFile($"{newAssemblyName}.sln"))
        {
            throw new MissionFailedException($"Could not find {newAssemblyName}.sln");
        }

        if (!projectDirectory.HasFile($"{newAssemblyName}.csproj"))
        {
            throw new MissionFailedException($"Could not find {newAssemblyName}.csproj");
        }

        var gameDotnet = new ProgramDotnet(newAssemblyName);

        await AddReference(gameDotnet, "../Packages/ExTween/ExTween.csproj");

        await AddReference(gameDotnet, "../Packages/SecretPlanCore/SecretPlanCore.csproj");
        
        await AddReference(gameDotnet, "../Packages/SecretPlanGodot/SecretPlanGodot.csproj");
        
        var dataProjectFiles = new RealFileSystem(Path.Join("Data", dataAssemblyName));
        var dataDotnet = new ProgramDotnet(dataProjectFiles.GetCurrentDirectory());

        await dataDotnet.CreateNewClassLib(DotnetVersion.Net8);
        
        dataProjectFiles.DeleteFile("Class1.cs");
        
        await dataDotnet.CreateNewSolution();
        
        // add own csproj to sln
        await dataDotnet.AddProjectToCurrentSolution($"{dataAssemblyName}.csproj");
        
        var copyToDataFiles = ControlRoomConstants.WorkingDirectoryFiles.GetDirectory("Templates/Godot/CopyToData");
        foreach (var path in copyToDataFiles.GetFilesAt("."))
        {
            var content = await copyToDataFiles.ReadFileAsync(path);
            await dataProjectFiles.WriteToFileAsync(path, content.Replace("DATA_ASSEMBLY", dataAssemblyName));
        }

        await AddReference(dataDotnet, $"../../Tools/ControlRoomLib/ControlRoomLib.csproj");
        
        await AddReference(gameDotnet, $"../Data/{dataAssemblyName}/{dataAssemblyName}.csproj");

        var controlRoom = new ProgramControlRoom(".");

        await controlRoom.RunMission<ConfigManagement>($"catalogue", $"--gamedirectory={newAssemblyName}");

        await godot.Run("--editor");
    }

    private static async Task AddReference(ProgramDotnet dotnet, string pathToCsproj)
    {
        await dotnet.AddReferenceToCurrentProject(pathToCsproj);
        await dotnet.AddProjectToCurrentSolution(pathToCsproj);
    }

    private static async Task ApplyAssemblyToTemplateAndCopy(string fileName, string newAssemblyName)
    {
        var projectDirectory = new RealFileSystem(newAssemblyName);
        var templateFiles = ControlRoomConstants.WorkingDirectoryFiles.GetDirectory("Templates/Godot");
        var template = await templateFiles.ReadFileAsync($"{fileName}.template");
        await projectDirectory.WriteToFileAsync($"{fileName}", template.Replace("%ASSEMBLY_NAME%", newAssemblyName));
    }
}
