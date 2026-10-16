using KeyboardLed.Models;
using KeyboardLed.Services;
using Xunit;

namespace KeyboardLed.Tests
{
    public class OverlayPlacementTests
    {
        // 1920x1040 munkaterület egy másodlagos monitoron, 40 px tálcával alul
        private static readonly PixelRect WorkArea = new(1920, 0, 3840, 1040);
        private const int W = 200;
        private const int H = 40;

        [Theory]
        [InlineData(OverlayAnchor.TopLeft, 1920 + 10, 0 + 20)]
        [InlineData(OverlayAnchor.TopCenter, 1920 + (1920 - W) / 2 + 10, 20)]
        [InlineData(OverlayAnchor.TopRight, 3840 - W - 10, 20)]
        [InlineData(OverlayAnchor.BottomLeft, 1920 + 10, 1040 - H - 20)]
        [InlineData(OverlayAnchor.BottomCenter, 1920 + (1920 - W) / 2 + 10, 1040 - H - 20)]
        [InlineData(OverlayAnchor.BottomRight, 3840 - W - 10, 1040 - H - 20)]
        public void Compute_places_window_relative_to_anchor(OverlayAnchor anchor, int expectedX, int expectedY)
        {
            var (x, y) = OverlayPlacement.Compute(WorkArea, W, H, anchor, 10, 20);

            Assert.Equal(expectedX, x);
            Assert.Equal(expectedY, y);
        }

        [Theory]
        [InlineData(OverlayAnchor.TopLeft)]
        [InlineData(OverlayAnchor.TopCenter)]
        [InlineData(OverlayAnchor.TopRight)]
        [InlineData(OverlayAnchor.BottomLeft)]
        [InlineData(OverlayAnchor.BottomCenter)]
        [InlineData(OverlayAnchor.BottomRight)]
        public void ComputeOffsets_is_inverse_of_Compute(OverlayAnchor anchor)
        {
            var (x, y) = OverlayPlacement.Compute(WorkArea, W, H, anchor, 37, 15);
            var (offsetX, offsetY) = OverlayPlacement.ComputeOffsets(WorkArea, x, y, W, H, anchor);

            Assert.Equal(37, offsetX);
            Assert.Equal(15, offsetY);
        }

        [Fact]
        public void Huge_offset_is_clamped_inside_work_area()
        {
            var (x, y) = OverlayPlacement.Compute(WorkArea, W, H, OverlayAnchor.TopLeft, 99999, -99999);

            Assert.Equal(WorkArea.Right - W, x);
            Assert.Equal(WorkArea.Top, y);
        }

        [Fact]
        public void Clamp_pulls_back_a_window_left_on_a_disconnected_monitor()
        {
            // Korábban a jobb oldali monitoron volt, most csak a 0..1920 monitor létezik
            var primary = new PixelRect(0, 0, 1920, 1080);
            var (x, y) = OverlayPlacement.Clamp(primary, W, H, 3000, 500);

            Assert.Equal(1920 - W, x);
            Assert.Equal(500, y);
        }

        [Fact]
        public void Clamp_keeps_left_edge_visible_when_window_is_wider_than_area()
        {
            var small = new PixelRect(0, 0, 100, 100);
            var (x, y) = OverlayPlacement.Clamp(small, 300, 50, 50, 10);

            Assert.Equal(0, x);
            Assert.Equal(10, y);
        }
    }
}
