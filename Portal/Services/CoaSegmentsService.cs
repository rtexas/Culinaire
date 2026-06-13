using Microsoft.Data.SqlClient;
using Portal.Models;

namespace Portal.Services;

public sealed class CoaSegmentsService
{
    private readonly string _cs;
    public CoaSegmentsService(string connectionString) => _cs = connectionString;

    public async Task<List<CoaSegment>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT [SegmentNumber],[Description] FROM [dbo].[CoaSegments] ORDER BY [SegmentNumber];";
        var list = new List<CoaSegment>();
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd    = new SqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(new CoaSegment { SegmentNumber = reader.GetInt32(0), Description = reader.GetString(1) });
        return list;
    }

    public async Task<int> GetMaxSegmentAsync(CancellationToken ct = default)
    {
        const string sql = "SELECT ISNULL(MAX([SegmentNumber]),0) FROM [dbo].[CoaSegments];";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpsertAsync(int segmentNumber, string description, CancellationToken ct = default)
    {
        const string sql = """
            IF EXISTS (SELECT 1 FROM [dbo].[CoaSegments] WHERE [SegmentNumber]=@Num)
                UPDATE [dbo].[CoaSegments] SET [Description]=@Desc WHERE [SegmentNumber]=@Num;
            ELSE
                INSERT INTO [dbo].[CoaSegments]([SegmentNumber],[Description]) VALUES(@Num,@Desc);
            """;
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Num",  segmentNumber);
        cmd.Parameters.AddWithValue("@Desc", description.Trim());
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(int segmentNumber, CancellationToken ct = default)
    {
        const string sql = "DELETE FROM [dbo].[CoaSegments] WHERE [SegmentNumber]=@Num;";
        await using var conn = new SqlConnection(_cs);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@Num", segmentNumber);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
