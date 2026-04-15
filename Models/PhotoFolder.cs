using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace ImageUploadApp.Models;

public class PhotoFolder
{
    public Guid Id { get; set; }

    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = null!;

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? ParentFolderId { get; set; }

    [ForeignKey(nameof(ParentFolderId))]
    public virtual PhotoFolder? Parent { get; set; }

    public virtual ICollection<PhotoFolder>? Children { get; set; }

    [ForeignKey(nameof(UserId))]
    public virtual IdentityUser? User { get; set; }
}
