using System.ComponentModel.DataAnnotations;

namespace ImageUploadApp.Models;

public sealed class LoginInputModel
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Display(Name = "Ghi nhớ")]
    public bool RememberMe { get; set; }
}

public sealed class RegisterInputModel
{
    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu {2} ký tự.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu xác nhận không khớp.")]
    public string? ConfirmPassword { get; set; }
}

public sealed class AuthLoginApiRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required]
    public string Password { get; set; } = "";
}
