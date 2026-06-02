using System.Windows;
using EndometriosisClient.Data;
using EndometriosisClient.Models;

namespace EndometriosisClient
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DbInitializer.Initialize();

            // Важно: пока открыто окно авторизации, нельзя завершать приложение
            // после закрытия последнего окна.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var loginWindow = new LoginWindow();
            bool? loginResult = loginWindow.ShowDialog();

            if (loginResult == true && loginWindow.LoggedUser != null)
            {
                Window userWindow;

                if (loginWindow.LoggedUser.Role == UserRoles.Admin)
                {
                    userWindow = new AdminWindow(loginWindow.LoggedUser);
                }
                else
                {
                    userWindow = new MainWindow(loginWindow.LoggedUser);
                }

                // Назначаем главное окно приложения
                MainWindow = userWindow;

                // Теперь приложение должно закрываться только после закрытия личного кабинета
                ShutdownMode = ShutdownMode.OnMainWindowClose;

                userWindow.Show();
            }
            else
            {
                Shutdown();
            }
        }
    }
}