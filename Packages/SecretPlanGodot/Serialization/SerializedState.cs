using Newtonsoft.Json;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace SecretPlanGodot.Serialization;

public class SerializedState<TSaveFile, TSettings>
    where TSettings : SettingsBlob, new()
    where TSaveFile : BaseSaveFile, new()
{
    private readonly LazyLoaded<TSaveFile> _saveFile;
    private readonly LazyLoaded<TSettings> _settings;
    private long? _lastBackupTimestamp;

    public SerializedState(Func<TSaveFile> loadSave, Func<TSettings> loadSettings)
    {
        _saveFile = new LazyLoaded<TSaveFile>(loadSave);
        _settings = new LazyLoaded<TSettings>(loadSettings);
    }

    public TSettings Settings => _settings.Get();
    public TSaveFile SaveFile => _saveFile.Get();

    public bool SaveFileDirty { get; private set; }

    public void SaveSettings()
    {
        var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
        File.WriteAllText(CommonSerializationConstants.SettingsPath, json);
        LocalClient.Print("Settings Saved!");
        SettingsSaved?.Invoke();
    }

    public event Action? SettingsSaved;
    public event Action? SaveFileSaved;

    public void SaveSaveFile()
    {
        var json = JsonConvert.SerializeObject(SaveFile, Formatting.Indented);
        File.WriteAllText(CommonSerializationConstants.SavePath, json);

        var now = TimeUtilities.TimeNowMilliseconds();
        if (!_lastBackupTimestamp.HasValue || now - _lastBackupTimestamp.Value > 5000 * 60) // backup every 5 minutes
        {
            LocalClient.Print($"Writing to backup save: {SaveFile.BackupIndex}");
            _lastBackupTimestamp = TimeUtilities.TimeNowMilliseconds();
            File.WriteAllText(CommonSerializationConstants.SavePath + $"_backup{SaveFile.BackupIndex}", json);
            SaveFile.IncrementBackupIndex();
        }

        SaveFileDirty = false;
        SaveFileSaved?.Invoke();
        LocalClient.Print("Save file saved");
    }

    public void MarkSaveFileDirty()
    {
        SaveFileDirty = true;
    }
}