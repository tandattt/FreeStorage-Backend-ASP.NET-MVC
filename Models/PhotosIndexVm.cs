namespace ImageUploadApp.Models;

public sealed class PhotosIndexVm
{
    public Guid? FolderId { get; init; }

    public string? FolderName { get; init; }

    public IReadOnlyList<PhotoFolderListItem> ChildFolders { get; init; } = [];

    public IReadOnlyList<PhotoFolderBreadcrumbItem> Breadcrumb { get; init; } = [];

    public Guid? ParentFolderId { get; init; }

    public int TotalLibraryCount { get; init; }

    public IReadOnlyList<PhotoItemVm> Items { get; init; } = [];

    public IReadOnlyList<PhotoItemVm> SharedItems { get; init; } = [];

    public PhotoShareInputModel ShareInput { get; init; } = new();

    public string Revision { get; init; } = "";

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 12;

    public int TotalCount { get; init; }

    public int Cols { get; init; } = 6;

    public long UsedBytes { get; init; }

    public long QuotaBytes { get; init; } = StorageLimits.PerUserQuotaBytes;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public int QuotaPercent => QuotaBytes <= 0
        ? 0
        : (int)Math.Min(100, Math.Floor(UsedBytes * 100.0 / QuotaBytes));

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
