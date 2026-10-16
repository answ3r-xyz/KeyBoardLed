using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Egypéldányos futás: a második példány jelez az elsőnek (megnyitja a beállításokat), majd kilép.
    /// </summary>
    public sealed class SingleInstance : IDisposable
    {
        private const string Id = "KeyboardLed-7F3A2C1E";

        private readonly Mutex _mutex;
        private readonly EventWaitHandle _showEvent;
        private RegisteredWaitHandle? _registration;

        public bool IsFirstInstance { get; }

        public SingleInstance()
        {
            _mutex = new Mutex(true, $@"Local\{Id}.Mutex", out bool createdNew);
            IsFirstInstance = createdNew;
            _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $@"Local\{Id}.Show");
        }

        public void SignalFirstInstance()
        {
            // Az előtérben lévő (most indított) példány engedi, hogy a futó példány ablaka előtérbe kerüljön
            AllowSetForegroundWindow(ASFW_ANY);
            _showEvent.Set();
        }

        private const int ASFW_ANY = -1;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        /// <summary>
        /// A callback thread pool szálon fut, a hívónak kell a UI szálra továbbítania
        /// </summary>
        public void ListenForShowRequests(Action onShowRequested)
        {
            _registration = ThreadPool.RegisterWaitForSingleObject(
                _showEvent, (_, _) => onShowRequested(), null, Timeout.Infinite, executeOnlyOnce: false);
        }

        public void Dispose()
        {
            _registration?.Unregister(null);
            _showEvent.Dispose();
            if (IsFirstInstance)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (ApplicationException)
                {
                    // Nem ez a szál birtokolja (pl. leállítás más szálról) - a rendszer úgyis felszabadítja
                }
            }
            _mutex.Dispose();
        }
    }
}
