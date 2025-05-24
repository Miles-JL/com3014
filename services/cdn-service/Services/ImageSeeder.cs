using System.IO;
using Microsoft.Extensions.Logging;

namespace CdnService.Services
{
    public static class ImageSeeder
    {
        public static void SeedProfileImages(string contentRootPath, ILogger logger)
        {
            var storagePath = Path.Combine(contentRootPath, "storage");
            var seedDataPath = Path.Combine(contentRootPath, "seed-data");
            
            // Ensure storage directory exists
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
                logger.LogInformation("Created storage directory at: {StoragePath}", storagePath);
            }

            // Check if seed data directory exists
            if (!Directory.Exists(seedDataPath))
            {
                logger.LogWarning("Seed data directory not found at {SeedDataPath}", seedDataPath);
                return;
            }

            // Supported image extensions
            var imageExtensions = new[] { "*.jpg", "*.jpeg", "*.gif", "*.png" };
            
            // Get all image files with supported extensions
            var imageFiles = imageExtensions
                .SelectMany(ext => Directory.GetFiles(seedDataPath, ext))
                .ToArray();
            
            if (imageFiles.Length == 0)
            {
                logger.LogWarning("No image files found in {SeedDataPath}", seedDataPath);
                return;
            }
            
            foreach (var imagePath in imageFiles)
            {
                var fileName = Path.GetFileName(imagePath);
                var targetPath = Path.Combine(storagePath, fileName);
                
                try
                {
                    File.Copy(imagePath, targetPath, overwrite: true);
                    logger.LogInformation("Seeded profile image: {FileName}", fileName);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    logger.LogError(ex, "Error seeding profile image {FileName}", fileName);
                }
            }
        }
    }
}
