using System.Reflection;
using ControlRoomLib.Core;
using SecretPlanCore.ArgumentParsing;
using SecretPlanCore.Core;

namespace ControlRoom.Missions;

public class MissionVariablesEdit : Mission
{
    public MissionVariablesEdit(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs, missionVariables)
    {
    }

    public override async Task Run()
    {
        var command = PositionalArgs.Get(0, "Command").ParseAsSpecificString("list", "set", "unset");

        var nameToProperty = new Dictionary<string, PropertyInfo>();
        foreach (var propertyInfo in Reflection
                     .GetAllMembersInTypeWithAttribute<ArgumentAttribute>(typeof(MissionVariables))
                     .Select(a => a as PropertyInfo))
        {
            var key = propertyInfo?.GetCustomAttribute<ArgumentAttribute>()?.Key.ToLower();

            if (key == null)
            {
                continue;
            }

            if (propertyInfo == null)
            {
                continue;
            }

            nameToProperty[key] = propertyInfo;
        }

        switch (command)
        {
            case "list":
                foreach (var (key, propertyInfo) in nameToProperty)
                {
                    await OutPipe.LogMessage(
                        $"{key} ({propertyInfo.PropertyType.Name}) = {ControlRoomConstants.HumanReadableStringify(propertyInfo.GetValue(MissionVariables))}");
                }

                break;
            case "set":
                var keyToAdd = PositionalArgs.Get(1, "Variable Name").ParseAsString().ToLower();
                var valueToAddAsString = PositionalArgs.Get(2, "Value").ParseAsString();
                if (Reflection.TryParseTo(nameToProperty[keyToAdd].PropertyType, valueToAddAsString, out var value))
                {
                    nameToProperty[keyToAdd].SetValue(Platform.SerializedState.MissionVariables, value);
                    await OutPipe.LogMessage($"Set default: --{keyToAdd}={value}");
                }
                else
                {
                    throw new MissionFailedException($"Could not parse `{valueToAddAsString}` as {nameToProperty[keyToAdd].PropertyType.Name}");
                }


                break;
            case "unset":
                var keyToRemove = PositionalArgs.Get(1, "Variable Name").ParseAsString().ToLower();
                var valueToRemove = nameToProperty[keyToRemove].GetValue(new MissionVariables());
                await OutPipe.LogMessage($"Set default: --{keyToRemove}={valueToRemove}");


                break;
        }
    }
}