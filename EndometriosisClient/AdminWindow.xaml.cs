using System;
using System.Windows;
using EndometriosisClient.Models;
using EndometriosisClient.Services;

namespace EndometriosisClient
{
    public partial class AdminWindow : Window
    {
        private readonly AppUser _currentUser;
        private readonly DatabaseService _databaseService;

        public AdminWindow(AppUser currentUser)
        {
            InitializeComponent();

            _currentUser = currentUser;
            _databaseService = new DatabaseService();

            Title = $"Панель администратора — {_currentUser.FullName}";
        }

        private void AddDoctorButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullName = DoctorFullNameTextBox.Text.Trim();
                string login = DoctorLoginTextBox.Text.Trim();
                string password = DoctorPasswordBox.Password;

                if (string.IsNullOrWhiteSpace(fullName) ||
                    string.IsNullOrWhiteSpace(login) ||
                    string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Заполните ФИО, логин и пароль врача.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                var doctor = _databaseService.AddDoctor(fullName, login, password);

                StatusTextBlock.Text = $"Врач успешно добавлен. ID: {doctor.Id}";

                DoctorFullNameTextBox.Clear();
                DoctorLoginTextBox.Clear();
                DoctorPasswordBox.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении врача:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void OpenStudiesButton_Click(object sender, RoutedEventArgs e)
        {
            var studyListWindow = new StudyListWindow(_currentUser);
            studyListWindow.ShowDialog();
        }
    }
}