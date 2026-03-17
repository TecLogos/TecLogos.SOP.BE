using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TecLogos.SOP.BAL.SOP
{
    public interface IFileStorageBAL
    {
        /// <summary>
        /// Saves the uploaded PDF to:
        ///   {ProjectRoot}/Uploads/Sop-Detail/{sopId}/SopDocument/V{version}/{originalFilename}
        /// Returns the relative path stored in [SopDetails].[SopDocument].
        /// </summary>
        Task<string> SaveSopDocumentAsync(IFormFile file, Guid sopId, int version);

        /// <summary>
        /// Converts the stored relative path back to an absolute physical path.
        /// </summary>
        string GetAbsolutePath(string relativePath);
    }

    public class FileStorageBAL : IFileStorageBAL
    {
        // Project root = ContentRootPath (same folder as appsettings.json)
        // Uploads sit here: {ContentRootPath}/Uploads/...
        private readonly string _projectRoot;
        private readonly ILogger<FileStorageBAL> _logger;

        public FileStorageBAL(IWebHostEnvironment env, ILogger<FileStorageBAL> logger)
        {
            _projectRoot = env.ContentRootPath;
            _logger = logger;
        }

        public async Task<string> SaveSopDocumentAsync(IFormFile file, Guid sopId, int version)
        {
            // Sanitise — strip any directory traversal from the original name
            var originalName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(originalName))
                originalName = $"document_{Guid.NewGuid()}.pdf";

            // Relative path segments (forward-slash — stored in DB)
            // Uploads / Sop-Detail / {sopId} / SopDocument / V{version} / filename.pdf
            var relativeDir = $"Uploads/Sop-Detail/{sopId}/SopDocument/V{version}";

            // Absolute directory on disk
            var absoluteDir = Path.Combine(_projectRoot, relativeDir.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteDir);  // creates all parent dirs

            var absoluteFile = Path.Combine(absoluteDir, originalName);

            await using var stream = new FileStream(
                absoluteFile, FileMode.Create, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream);

            _logger.LogInformation(
                "SOP document saved → {AbsPath} ({Bytes} bytes)", absoluteFile, file.Length);

            // Return relative path with forward slashes for cross-platform DB storage
            return $"{relativeDir}/{originalName}";
        }

        public string GetAbsolutePath(string relativePath)
            => Path.Combine(_projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}