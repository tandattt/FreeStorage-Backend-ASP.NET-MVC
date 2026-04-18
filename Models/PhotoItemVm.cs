namespace ImageUploadApp.Models;

public class PhotoItemVm
{
    public Guid Id { get; set; }

    public Guid? FolderId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ProxyUrl { get; set; }

    public string? DownloadUrl { get; set; }

    public bool IsPending { get; set; }

    public bool IsFailed { get; set; }

    public bool IsSharedWithViewer { get; set; }

    public string? SharedByDisplayName { get; set; }

    public DateTimeOffset? SharedAt { get; set; }
}
