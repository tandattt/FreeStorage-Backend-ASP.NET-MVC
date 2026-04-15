namespace ImageUploadApp.Models;

public sealed class PhotoFolderListItem
{
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    public int PhotoCount { get; init; }
}
