// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// The absolute-session-cap logic (<see cref="AbsoluteSessionTimeout"/>) behind
    /// Session.AbsoluteTimeoutMinutes: stamp once at sign-in, anchor the cap to the FIRST sign-in (not the
    /// last activity, so sliding cannot defeat it), expire at/after the deadline, and fail OPEN on a
    /// missing or garbage stamp. Time is injected so the checks are deterministic.
    /// </summary>
    public class AbsoluteSessionTimeoutTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

        [Fact]
        public void Stamp_NullTimeout_DoesNotStamp()
        {
            var props = new AuthenticationProperties();

            AbsoluteSessionTimeout.Stamp(props, null, T0);

            props.Items.Should().NotContainKey(AbsoluteSessionTimeout.DeadlineKey);
        }

        [Fact]
        public void Stamp_NonPositiveTimeout_DoesNotStamp()
        {
            var props = new AuthenticationProperties();

            AbsoluteSessionTimeout.Stamp(props, 0, T0);
            AbsoluteSessionTimeout.Stamp(props, -5, T0);

            props.Items.Should().NotContainKey(AbsoluteSessionTimeout.DeadlineKey);
        }

        [Fact]
        public void Stamp_SetsDeadline_AtNowPlusTimeout()
        {
            var props = new AuthenticationProperties();

            AbsoluteSessionTimeout.Stamp(props, 60, T0);

            props.Items.Should().ContainKey(AbsoluteSessionTimeout.DeadlineKey);
            AbsoluteSessionTimeout.IsExpired(props, T0.AddMinutes(59)).Should().BeFalse();
            AbsoluteSessionTimeout.IsExpired(props, T0.AddMinutes(61)).Should().BeTrue();
        }

        [Fact]
        public void Stamp_IsIdempotent_AnchorsToFirstSignIn()
        {
            var props = new AuthenticationProperties();
            AbsoluteSessionTimeout.Stamp(props, 60, T0);
            string first = props.Items[AbsoluteSessionTimeout.DeadlineKey];

            // A later stamp (re-entrancy; sliding must not re-stamp) must NOT push the cap out.
            AbsoluteSessionTimeout.Stamp(props, 60, T0.AddMinutes(30));

            props.Items[AbsoluteSessionTimeout.DeadlineKey].Should().Be(first);
            AbsoluteSessionTimeout.IsExpired(props, T0.AddMinutes(61)).Should()
                .BeTrue("the cap anchors to the first sign-in, so activity cannot extend it past the original deadline");
        }

        [Fact]
        public void IsExpired_NoStamp_IsFalse()
        {
            AbsoluteSessionTimeout.IsExpired(new AuthenticationProperties(), T0).Should().BeFalse();
        }

        [Fact]
        public void IsExpired_ExactlyAtDeadline_IsExpired()
        {
            var props = new AuthenticationProperties();
            AbsoluteSessionTimeout.Stamp(props, 60, T0);

            AbsoluteSessionTimeout.IsExpired(props, T0.AddMinutes(60)).Should().BeTrue();
        }

        [Fact]
        public void IsExpired_GarbageStamp_FailsOpen()
        {
            var props = new AuthenticationProperties();
            props.Items[AbsoluteSessionTimeout.DeadlineKey] = "not-a-date";

            AbsoluteSessionTimeout.IsExpired(props, T0.AddYears(10)).Should().BeFalse();
        }
    }
}
