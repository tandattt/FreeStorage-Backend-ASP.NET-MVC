using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ImageUploadApp.Models;

public sealed class ImageShareRecord
{
    public Guid Id { get; set; }

    public Guid ImageId { get; set; }

    [Required]
    [MaxLength(450)]
    public string OwnerUserId { get; set; } = null!;

    [Required]
    [MaxLength(450)]
    public string RecipientUserId { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    [ForeignKey(nameof(ImageId))]
    public ImageRecord? Image { get; set; }

    [ForeignKey(nameof(OwnerUserId))]
    public Microsoft.AspNetCore.Identity.IdentityUser? OwnerUser { get; set; }

    [ForeignKey(nameof(RecipientUserId))]
    public Microsoft.AspNetCore.Identity.IdentityUser? RecipientUser { get; set; }
}
