using MediaFlow.Application.Services;
using MediaFlow.Core.Domain;

namespace MediaFlow.Tests;

public sealed class AuditExportServiceTests
{
    [Fact]
    public void ToCsv_EscapesPathsAndErrors()
    {
        var operation = new MediaOperation
        {
            MediaFileId = Guid.NewGuid(),
            State = MediaOperationState.Quarantined,
            SourcePath = "/source/a,b.jpg",
            LastError = "Hash \"mismatch\"\nreview required",
            StartedAt = new DateTimeOffset(2026, 8, 21, 20, 0, 0, TimeSpan.Zero)
        };

        var csv = AuditExportService.ToCsv([operation]);

        Assert.Contains("\"/source/a,b.jpg\"", csv);
        Assert.Contains("\"Hash \"\"mismatch\"\"\nreview required\"", csv);
    }
}
