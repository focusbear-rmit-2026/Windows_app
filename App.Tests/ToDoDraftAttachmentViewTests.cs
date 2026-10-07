using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using Focus_Bear.ToDoScreens;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FocusBear.App.Tests
{
    [TestClass]
    public class ToDoDraftAttachmentViewTests
    {
        private const string CaptureDirectoryVariable = "FOCUSBEAR_ATTACHMENT_CAPTURE_DIRECTORY";
        private const string CaptureName = "draft-task-attachments.png";
        private const int CaptureWidth = 640;
        private const int CaptureHeight = 240;
        private const int CaptureDpi = 96;

        private static string RepositoryRoot([CallerFilePath] string source = "") =>
            Path.GetDirectoryName(Path.GetDirectoryName(source));

        [TestMethod]
        public void NewTaskStagesFilesInTheActualAttachmentViewWithoutNetwork()
        {
            Exception failure = null;
            var thread = new Thread(() =>
            {
                string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(directory);
                try
                {
                    var view = new ToDoTaskAttachments(Guid.NewGuid().ToString(), ToDoStrings.Fallbacks(), isDraft: true);
                    string file = Path.Combine(directory, "Meeting slides.pdf");
                    File.WriteAllBytes(file, new byte[] { 1 });
                    view.UploadAsync(file).GetAwaiter().GetResult();
                    Assert.IsTrue(view.HasPendingFiles);
                    Assert.AreEqual("Meeting slides.pdf", view.Draft.Files[0].FileName);
                    Assert.IsFalse(view.Draft.HasSavedTask);

                    var captureDirectory = Environment.GetEnvironmentVariable(CaptureDirectoryVariable);
                    if (string.IsNullOrEmpty(captureDirectory)) return;
                    var container = new Border { Child = view, Background = Brushes.White, Padding = new Thickness(24) };
                    // Use the real theme's brushes without constructing the app or loading account settings.
                    var theme = XDocument.Load(Path.Combine(RepositoryRoot(), "Theme", "LightTheme.xaml"));
                    XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
                    foreach (var brush in theme.Root.Elements())
                        if (brush.Name.LocalName == "SolidColorBrush" && brush.Attribute(xaml + "Key") != null)
                            container.Resources[(string)brush.Attribute(xaml + "Key")] = new SolidColorBrush((Color)ColorConverter.ConvertFromString((string)brush.Attribute("Color")));
                    var size = new Size(CaptureWidth, CaptureHeight);
                    container.Measure(size);
                    container.Arrange(new Rect(size));
                    container.UpdateLayout();
                    Assert.IsTrue(view.ActualWidth > 0 && view.ActualHeight <= CaptureHeight);
                    var image = new RenderTargetBitmap(CaptureWidth, CaptureHeight, CaptureDpi, CaptureDpi, PixelFormats.Pbgra32);
                    image.Render(container);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(image));
                    Directory.CreateDirectory(captureDirectory);
                    using (var output = File.Create(Path.Combine(captureDirectory, CaptureName))) encoder.Save(output);
                }
                catch (Exception exception) { failure = exception; }
                finally { Directory.Delete(directory, recursive: true); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) throw new AssertFailedException(failure.Message, failure);
        }
    }
}
