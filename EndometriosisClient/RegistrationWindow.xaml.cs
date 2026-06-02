using System;
using System.Windows;
using EndometriosisClient.Models;
using EndometriosisClient.Services;

namespace EndometriosisClient
{
    public partial class RegistrationWindow : Window
    {
        private readonly AuthService _authService;

        public AppUser RegisteredUser { get; private set; }

        public RegistrationWindow()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string fullName = FullNameTextBox.Text.Trim();
                string login = LoginTextBox.Text.Trim();
                string password = PasswordBox.Password;
                string repeatPassword = RepeatPasswordBox.Password;

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    MessageBox.Show("Введите ФИО пользователя.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(login))
                {
                    MessageBox.Show("Введите логин.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    MessageBox.Show("Введите пароль.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (password.Length < 6)
                {
                    MessageBox.Show("Пароль должен содержать не менее 6 символов.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                if (password != repeatPassword)
                {
                    MessageBox.Show("Пароли не совпадают.",
                        "Ошибка",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                RegisteredUser = _authService.RegisterUser(fullName, login, password);

                string roleText = RegisteredUser.Role == UserRoles.Admin
                    ? "администратор"
                    : "врач";

                MessageBox.Show($"Пользователь успешно зарегистрирован.\nРоль: {roleText}",
                    "Регистрация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при регистрации:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}