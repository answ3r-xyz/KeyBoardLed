using System;
using System.Windows.Threading;
using KeyboardLed.Models;
using Microsoft.Win32;

namespace KeyboardLed.Services
{
    /// <summary>
    /// Állapotfigyelő: a hook eseményeire azonnal (rövid gyors pollinggal) frissít,
    /// egyébként csak ritka tartalék polling fut (pl. ha emelt jogú ablak van fókuszban,
    /// ahová a hook nem lát be, vagy más program váltja a LED-et).
    /// </summary>
    public sealed class StateMonitor : IDisposable
    {
        private static readonly TimeSpan IdleInterval = TimeSpan.FromMilliseconds(500);
        private static readonly TimeSpan FastInterval = TimeSpan.FromMilliseconds(25);
        private static readonly TimeSpan CorrectionCooldown = TimeSpan.FromSeconds(1);
        private const int FastTicks = 12; // ~300 ms a billentyű esemény után

        private readonly Settings _settings;
        private readonly KeyboardHook _hook = new();
        private readonly DispatcherTimer _timer;
        private readonly DateTime[] _lastCorrection = new DateTime[LockKeys.All.Length];
        private int _fastTicksLeft;
        private bool _started;

        public KeyboardState Current { get; private set; }

        /// <summary>
        /// (előző, új) állapot
        /// </summary>
        public event Action<KeyboardState, KeyboardState>? StateChanged;

        public StateMonitor(Settings settings)
        {
            _settings = settings;
            _hook.ForcedStateProvider = GetForcedState;
            _hook.LockKeyActivity += OnLockKeyActivity;

            _timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = IdleInterval };
            _timer.Tick += Timer_Tick;

            Current = Read();
        }

        public void Start()
        {
            if (_started) return;
            _started = true;

            _hook.Start();
            _timer.Start();
            SystemEvents.SessionSwitch += OnSessionSwitch;
            EnforceForcedStates();
        }

        /// <summary>
        /// Azonnali újraolvasás (pl. beállítás változás után)
        /// </summary>
        public void Refresh()
        {
            var state = Read();
            if (!state.Equals(Current))
            {
                var previous = Current;
                Current = state;
                StateChanged?.Invoke(previous, state);
            }
            EnforceForcedStates();
        }

        private ForcedState GetForcedState(LockKey key) =>
            LockKeys.CanForce(key) ? _settings.Keys[key].Forced : ForcedState.None;

        private KeyboardState Read() => KeyStateReader.GetCurrentState(_settings.TrackInsert);

        private void OnLockKeyActivity()
        {
            _fastTicksLeft = FastTicks;
            if (_timer.Interval != FastInterval)
            {
                _timer.Interval = FastInterval;
            }
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_fastTicksLeft > 0 && --_fastTicksLeft == 0)
            {
                _timer.Interval = IdleInterval;
            }
            Refresh();
        }

        private void EnforceForcedStates()
        {
            if (!_started) return;

            foreach (var key in LockKeys.All)
            {
                if (!ForcedStateRules.NeedsCorrection(GetForcedState(key), Current[key]))
                {
                    continue;
                }

                // Ha a váltás nem "ragad meg" (pl. UIPI blokkolja), ne próbálkozzunk folyamatosan
                var now = DateTime.UtcNow;
                if (now - _lastCorrection[(int)key] < CorrectionCooldown)
                {
                    continue;
                }
                _lastCorrection[(int)key] = now;

                KeyboardInput.Toggle(key);
                OnLockKeyActivity();
            }
        }

        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            // Feloldás után az állapot eltérhet (pl. a bejelentkező képernyő átállította)
            if (e.Reason == SessionSwitchReason.SessionUnlock || e.Reason == SessionSwitchReason.SessionLogon)
            {
                OnLockKeyActivity();
            }
        }

        public void Dispose()
        {
            _timer.Stop();
            _hook.Dispose();
            SystemEvents.SessionSwitch -= OnSessionSwitch;
        }
    }
}
