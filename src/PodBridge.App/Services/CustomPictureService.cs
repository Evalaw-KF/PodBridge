using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PodBridge.Core.Display;
using PodBridge.Core.Settings;
using Windows.Storage.Pickers;

namespace PodBridge.App.Services;

/// <summary>The optional custom headphone picture, copied into the PodBridge data folder.</summary>
internal static class CustomPictureService
{
    private static string Folder => SettingsStore.DefaultDirectory();

    /// <summary>Lets the user choose a picture and copies it in. Returns the stored file name, or an error message.</summary>
    public static async Task<(string? FileName, string? Error)> PickAndImportAsync(IntPtr windowHandle)
    {
        try
        {
            var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            foreach (var ext in CustomPicture.Extensions)
            {
                picker.FileTypeFilter.Add(ext);
            }
            WinRT.Interop.InitializeWithWindow.Initialize(picker, windowHandle);

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return (null, null); // cancelled
            }

            string extension = Path.GetExtension(file.Path);
            long length = new FileInfo(file.Path).Length;
            if (CustomPicture.Validate(extension, length) is { } problem)
            {
                return (null, problem);
            }

            Directory.CreateDirectory(Folder);
            string name = CustomPicture.StoredFileName(extension, DateTime.UtcNow.Ticks);
            File.Copy(file.Path, Path.Combine(Folder, name), overwrite: true);
            return (name, null);
        }
        catch (Exception ex)
        {
            Log.Error("Could not import the picture", ex);
            return (null, "Could not use that picture. Please try another file.");
        }
    }

    /// <summary>Deletes the stored copy (and any leftovers from earlier picks), keeping <paramref name="keep"/>.</summary>
    public static void Cleanup(string? keep)
    {
        try
        {
            if (!Directory.Exists(Folder))
            {
                return;
            }
            foreach (var path in Directory.EnumerateFiles(Folder, "custom-headphones-*"))
            {
                string name = Path.GetFileName(path);
                if (CustomPicture.IsStoredName(name) && !string.Equals(name, keep, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Could not clean up old pictures", ex);
        }
    }

    /// <summary>Shows the picture in the image, or hides the image when there is none. Never throws.</summary>
    public static void ApplyTo(Image image, string? fileName)
    {
        try
        {
            string? path = CustomPicture.IsStoredName(fileName) ? Path.Combine(Folder, fileName!) : null;
            if (path is null || !File.Exists(path))
            {
                image.Source = null;
                image.Visibility = Visibility.Collapsed;
                return;
            }
            image.Source = new BitmapImage(new Uri(path)) { DecodePixelWidth = 160 };
            image.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            Log.Error("Could not show the custom picture", ex);
            image.Visibility = Visibility.Collapsed;
        }
    }
}
