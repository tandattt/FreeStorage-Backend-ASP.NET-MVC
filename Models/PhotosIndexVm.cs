namespace ImageUploadApp.Models;

public sealed class PhotosIndexVm
{
    public IReadOnlyList<PhotoItemVm> Items { get; init; } = [];

    /// <summary>Fingerprint trang hiện tại — client so với API Sync để biết có cần cập nhật DOM không.</summary>
    public string Revision { get; init; } = "";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 12;

    public int TotalCount { get; init; }

    /// <summary>Số cột trên màn hình lớn (2, 3, 4, 6, 8, 10).</summary>
    public int Cols { get; init; } = 6;

    /// <summary>Tổng byte đã lưu (JPEG) của user — dùng cho hạn mức 5 GB.</summary>
    public long UsedBytes { get; init; }

    public long QuotaBytes { get; init; } = StorageLimits.PerUserQuotaBytes;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Phần trăm đã dùng (0–100) cho thanh tiến trình.</summary>
    public int QuotaPercent => QuotaBytes <= 0
        ? 0
        : (int)Math.Min(100, Math.Floor(UsedBytes * 100.0 / QuotaBytes));

    /// <summary>Hiển thị dung lượng đã dùng (KB/MB/GB).</summary>
    public string UsedSizeLabel
    {
        get
        {
            if (UsedBytes <= 0)
                return "0 MB";
            var mb = UsedBytes / (1024.0 * 1024.0);
            if (mb >= 1024)
                return $"{mb / 1024:0.##} GB";
            if (mb >= 1)
                return $"{mb:0.#} MB";
            var kb = UsedBytes / 1024.0;
            return $"{kb:0.#} KB";
        }
    }
}
