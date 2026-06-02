using System.Windows;
using EndometriosisClient.Models;
using EndometriosisClient.Services;

namespace EndometriosisClient
{
    public partial class LoginWindow : Window
    {
        private readonly AuthService _authService;

        public AppUser LoggedUser { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            _authService = new AuthService();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            string login = LoginTextBox.Text.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
            {
                MessageBox.Show("Введите логин и пароль.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var user = _authService.Login(login, password);

            if (user == null)
            {
                MessageBox.Show("Неверный логин или пароль.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            LoggedUser = user;
            DialogResult = true;

            try
            {
                var mainWindow = new MainWindow(user);
                Application.Current.MainWindow = mainWindow;
                mainWindow.Show();

                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Не удалось открыть главное окно:\n{ex.Message}",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void RegisterButton_Click(object sender, RoutedEventArgs e)
        {
            var registrationWindow = new RegistrationWindow
            {
                Owner = this
            };

            bool? result = registrationWindow.ShowDialog();

            if (result == true && registrationWindow.RegisteredUser != null)
            {
                LoggedUser = registrationWindow.RegisteredUser;
                DialogResult = true;
                Close();
            }
        }
    }
}