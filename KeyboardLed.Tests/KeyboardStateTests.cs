using KeyboardLed.Models;
using Xunit;

namespace KeyboardLed.Tests
{
    public class KeyboardStateTests
    {
        [Fact]
        public void FromIndex_and_ToIndex_round_trip_for_every_state()
        {
            for (int i = 0; i < KeyboardState.StateCount; i++)
            {
                Assert.Equal(i, KeyboardState.FromIndex(i).ToIndex());
            }
        }

        [Fact]
        public void Every_state_has_a_distinct_index()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < KeyboardState.StateCount; i++)
            {
                Assert.True(seen.Add(KeyboardState.FromIndex(i).GetHashCode()));
            }
        }

        [Fact]
        public void Indexer_matches_properties()
        {
            var state = new KeyboardState { NumLock = true, CapsLock = false, ScrollLock = true, Insert = false };

            Assert.True(state[LockKey.NumLock]);
            Assert.False(state[LockKey.CapsLock]);
            Assert.True(state[LockKey.ScrollLock]);
            Assert.False(state[LockKey.Insert]);
        }

        [Fact]
        public void Equality_compares_all_keys()
        {
            var a = new KeyboardState { NumLock = true, Insert = true };
            var b = new KeyboardState { NumLock = true, Insert = true };
            var c = new KeyboardState { NumLock = true };

            Assert.True(a.Equals(b));
            Assert.False(a.Equals(c));
            Assert.True(a.Equals((object)b));
        }

        [Theory]
        [InlineData(ForcedState.None, false, false)]
        [InlineData(ForcedState.None, true, false)]
        [InlineData(ForcedState.AlwaysOn, true, true)]
        [InlineData(ForcedState.AlwaysOn, false, false)]
        [InlineData(ForcedState.AlwaysOff, false, true)]
        [InlineData(ForcedState.AlwaysOff, true, false)]
        public void ShouldBlockToggle_blocks_only_presses_that_would_leave_the_forced_state(ForcedState forced, bool isOn, bool expected)
        {
            Assert.Equal(expected, ForcedStateRules.ShouldBlockToggle(forced, isOn));
        }

        [Theory]
        [InlineData(ForcedState.None, false, false)]
        [InlineData(ForcedState.None, true, false)]
        [InlineData(ForcedState.AlwaysOn, false, true)]
        [InlineData(ForcedState.AlwaysOn, true, false)]
        [InlineData(ForcedState.AlwaysOff, true, true)]
        [InlineData(ForcedState.AlwaysOff, false, false)]
        public void NeedsCorrection_when_state_differs_from_forced(ForcedState forced, bool isOn, bool expected)
        {
            Assert.Equal(expected, ForcedStateRules.NeedsCorrection(forced, isOn));
        }

        [Fact]
        public void Virtual_key_mapping_round_trips()
        {
            foreach (var key in LockKeys.All)
            {
                Assert.Equal(key, LockKeys.FromVirtualKey(LockKeys.VirtualKey(key)));
            }
            Assert.Null(LockKeys.FromVirtualKey(0x41)); // 'A'
        }

        [Fact]
        public void Insert_cannot_be_forced()
        {
            Assert.False(LockKeys.CanForce(LockKey.Insert));
            Assert.True(LockKeys.CanForce(LockKey.CapsLock));
        }
    }
}
