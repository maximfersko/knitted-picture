using KnittedPicture.Game;
using Xunit;
namespace KnittedPicture.Core.Tests
{
    public class HomeLayoutTests
    {
        [Theory]
        [InlineData(320, 640)] [InlineData(390, 844)] [InlineData(540, 1000)]
        public void Win_reward_fits_above_continue_using_measured_height(float width, float height)
        {
            const float cardHeight = 96f;
            var compact = height < 760f;
            var top = compact ? 52f : 64f;
            var hoop = WinLayoutRaster.Hoop(width, height, cardHeight, compact, 0f);
            var rewardBottom = WinLayoutRaster.StarsTop(top, hoop) + 48f + 40f +
                WinLayoutRaster.HintHeight(height, cardHeight, compact, 0f) + cardHeight;
            var continueTop = height - 24f - 44f - 6f - 70f;
            Assert.True(rewardBottom <= continueTop - 12f + 0.001f);
            Assert.True(hoop > 1f);
        }

        [Theory]
        [InlineData(320, 2)] [InlineData(320, 3)] [InlineData(320, 4)]
        [InlineData(390, 2)] [InlineData(390, 3)] [InlineData(390, 4)]
        [InlineData(540, 2)] [InlineData(540, 3)] [InlineData(540, 4)]
        public void Event_hit_areas_fit_and_do_not_overlap(float width, int count)
        {
            var size = HomeLayoutRaster.EventWidth(width, count);
            Assert.True(size >= 44f);
            for (var i = 0; i < count; i++)
            {
                var x = HomeLayoutRaster.EventCenter(width, count, i);
                Assert.InRange(x - size / 2f, 0f, width);
                Assert.InRange(x + size / 2f, 0f, width);
                if (i > 0)
                {
                    Assert.True(x - size / 2f >= HomeLayoutRaster.EventCenter(width, count, i - 1) + size / 2f - 0.001f);
                }
            }
        }
    }
}
