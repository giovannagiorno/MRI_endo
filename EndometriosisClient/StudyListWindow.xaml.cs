using EndometriosisClient.Models;
using EndometriosisClient.Services;
using System.Windows;
using System.IO;

namespace EndometriosisClient
{
    public partial class StudyListWindow : Window
    {
        private readonly DatabaseService _databaseService;
        private readonly FileStorageService _fileStorageService;
        private readonly MriPreviewService _mriPreviewService;
        private readonly AppUser _currentUser;

        public StudyListWindow(AppUser currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;

            _databaseService = new DatabaseService();
            _fileStorageService = new FileStorageService();
            _mriPreviewService = new MriPreviewService();

            LoadStudies();
        }

        private void LoadStudies()
        {
            var studies = _databaseService.GetStudyListItems(_currentUser);
            StudiesDataGrid.ItemsSource = studies;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStudies();
        }

        private void OpenResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (StudiesDataGrid.SelectedItem is not StudyListItem selectedStudy)
            {
                MessageBox.Show("Выберите исследование из списка.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedStudy.ResultPath) ||
                !File.Exists(selectedStudy.ResultPath))
            {
                MessageBox.Show("Для этого исследования результат сегментации отсутствует.",
                    "Информация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(selectedStudy.PreviewImagePath) ||
                !File.Exists(selectedStudy.PreviewImagePath))
            {
                MessageBox.Show(
                    "Для этого исследования не найдено сохраненное МРТ-превью. " +
                    "Скорее всего, запись была создана до добавления сохранения превью. " +
                    "Повторите сегментацию для этого исследования.",
                    "Превью отсутствует",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var resultWindow = new ResultWindow(
                selectedStudy.PatientName,
                selectedStudy.Age,
                selectedStudy.FileName,
                selectedStudy.Conclusion,
                selectedStudy.PreviewImagePath,
                selectedStudy.ResultPath);

            resultWindow.ShowDialog();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}