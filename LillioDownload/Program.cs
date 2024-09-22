using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace ImageDownloader
{
    class Program
    {
        // Define the target directory
        private const string TargetDirectory = @"C:\temp\rory\";

        static async Task Main(string[] args)
        {
            try
            {
                // Ensure the target directory exists
                Directory.CreateDirectory(TargetDirectory);

                // Read and parse the JSON file
                var images = await ReadJsonAsync("images.json");

                // Dictionary to keep track of filenames to handle duplicates
                var filenameCounts = new Dictionary<string, int>();

                using (HttpClient client = new HttpClient())
                {
                    foreach (var image in images)
                    {
                        // Parse the date
                        if (!TryParseDate(image.Date, out DateTime imageDate))
                        {
                            Console.WriteLine($"Invalid date format: {image.Date}. Skipping this entry.");
                            continue;
                        }

                        // Format the filename
                        string baseFilename = imageDate.ToString("yyyy-MM-dd");
                        string filename = $"{baseFilename}";
                        
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
                        string extension = Path.GetExtension(new Uri(image.Link).AbsolutePath);
                        if (string.IsNullOrEmpty(extension))
                        {
                            extension = ".jpg"; // Default to .jpg if extension is missing
                        }

                        string filePath = Path.Combine(TargetDirectory, $"{filename}{extension}");

                        // Download the image
                        Console.WriteLine($"Downloading {image.Link} to {filePath}");
                        byte[] imageBytes = await client.GetByteArrayAsync(image.Link);
                        await File.WriteAllBytesAsync(filePath, imageBytes);

                        // Set the file creation and modification dates
                        File.SetCreationTime(filePath, imageDate);
                        File.SetLastWriteTime(filePath, imageDate);

                        // Set EXIF date
                        SetExifDate(filePath, imageDate);
                    }
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

            using (FileStream fs = File.OpenRead(filePath))
            {
                return await JsonSerializer.DeserializeAsync<List<ImageInfo>>(fs, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
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
                using (Image image = Image.Load(filePath))
                {
                    var exifProfile = image.Metadata.ExifProfile ?? new ExifProfile();

                    // Format the date as "yyyy:MM:dd HH:mm:ss"
                    string exifDate = date.ToString("yyyy:MM:dd HH:mm:ss");

                    // Set DateTimeOriginal (Tag 0x9003)
                    exifProfile.SetValue(ExifTag.DateTimeOriginal, exifDate);

                    // Optionally, set other date tags
                    exifProfile.SetValue(ExifTag.DateTimeDigitized, exifDate);
                    exifProfile.SetValue(ExifTag.DateTime, exifDate);

                    image.Metadata.ExifProfile = exifProfile;

                    // Save the image with updated EXIF data
                    image.Save(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to set EXIF data for {filePath}: {ex.Message}");
            }
        }
    }

    // Define a class to represent each image entry
    public class ImageInfo
    {
        public string Date { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
    }
}
