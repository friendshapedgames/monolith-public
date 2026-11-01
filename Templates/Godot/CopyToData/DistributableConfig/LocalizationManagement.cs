using System.Text;
using ControlRoomLib.Core;
using ControlRoomLib.Missions;
using CsvHelper;
using JetBrains.Annotations;
using SecretPlan.Generated;
using SecretPlanCore.Configuration;
using SecretPlanCore.Core;
using SecretPlanGodot.Core;

namespace DATA_ASSEMBLY.DistributableConfig;

[UsedImplicitly]
public class LocalizationManagement : Mission
{
    public enum CsvFormat
    {
        CommaCsv,
        TabTsv
    }

    public LocalizationManagement(List<string> rawArgs, MissionVariables missionVariables) : base(rawArgs,
        missionVariables)
    {
    }

    public override async Task Run()
    {
        await ConfigManagement.SetupConfigServer(MissionVariables.GameDirectoryFiles());

        var command = PositionalArgs.Get(0, "Command")
            .ParseAsSpecificString(
                "add",
                "export-tsv",
                "export-tsv-unsorted",
                "import-tsv",
                "export-csv",
                "export-csv-unsorted",
                "import-csv",
                "fill-test",
                "cleanup",
                "clear"
            );

        switch (command)
        {
            case "add":
                await AddEntry();
                break;
            case "export-csv":
                await ExportCsv(true, CsvFormat.CommaCsv);
                break;
            case "export-tsv":
                await ExportCsv(true, CsvFormat.TabTsv);
                break;
            case "export-tsv-unsorted":
                await ExportCsv(false, CsvFormat.TabTsv);
                break;
            case "export-csv-unsorted":
                await ExportCsv(false, CsvFormat.CommaCsv);
                break;
            case "import-csv":
                await ImportCsv(CsvFormat.CommaCsv);
                break;
            case "import-tsv":
                await ImportCsv(CsvFormat.TabTsv);
                break;
            case "fill-test":
                await FillTestLocale();
                break;
            case "cleanup":
                await Cleanup();
                break;
            case "clear":
                await Clear();
                break;
        }
    }

    /// <summary>
    ///     Remove ALL loc strings across all locales, fully emptying the loc table
    /// </summary>
    private async Task Clear()
    {
        foreach (var idTable in ConfigServer.Instance.GetAllInstancesOfType<LocalizationIdTable>())
        {
            foreach (var idToRemove in idTable.AllIds().ToList())
            {
                idTable.Remove(idToRemove);
            }
        }

        foreach (var locale in ConfigServer.Instance.GetAllInstancesOfType<Locale>())
        {
            foreach (var idToRemove in locale.DebugGetAllIds().ToList())
            {
                locale.Remove(idToRemove);
            }
        }

        ConfigServer.Instance.WriteAllConfigs(MissionVariables.GameDirectoryFiles());
    }

    private async Task Cleanup()
    {
        var idsToRemove = new HashSet<uint>();
        foreach (var rootTable in ConfigServer.Instance.GetAllInstancesOfType<LocalizationRootTable>())
        {
            foreach (var id in rootTable.IdTable.ReadOrDefault().AllIds())
            {
                var englishString = LocalizationServer.Instance.GetTranslatedStringFromId(id, rootTable.FallbackLocale);
                if (englishString.Result == TranslationResult.NoTranslations)
                {
                    idsToRemove.Add(id);
                }
            }

            foreach (var id in rootTable.FallbackLocale.ReadOrDefault().DebugGetAllIds())
            {
                if (!rootTable.IdTable.ReadOrDefault().HasKey(id))
                {
                    idsToRemove.Add(id);
                }
            }
        }

        foreach (var idTable in ConfigServer.Instance.GetAllInstancesOfType<LocalizationIdTable>())
        {
            foreach (var idToRemove in idsToRemove)
            {
                idTable.Remove(idToRemove);
            }
        }

        foreach (var idTable in ConfigServer.Instance.GetAllInstancesOfType<Locale>())
        {
            foreach (var idToRemove in idsToRemove)
            {
                idTable.Remove(idToRemove);
            }
        }

        ConfigServer.Instance.WriteAllConfigs(MissionVariables.GameDirectoryFiles());
    }

    private async Task FillTestLocale()
    {
        var rootTable = LocalizationRootTableEnum.LocalizationTable.ReadOrDefault();

        foreach (var id in rootTable.IdTable.ReadOrDefault().AllIds())
        {
            var englishString = LocalizationServer.Instance.GetTranslatedStringFromId(id, rootTable.FallbackLocale);
            var stringBuilder = new StringBuilder();

            stringBuilder.Append('%');
            var normalFlag = 0;

            foreach (var character in englishString.Content)
            {
                if (character == '[')
                {
                    normalFlag++;
                }

                if (character == ']')
                {
                    normalFlag--;
                }

                if (normalFlag == 0)
                {
                    if (character == 'o')
                    {
                        stringBuilder.Append('0');
                    }
                    else if (character == 'e')
                    {
                        stringBuilder.Append('3');
                    }
                    else if (char.IsUpper(character))
                    {
                        stringBuilder.Append(char.ToLower(character));
                    }
                    else if (char.IsLower(character))
                    {
                        stringBuilder.Append(char.ToUpper(character));
                    }
                    else
                    {
                        stringBuilder.Append(character);
                    }
                }
                else
                {
                    stringBuilder.Append(character);
                }
            }

            stringBuilder.Append('%');

            await OutPipe.LogMessage($"{englishString} -> {stringBuilder}");
            LocaleEnum.RandomLocale.ReadOrDefault().SetTranslation(id, stringBuilder.ToString());
        }

        ConfigServer.Instance.WriteAllConfigs(MissionVariables.GameDirectoryFiles());
    }

    private async Task AddEntry()
    {
        var slug = PositionalArgs.Get(1, "Slug").ParseAsString();
        var rootTable = ConfigServer.Instance.GetAllInstancesOfType<LocalizationRootTable>().FirstOrDefault();

        if (rootTable == null)
        {
            throw new MissionFailedException($"No {nameof(LocalizationRootTable)} found");
        }

        var reference = rootTable.GetMutableReference(slug);

        var translation = PositionalArgs.Get(2, "Translation").ParseAsString();
        if (translation.StartsWith("\"") && translation.EndsWith("\""))
        {
            translation = translation.Substring(1, translation.Length - 2);
        }

        if (rootTable.FallbackLocale == LocaleEnum.None)
        {
            throw new MissionFailedException("No fallback locale set!");
        }

        if (rootTable.IdTable == LocalizationIdTableEnum.None)
        {
            throw new MissionFailedException("No id table set!");
        }

        reference.SetTranslation(rootTable.FallbackLocale, translation);

        ConfigServer.Instance.WriteAllConfigs(MissionVariables.GameDirectoryFiles());

        await OutPipe.LogMessage($"Adding {reference.Id} {slug}: {rootTable.FallbackLocale}");
    }

    private async Task ExportCsv(bool sortByKey, CsvFormat format)
    {
        var entries = new List<Dictionary<string, string>>();
        var rootTable = LocalizationRootTableEnum.LocalizationTable.ReadOrDefault();
        var idTable = rootTable.IdTable.ReadOrDefault();

        foreach (var id in idTable.AllIds())
        {
            var entry = new Dictionary<string, string>
            {
                ["id"] = id.ToString(),
                ["slug"] = idTable.GetSlug(id)
            };

            foreach (var locale in LocalizationServer.Instance.AvailableLocales(false))
            {
                // intentionally put empty string if we don't have a translation
                entry[locale.ReadOrDefault().LocaleCode] =
                    LocalizationServer.Instance.GetTranslationStatus(id, locale) == TranslationResult.Success
                        ? LocalizationServer.Instance.GetTranslatedStringFromId(id, locale)
                        : "";
            }

            entries.Add(entry);
        }

        if (sortByKey)
        {
            entries.Sort((a, b) => string.Compare(a["slug"], b["slug"], StringComparison.Ordinal));
        }

        var allColumns = new List<string> { "id", "slug" };
        allColumns.AddRange(
            LocalizationServer.Instance.AvailableLocales(false)
                .Select(a => a.ReadOrDefault().LocaleCode)
                .Distinct());


        var filePath = "loc_table.csv";

        new RealFileSystem(".").DeleteFile(filePath);

        await using var writer = new StreamWriter(filePath, false, new UTF8Encoding());

        var configuration = ControlRoomConstants.DefaultCsvConfiguration();

        if (format == CsvFormat.TabTsv)
        {
            configuration = ControlRoomConstants.GetDefaultTsvConfiguration();
        }

        await using var csv = new CsvWriter(writer, configuration);

        // Header
        foreach (var column in allColumns)
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();

        // Rows
        foreach (var entry in entries)
        {
            foreach (var column in allColumns)
            {
                entry.TryGetValue(column, out var value);
                csv.WriteField(value);
            }

            await csv.NextRecordAsync();
        }
    }

    private async Task ImportCsv(CsvFormat format)
    {
        var path = PositionalArgs.Get(1, "File Path").ParseAsString();

        var results = new List<Dictionary<string, string>>();

        using var reader = new StreamReader(path);
        var config = format == CsvFormat.TabTsv
            ? ControlRoomConstants.GetDefaultTsvConfiguration()
            : ControlRoomConstants.DefaultCsvConfiguration();
        using var csv = new CsvReader(reader, config with { NewLine = "\n" });

        await OutPipe.LogMessage($"Reading {path}");
        await csv.ReadAsync();
        csv.ReadHeader();

        var headers = csv.HeaderRecord;

        if (headers == null)
        {
            throw new MissionFailedException($"Could not read headers of csv file: {path}");
        }

        while (await csv.ReadAsync())
        {
            var row = new Dictionary<string, string>();

            foreach (var header in headers)
            {
                row[header.Trim()] = csv.GetField(header) ?? string.Empty;
            }

            results.Add(row);
        }

        var rootTable = LocalizationRootTableEnum.LocalizationTable.ReadOrDefault();
        var allLocales = LocalizationServer.Instance.AvailableLocales(true).ToList();

        foreach (var csvEntry in results)
        {
            var slug = csvEntry["slug"];
            uint id;
            if (ulong.TryParse(csvEntry["id"], out var longId))
            {
                id = (uint)longId;

                if (!rootTable.IdTable.ReadOrDefault().HasKey(id))
                {
                    rootTable.IdTable.ReadOrDefault().AddSlugAndId(id, slug);
                }

                await OutPipe.LogMessage($"Reading slug {slug}");
            }
            else
            {
                var newId = rootTable.IdTable.ReadOrDefault().AddNewSlug(slug);

                id = newId;
            }

            var mutable = rootTable.GetMutableReference(id);

            foreach (var locale in allLocales)
            {
                var translation = csvEntry.GetValueOrDefault(locale.ReadOrDefault().LocaleCode);

                if (translation != null)
                {
                    var trimmedTranslation = string.Join('\n', translation.Trim().SplitLines());
                    await OutPipe.LogMessage($"\tSetting {locale} {slug} to {trimmedTranslation}");
                    mutable.SetTranslation(locale, trimmedTranslation);
                }
                else
                {
                    await OutPipe.LogMessage($"\tSkipping for {locale} because there is no entry");
                }
            }

            mutable.SetSlug(slug);
        }

        ConfigServer.Instance.WriteAllConfigs(MissionVariables.GameDirectoryFiles());
    }
}