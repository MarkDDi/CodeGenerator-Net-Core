using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Image = SixLabors.ImageSharp.Image;

namespace CodeGenerator.Views;

public partial class ShowImageWindow : Window
{
    public ShowImageWindow(FileInfo file)
    {
        InitializeComponent();

        Title = file.Name;

        var imageInfo = Image.Identify($"{file.FullName}");
        var drawingImage = new DrawingImage();
        var imageDrawing = new ImageDrawing
        {
            ImageSource = new Bitmap(file.FullName),
            Rect = new Rect(0, 0, imageInfo.Width, imageInfo.Height)
        };
        drawingImage.Drawing = imageDrawing;
        BigImageViewer.Source = drawingImage;
    }
}