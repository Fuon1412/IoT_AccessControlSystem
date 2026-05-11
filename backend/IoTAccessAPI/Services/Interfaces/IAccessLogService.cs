using IoTAccessAPI.DTOs.AccessLogs;

namespace IoTAccessAPI.Services.Interfaces;

public interface IAccessLogService
{
    Task<IEnumerable<AccessLogDto>> GetAllAsync();
    Task<IEnumerable<AccessLogDto>> GetByUserIdAsync(int userId);
    /// <summary>Returns (log, isDuplicate). isDuplicate=true means RequestId already existed.</summary>
    Task<(AccessLogDto log, bool isDuplicate)> CreateAsync(CreateAccessLogRequest request);
}
