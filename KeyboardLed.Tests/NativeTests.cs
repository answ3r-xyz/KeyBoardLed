using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using KeyboardLed.Models;
using KeyboardLed.Services;
using Xunit;

namespace KeyboardLed.Tests
{
    public class TrayIconRendererTests
    {
        private const uint GR_USEROBJECTS = 1;

        [DllImport("user32.dll")]
        private static extern uint GetGuiResources(IntPtr hProcess, uint uiFlags);

        private static uint UserObjects()
        {
            using var process = Process.GetCurrentProcess();
            return GetGuiResources(process.Handle, GR_USEROBJECTS);
        }

        [Theory]
        [InlineData(16, 3)]
        [InlineData(16, 4)]
        [InlineData(20, 3)]
        [InlineData(24, 4)]
        [InlineData(32, 4)]
        public void Bars_fit_inside_icon_without_overlapping(int size, int count)
        {
            var bars = TrayIconRenderer.LayoutBars(size, count);

            Assert.Equal(count, bars.Length);
            Assert.All(bars, b =>
            {
                Assert.True(b.Height >= 1);
                Assert.True(b.Left >= 0 && b.Right <= size);
                Assert.True(b.Top >= 0 && b.Bottom <= size);
            });
            Assert.Single(bars.Select(b => b.Height).Distinct());
            for (int i = 1; i < bars.Length; i++)
            {
                Assert.True(bars[i].Top >= bars[i - 1].Bottom, "bars overlap");
            }
        }

        [Fact]
        public void Icons_are_rendered_at_requested_size()
        {
            using var renderer = new TrayIconRenderer();
            renderer.Update(24, lightTheme: false, includeInsert: true);

            var icon = renderer.Get(new KeyboardState { CapsLock = true, Insert = true });
            Assert.Equal(24, icon.Width);
            Assert.Equal(24, icon.Height);
        }

        [Fact]
        public void Update_is_noop_when_nothing_changed()
        {
            using var renderer = new TrayIconRenderer();
            Assert.NotNull(renderer.Update(16, false, false));
            Assert.Null(renderer.Update(16, false, false));
            var previous = renderer.Update(16, true, false);
            Assert.NotNull(previous);
            TrayIconRenderer.DisposeIcons(previous);
        }

        [Fact]
        public void Dispose_frees_all_icon_handles()
        {
            // Regresszió: Icon.FromHandle ikonok Dispose-a nem szabadította fel a HICON-t
            uint before = UserObjects();
            for (int round = 0; round < 5; round++)
            {
                using var renderer = new TrayIconRenderer();
                renderer.Update(32, lightTheme: round % 2 == 0, includeInsert: true);
            }
            uint after = UserObjects();

            Assert.True(after - before < 5, $"leaked {after - before} USER objects");
        }
    }

    public class KeyboardInputTests
    {
        [Fact]
        public void Input_struct_matches_native_size()
        {
            // SendInput elutasítja a hívást, ha a cbSize nem egyezik a natív INPUT méretével
            Assert.Equal(IntPtr.Size == 8 ? 40 : 28, KeyboardInput.InputSize);
        }
    }
}
