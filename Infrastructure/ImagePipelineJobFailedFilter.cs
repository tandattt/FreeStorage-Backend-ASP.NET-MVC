using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using ImageUploadApp.Data;
using ImageUploadApp.Models;
using ImageUploadApp.Services.Jobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImageUploadApp.Infrastructure;

/// <summary>
/// Khi job <see cref="ImagePipelineJob.ProcessAsync"/> chuyển sang trạng thái Failed (hết retry),
/// đánh dấu <see cref="ImageRecord.PipelineFailed"/> để FE không poll vĩnh viễn.
/// </summary>
public sealed class ImagePipelineJobFailedFilter : IApplyStateFilter
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ImagePipelineJobFailedFilter(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState)
            return;

        var job = context.BackgroundJob?.Job;
        if (job == null || job.Type != typeof(ImagePipelineJob))
            return;
        if (job.Method.Name != nameof(ImagePipelineJob.ProcessAsync))
            return;
        if (!TryGetGuidArg(job, out var imageId))
            return;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var env = scope.ServiceProvider.GetRequiredService<IWebHostEnvironment>();
            var log = scope.ServiceProvider.GetRequiredService<ILogger<ImagePipelineJobFailedFilter>>();

            var image = db.Images.FirstOrDefault(x => x.Id == imageId);
            if (image is null)
            {
                log.LogWarning("ImagePipelineJobFailedFilter: image {Id} not found", imageId);
                return;
            }

            if (!string.IsNullOrEmpty(image.SourceUrl))
                return;

            image.PipelineFailed = true;

            var pendingFull = ResolvePendingFullPath(image, env);
            if (!string.IsNullOrEmpty(pendingFull) && File.Exists(pendingFull))
            {
                try
                {
                    File.Delete(pendingFull);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Could not delete pending file after failure {Path}", pendingFull);
                }
            }

            image.PendingFilePath = null;
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Không ném lại — tránh lỗi khi áp dụng state
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var log = scope.ServiceProvider.GetRequiredService<ILogger<ImagePipelineJobFailedFilter>>();
                log.LogError(ex, "ImagePipelineJobFailedFilter: failed to mark {ImageId}", imageId);
            }
            catch
            {
                /* ignore */
            }
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }

    private static bool TryGetGuidArg(Job job, out Guid imageId)
    {
        imageId = default;
        if (job.Args is null || job.Args.Count == 0)
            return false;
        var a0 = job.Args[0];
        switch (a0)
        {
            case Guid g:
                imageId = g;
                return true;
            case string s when Guid.TryParse(s, out var g2):
                imageId = g2;
                return true;
            default:
                return false;
        }
    }

    private static string? ResolvePendingFullPath(ImageRecord image, IWebHostEnvironment env)
    {
        if (string.IsNullOrEmpty(image.PendingFilePath))
            return null;
        var name = Path.GetFileName(image.PendingFilePath);
        if (name.Length != 36 || !name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
            return null;
        var dir = Path.Combine(env.ContentRootPath, "App_Data", "pending");
        return Path.Combine(dir, name);
    }
}
