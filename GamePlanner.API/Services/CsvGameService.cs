using GamePlanner.API.Domain;

namespace GamePlanner.API.Services;

public class CsvGameService(IConfiguration config, ILogger<CsvGameService> logger)
{
    private const double SecondsToHours = 3600.0;

    public (List<HLTBGameDto> Records, int Skipped, string? FailureDetail) ReadGames()
    {
        var path = config["HLTB:CsvPath"];

        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogWarning("HLTB:CsvPath is not configured.");
            return ([], 0, "HLTB:CsvPath is not configured");
        }

        if (!File.Exists(path))
        {
            logger.LogWarning("HLTB CSV file not found at: {Path}", path);
            return ([], 0, "HLTB CSV file not found");
        }

        var results = new List<HLTBGameDto>();
        int skipped = 0;

        try
        {
            using var reader = new StreamReader(path);

            // Read and validate header
            var header = reader.ReadLine();
            if (header is null)
            {
                logger.LogWarning("HLTB CSV is empty.");
                return ([], 0, "HLTB CSV is empty");
            }

            var columns = ParseRow(header);
            int idxSteamId   = IndexOf(columns, "steam_id");
            int idxName      = IndexOf(columns, "game_name");
            int idxMain      = IndexOf(columns, "comp_main");
            int idxPlus      = IndexOf(columns, "comp_plus");
            int idx100       = IndexOf(columns, "comp_100");

            if (idxName < 0)
            {
                logger.LogError("HLTB CSV missing required column 'game_name'.");
                return ([], 0, "HLTB CSV missing required column 'game_name'");
            }

            string? line;
            int lineNumber = 1;

            while ((line = reader.ReadLine()) is not null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var cols = ParseRow(line);

                    var name = GetValue(cols, idxName);
                    if (string.IsNullOrWhiteSpace(name)) { skipped++; continue; }

                    results.Add(new HLTBGameDto
                    {
                        Name               = name,
                        SteamAppId         = ParseNullableInt(GetValue(cols, idxSteamId)),
                        MainStoryHours     = SecondsToHoursNullable(GetValue(cols, idxMain)),
                        MainPlusHours      = SecondsToHoursNullable(GetValue(cols, idxPlus)),
                        CompletionistHours = SecondsToHoursNullable(GetValue(cols, idx100)),
                    });
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Skipping invalid row at line {Line}.", lineNumber);
                    skipped++;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to read HLTB CSV file.");
            return ([], 0, "HLTB CSV read failed");
        }

        if (skipped > 0)
            logger.LogWarning("HLTB CSV read: {Count} records, {Skipped} skipped.", results.Count, skipped);
        else
            logger.LogInformation("HLTB CSV read: {Count} records, {Skipped} skipped.", results.Count, skipped);

        return (results, skipped, null);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    // Simple CSV row parser — handles quoted fields (including commas inside quotes)
    private static string[] ParseRow(string line)
    {
        var fields = new List<string>();
        bool inQuotes = false;
        var current = new System.Text.StringBuilder();

        foreach (var ch in line)
        {
            if (ch == '"')      { inQuotes = !inQuotes; }
            else if (ch == ',' && !inQuotes) { fields.Add(current.ToString()); current.Clear(); }
            else                { current.Append(ch); }
        }
        fields.Add(current.ToString());
        return [.. fields];
    }

    private static int IndexOf(string[] headers, string name) =>
        Array.FindIndex(headers, h => h.Trim().Equals(name, StringComparison.OrdinalIgnoreCase));

    private static string GetValue(string[] cols, int idx) =>
        idx >= 0 && idx < cols.Length ? cols[idx].Trim() : string.Empty;

    private static int? ParseNullableInt(string value) =>
        int.TryParse(value, out var n) && n > 0 ? n : null;

    private static double? SecondsToHoursNullable(string value) =>
        double.TryParse(value, out var d) && d > 0
            ? Math.Round(d / SecondsToHours, 1)
            : null;
}
