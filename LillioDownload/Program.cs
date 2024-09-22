using System.Text.Json;
using JetBrains.Annotations;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using Directory = System.IO.Directory;

namespace ImageDownloader;

public class Program
{
    // Define the target directory
    private const string TargetDirectory = @"C:\temp\rory\";

    public static async Task Main()
    {
        try
        {
            // Ensure the target directory exists
            Directory.CreateDirectory(TargetDirectory);

            // Read and parse the JSON file
            var images = await ReadJsonAsync("images.json");

            // Dictionary to keep track of filenames to handle duplicates
            var filenameCounts = new Dictionary<string, int>();

            using var client = new HttpClient();

            foreach (var image in images)
            {
                // Parse the date
                if (!TryParseDate(image.Date, out var imageDate))
                {
                    Console.WriteLine($"Invalid date format: {image.Date}. Skipping this entry.");
                    continue;
                }

                // Format the filename
                var baseFilename = imageDate.ToString("yyyy-MM-dd");
                var filename = $"{baseFilename}";

                if (filenameCounts.ContainsKey(baseFilename))
                {
                    filenameCounts[baseFilename]++;
                    filename = $"{baseFilename} ({filenameCounts[baseFilename]})";
                }
                else
                {
                    filenameCounts[baseFilename] = 1;
                }

                // Determine file extension from URL
                var extension = Path.GetExtension(new Uri(image.Link).AbsolutePath);
                if (string.IsNullOrEmpty(extension))
                {
                    extension = ".jpg"; // Default to .jpg if extension is missing
                }

                var filePath = Path.Combine(TargetDirectory, $"{filename}{extension}");

                // Download the image
                Console.WriteLine($"Downloading {image.Link} to {filePath}");
                var imageBytes = await client.GetByteArrayAsync(image.Link);
                await File.WriteAllBytesAsync(filePath, imageBytes);

                // Set the file creation and modification dates
                File.SetCreationTime(filePath, imageDate);
                File.SetLastWriteTime(filePath, imageDate);

                // Set EXIF date
                SetExifDate(filePath, imageDate);
            }

            Console.WriteLine("All images have been processed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    // Method to read and parse the JSON file
    private static async Task<List<ImageInfo>> ReadJsonAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"JSON file not found at path: {filePath}");

        await using var fs = File.OpenRead(filePath);
        var data = await JsonSerializer.DeserializeAsync<List<ImageInfo>>(fs, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (data is null)
            throw new NullReferenceException($"JSON data is null");

        return data;
    }

    // Method to parse date strings like "9/6/24" to DateTime
    private static bool TryParseDate(string dateStr, out DateTime date)
    {
        // Define possible date formats
        string[] formats = { "M/d/yy", "M/d/yyyy", "MM/dd/yy", "MM/dd/yyyy" };
        return DateTime.TryParseExact(dateStr, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    }

    // Method to set EXIF DateTimeOriginal
    private static void SetExifDate(string filePath, DateTime date)
    {
        try
        {
            using var image = Image.Load(filePath);
            var exifProfile = image.Metadata.ExifProfile ?? new ExifProfile();

            // Format the date as "yyyy:MM:dd HH:mm:ss"
            var exifDate = date.ToString("yyyy:MM:dd HH:mm:ss");

            // Set DateTimeOriginal (Tag 0x9003)
            exifProfile.SetValue(ExifTag.DateTimeOriginal, exifDate);

            // Optionally, set other date tags
            exifProfile.SetValue(ExifTag.DateTimeDigitized, exifDate);
            exifProfile.SetValue(ExifTag.DateTime, exifDate);

            image.Metadata.ExifProfile = exifProfile;

            // Save the image with updated EXIF data
            image.Save(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set EXIF data for {filePath}: {ex.Message}");
        }
    }
}

// Define a class to represent each image entry
[UsedImplicitly]
public class ImageInfo
{
    public string Date { get; set; }
    public string Title { get; set; }
    public string Link { get; set; }
}