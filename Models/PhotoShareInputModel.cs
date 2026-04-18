using System.ComponentModel.DataAnnotations;

namespace ImageUploadApp.Models;

public sealed class PhotoShareInputModel
{
    [Required(ErrorMessage = "Nhập email người nhận.")]
    [EmailAddress(ErrorMessage = "Email người nhận không hợp lệ.")]
    public string RecipientEmail { get; set; } = "";

    public string ImageIds { get; set; } = "";

    public Guid? ReturnFolderId { get; set; }
}
