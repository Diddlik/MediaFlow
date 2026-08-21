using System.Globalization;
using System.Text;
using MediaFlow.Core.Domain;

namespace MediaFlow.Application.Services;

public static class AuditExportService
{
    public static string ToCsv(IEnumerable<MediaOperation> operations)
    {
        var csv = new StringBuilder("id,media_file_id,event_id,state,source_path,destination_path,retry_count,error,started_at_utc,completed_at_utc\n");
        foreach (var operation in operations)
        {
            AppendRow(csv,
                operation.Id.ToString("D"),
                operation.MediaFileId.ToString("D"),
                operation.EventId?.ToString("D") ?? string.Empty,
                operation.State.ToString(),
                operation.SourcePath,
                operation.DestinationPath ?? string.Empty,
                operation.RetryCount.ToString(CultureInfo.InvariantCulture),
                operation.LastError ?? string.Empty,
                operation.StartedAt.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                operation.CompletedAt?.UtcDateTime.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty);
        }
        return csv.ToString();
    }

    private static void AppendRow(StringBuilder csv, params string[] values) =>
        csv.AppendJoin(',', values.Select(Escape)).Append('\n');

    private static string Escape(string value) =>
        value.IndexOfAny([',', '"', '\r', '\n']) < 0
            ? value
            : $"\"{value.Replace("\"", "\"\"")}\"";
}
