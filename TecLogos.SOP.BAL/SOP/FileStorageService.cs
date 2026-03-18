using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IFileStorageService
    {
        Task<string> SaveSopDocumentAsync(IFormFile file, Guid sopId, int version);

        string GetAbsolutePath(string relativePath);
    }

    public class FileStorageService : IFileStorageService
    {
        private readonly string _projectRoot;
        private readonly ILogger<FileStorageService> _logger;

        public FileStorageService(IWebHostEnvironment env, ILogger<FileStorageService> logger)
        {
            _projectRoot = env.ContentRootPath;
            _logger      = logger;
        }

        public async Task<string> SaveSopDocumentAsync(IFormFile file, Guid sopId, int version)
        {
            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                originalName = $"document_{Guid.NewGuid()}.pdf";

            var relativeDir = $"Uploads/Sop-Detail/{sopId}/SopDocument/V{version}";

            var absoluteDir = Path.Combine(_projectRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteDir);

            var absoluteFile = Path.Combine(absoluteDir, originalName);

            await using var stream = new FileStream(
                absoluteFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);

            _logger.LogInformation(
                "SOP document saved → {AbsPath} ({Bytes} bytes)", absoluteFile, file.Length);

            return $"{relativeDir}/{originalName}";
        }

        public string GetAbsolutePath(string relativePath)
            => Path.Combine(_projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
