using System.ComponentModel.DataAnnotations;

namespace ImageUploadApp.Models.Api;

public sealed class AlbumCreateFolderRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = "";

    public Guid? ParentFolderId { get; set; }
}
