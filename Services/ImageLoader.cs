using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TestingSystem.Services;

public static class ImageLoader
{
    /// <summary>Безпечно завантажує картинку (файл не блокується). null — файлу немає або він пошкоджений.</summary>
    public static ImageSource? TryLoad(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(path, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch { return null; }
    }
}
