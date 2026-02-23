using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace Focus_Bear.Utils
{
    // Method for URL to Icon using Google's favicon service
    internal class UrlToIcon : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string url = value as string;
            if (string.IsNullOrEmpty(url)) return null;

            try
            {
                // Clean the URL to get just the domain if possible
                Uri uri = new Uri(url.StartsWith("http") ? url : "http://" + url);
                string domain = uri.Host;

                // Google favicon service
                string faviconUrl = $"https://www.google.com/s2/favicons?domain={domain}&sz=64";

                // Create BitmapImage from the favicon URL
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(faviconUrl);
                bitmap.DecodePixelWidth = 32; // Optimize memory
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                return bitmap;
            }
            catch (Exception ex)
            {
                return null; 
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Method for File Path to Icon using Windows API
    internal class IconToImage : IValueConverter 
    {
        public object Convert(Object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Verify if the value is not null o empty
            string path = value as string;
            if (string.IsNullOrEmpty(path)) return null;

            try
            { 
                // Extract the icon from the file path 
                using (Icon icon = Icon.ExtractAssociatedIcon(path))
                {
                    return Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                }
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public object ConvertBack(Object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
        
    }

    internal class SmartKeywordIcon : IValueConverter
    {

        private readonly UrlToIcon _urlConverter = new UrlToIcon();
        private readonly IconToImage _fileConverter = new IconToImage();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string input = value as string;
            if (string.IsNullOrEmpty(input)) return null;

            if (input.Contains("\\") || input.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                return _fileConverter.Convert(input, targetType, parameter, culture);

            if (input.Contains("."))
                return _urlConverter.Convert(input, targetType, parameter, culture);

            return null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}