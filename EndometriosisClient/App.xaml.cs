using System.Windows;
using EndometriosisClient.Data;
using EndometriosisClient.Models;

namespace EndometriosisClient
{
    public partial class App : Application
    {
        private bool _isShuttingDown = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DbInitializer.Initialize();

            // Приложение закрываем только вручную,
            // чтобы после закрытия кабинета можно было вернуться к окну входа.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            ShowLoginWindow();
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow();

            bool? loginResult = loginWindow.ShowDialog();

            if (loginResult == true && loginWindow.LoggedUser != null)
            {
                OpenUserWindow(loginWindow.LoggedUser);
            }
            else
            {
                _isShuttingDown = true;
                Shutdown();
            }
        }

        private void OpenUserWindow(AppUser user)
        {
            Window userWindow;

            if (user.Role == UserRoles.Admin)
            {
                userWindow = new AdminWindow(user);
            }
            else
            {
                userWindow = new MainWindow(user);
            }

            MainWindow = userWindow;

            userWindow.Closed += UserWindow_Closed;

            userWindow.Show();
        }

        private void UserWindow_Closed(object sender, System.EventArgs e)
        {
            if (_isShuttingDown)
                return;

            // После закрытия кабинета пользователя снова показываем окно входа.
            ShowLoginWindow();
        }
    }
}