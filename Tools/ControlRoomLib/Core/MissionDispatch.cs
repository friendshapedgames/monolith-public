using SecretPlanCore.ArgumentParsing;

namespace ControlRoomLib.Core;

public static class MissionDispatch
{
    public static async Task Execute(Mission mission)
    {
        var missionName = mission.GetType().Name;
        await OutPipe.LogMessage($"-- MISSION START: {missionName} --");
        await OutPipe.LogMessage($"-- {mission.MissionVariables} --");

        try
        {
            await mission.Run();
            await OutPipe.LogMessage("-- MISSION COMPLETE --");
        }
        catch (Exception exception)
        {
            await OutPipe.LogError("!! MISSION FAILED !!");

            if (exception is ArgParseFailedException consoleArgParseFailedException)
            {
                await OutPipe.LogError($"Failed to parse argument {consoleArgParseFailedException.Argument}");
                await OutPipe.LogError(consoleArgParseFailedException.Message);
            }
            else
            {
                await OutPipe.LogError(exception.ToString());
                var innerException = exception.InnerException;
                if (innerException != null)
                {
                    await OutPipe.LogError(innerException.ToString());
                }
            }
        }
    }

    public static async Task<Mission?> CreateMission(Type type, List<string> argsAsString)
    {
        try
        {
            return (Mission?)Activator.CreateInstance(type, argsAsString, Platform.SerializedState.MissionVariables);
        }
        catch (Exception exception)
        {
            await OutPipe.LogError($"Failed to create instance of {type.Name}\n{exception}");
        }

        return null;
    }
    
    
}