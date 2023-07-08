
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;


namespace Focus_Bear.Models
{
    class IconExtractConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            ImageSource exIconImageSource = null;

            string DefaultAppIcon = ("pack://application:,,,/Focus Bear;component/images/DefaultAppIcon.png");

            if (value != null)
            {
                var filePath = value.ToString();
                
                if (File.Exists(filePath))
                {
                    //Working Code
                    using (var exeIcon = Icon.ExtractAssociatedIcon(filePath))
                    {
                        ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                                                   exeIcon.Handle,
                                                   Int32Rect.Empty,
                                                   BitmapSizeOptions.FromEmptyOptions());

                        exIconImageSource = imageSource;
                    }                      
                }
                else  if (Directory.Exists(filePath))
                {
                   
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(DefaultAppIcon);
                    bi.EndInit();
                    return bi;
                }

                //Return default image
                if(exIconImageSource==null)
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(DefaultAppIcon);
                    bi.EndInit();
                    return bi;
                }
                return exIconImageSource;
            }
            else
            {
                //Return default image
                BitmapImage bi = new BitmapImage();
                bi.BeginInit();
                bi.UriSource = new Uri(DefaultAppIcon);
                bi.EndInit();
                return bi;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

     
     
}
