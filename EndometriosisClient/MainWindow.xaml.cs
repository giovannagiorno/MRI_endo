using EndometriosisClient.Models;
using EndometriosisClient.Services;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace EndometriosisClient
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly FileStorageService _fileStorageService;
        private readonly OnnxSegmentationService _segmentationService;
        private readonly AppUser _currentUser;

        private string _selectedMriFilePath = string.Empty;

        public MainWindow(AppUser currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;

            _databaseService = new DatabaseService();
            _fileStorageService = new FileStorageService();
            _segmentationService = new OnnxSegmentationService();

            Title = $"Система анализа МРТ — {_currentUser.FullName}";
        }

        private void SelectMriFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Выберите файл МРТ",
                Filter = "NIfTI files (*.nii;*.nii.gz)|*.nii;*.nii.gz|All files (*.*)|*.*"
            };

            bool? result = openFileDialog.ShowDialog();

            if (result == true)
            {
                _selectedMriFilePath = openFileDialog.FileName;
                MriFilePathTextBox.Text = _selectedMriFilePath;
                StatusTextBlock.Text = "Файл МРТ выбран.";
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullName = FullNameTextBox.Text.Trim();
                string notes = NotesTextBox.Text.Trim();
                string ageText = AgeTextBox.Text.Trim();

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    MessageBox.Show("Введите ФИО пациента.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(ageText, out int age) || age <= 0)
                {
                    MessageBox.Show("Введите корректный возраст.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(_selectedMriFilePath))
                {
                    MessageBox.Show("Выберите файл МРТ.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SaveButton.IsEnabled = false;
                SegmentationProgressBar.Visibility = Visibility.Visible;
                SegmentationProgressBar.IsIndeterminate = false;
                SegmentationProgressBar.Value = 0;
                ProgressPercentTextBlock.Text = "0%";
                StatusTextBlock.Text = "Сохранение данных и локальная сегментация МРТ...";

                string savedMriPath = _fileStorageService.SaveMRIFile(_selectedMriFilePath);
                string fileName = Path.GetFileName(savedMriPath);
                string fileType = GetFileType(savedMriPath);

                var patient = _databaseService.AddPatient(fullName, age, notes, _currentUser.Id);
                var study = _databaseService.AddMRIStudy(patient.Id, savedMriPath, fileName, fileType);

                var progress = new Progress<int>(percent =>
                {
                    SegmentationProgressBar.Value = percent;
                    ProgressPercentTextBlock.Text = $"{percent}%";
                    StatusTextBlock.Text = $"Сегментация МРТ: {percent}%";
                });

                var segmentationResponse = await Task.Run(() =>
                    _segmentationService.SegmentMri(savedMriPath, progress));

                var segmentationResult = _databaseService.AddSegmentationResult(
                    study.Id,
                    segmentationResponse.ResultImagePath,
                    segmentationResponse.Status,
                    segmentationResponse.Conclusion,
                    segmentationResponse.PreviewImagePath);

                StatusTextBlock.Text =
                    $"Данные успешно сохранены.\n" +
                    $"Пациент ID: {patient.Id}\n" +
                    $"Исследование ID: {study.Id}\n" +
                    $"Результат ID: {segmentationResult.Id}";

                var resultWindow = new ResultWindow(
                    patient.FullName,
                    patient.Age,
                    study.FileName,
                    segmentationResult.Conclusion,
                    segmentationResponse.PreviewImagePath,
                    segmentationResult.ResultPath);

                resultWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                string fullError = ex.Message;

                if (ex.InnerException != null)
                    fullError += "\n\nВнутренняя ошибка:\n" + ex.InnerException.Message;

                MessageBox.Show($"Ошибка при сохранении данных:\n{fullError}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                StatusTextBlock.Text = "Произошла ошибка при сохранении.";
            }
            finally
            {
                SaveButton.IsEnabled = true;
                SegmentationProgressBar.Visibility = Visibility.Collapsed;
                SegmentationProgressBar.IsIndeterminate = false;
                ProgressPercentTextBlock.Text = string.Empty;
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            FullNameTextBox.Clear();
            AgeTextBox.Clear();
            NotesTextBox.Clear();
            MriFilePathTextBox.Clear();

            _selectedMriFilePath = string.Empty;
            StatusTextBlock.Text = "Форма очищена.";
        }

        private void OpenStudyListButton_Click(object sender, RoutedEventArgs e)
        {
            var studyListWindow = new StudyListWindow(_currentUser);
            studyListWindow.ShowDialog();
        }

        private string GetFileType(string filePath)
        {
            if (filePath.EndsWith(".nii.gz", StringComparison.OrdinalIgnoreCase))
                return "NIfTI (.nii.gz)";

            if (filePath.EndsWith(".nii", StringComparison.OrdinalIgnoreCase))
                return "NIfTI (.nii)";

            return "Unknown";
        }
    }
}