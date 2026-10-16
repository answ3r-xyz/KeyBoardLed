using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using KeyboardLed.Models;
using KeyboardLed.Services;

namespace KeyboardLed
{
    public partial class App : Application
    {
        private SingleInstance? _singleInstance;
        private MainWindow? _main;
        private bool _errorShown;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            bool minimized = e.Args.Contains("--minimized", StringComparer.OrdinalIgnoreCase);

            _singleInstance = new SingleInstance();
            if (!_singleInstance.IsFirstInstance)
            {
                // Már fut: kézi indításnál megnyitjuk a futó példány ablakát
                if (!minimized)
                {
                    _singleInstance.SignalFirstInstance();
                }
                Shutdown();
                return;
            }

            RegisterGlobalExceptionHandlers();
            Log.Info($"KeyboardLed {AppInfo.Version} starting");

            try
            {
                AutoStartManager.RefreshPathIfEnabled();

                var settings = Settings.Load(DisplayInfo.SystemScale);
                Loc.Instance.SetLanguage(settings.Language);
                ThemeManager.Apply(settings.Theme);

                _main = new MainWindow(settings);
                MainWindow = _main;
                if (!minimized)
                {
                    _main.ShowSettingsWindow();
                }

                _singleInstance.ListenForShowRequests(() =>
                    Dispatcher.BeginInvoke(new Action(() => _main?.ShowSettingsWindow())));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Startup failed");
                MessageBox.Show(Loc.Format("StartupErrorMessage", Log.LogPath), Loc.T("ErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }

        protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
        {
            _main?.Cleanup();
            base.OnSessionEnding(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_main != null)
            {
                _main.Cleanup();
                Log.Info("KeyboardLed exiting");
            }
            _singleInstance?.Dispose();
            base.OnExit(e);
        }

        private void RegisterGlobalExceptionHandlers()
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Log.Error(args.ExceptionObject as Exception, $"Unhandled exception (terminating: {args.IsTerminating})");

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                Log.Error(args.Exception, "Unobserved task exception");
                args.SetObserved();
            };
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error(e.Exception, "Unhandled UI exception");

            // Tálca alkalmazás: inkább fusson tovább, mint hogy eltűnjön. Csak egyszer szólunk.
            e.Handled = true;
            if (!_errorShown)
            {
                _errorShown = true;
                MessageBox.Show(Loc.Format("ErrorMessage", Log.LogPath), Loc.T("ErrorTitle"),
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
