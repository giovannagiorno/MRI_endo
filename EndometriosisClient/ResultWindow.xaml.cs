using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace EndometriosisClient
{
    public partial class ResultWindow : Window
    {
        private readonly string _previewImagePath;
        private readonly string _resultImagePath;

        public ResultWindow(
            string patientName,
            int patientAge,
            string fileName,
            string conclusion,
            string previewImagePath,
            string resultImagePath)
        {
            InitializeComponent();

            _previewImagePath = previewImagePath;
            _resultImagePath = resultImagePath;

            PatientInfoTextBlock.Text = $"Пациент: {patientName}, возраст: {patientAge}";
            StudyInfoTextBlock.Text = $"Файл исследования: {fileName}";
            ConclusionTextBlock.Text = $"Заключение: {conclusion}";

            LoadImage(PreviewImage, previewImagePath, "Файл превью МРТ не найден.");
            LoadImage(ResultImage, resultImagePath, "Файл изображения результата не найден.");
        }

        private void LoadImage(System.Windows.Controls.Image imageControl, string imagePath, string errorMessage)
        {
            if (File.Exists(imagePath))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                imageControl.Source = bitmap;
            }
            else
            {
                MessageBox.Show(errorMessage, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DownloadResultsButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Выберите папку для сохранения результатов"
            };

            if (dialog.ShowDialog() != true)
                return;

            string targetFolder = dialog.FolderName;
            string sourceFolder = Path.GetDirectoryName(_resultImagePath);

            if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            {
                MessageBox.Show(
                    "Папка с результатами не найдена.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                return;
            }

            string resultFolder = Path.Combine(
                targetFolder,
                $"segmentation_result_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}");

            Directory.CreateDirectory(resultFolder);

            foreach (string filePath in Directory.GetFiles(sourceFolder))
            {
                string fileName = Path.GetFileName(filePath);
                string destinationPath = Path.Combine(resultFolder, fileName);

                File.Copy(filePath, destinationPath, overwrite: true);
            }

            MessageBox.Show(
                $"Результаты сохранены в папку:\n{resultFolder}",
                "Готово",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}