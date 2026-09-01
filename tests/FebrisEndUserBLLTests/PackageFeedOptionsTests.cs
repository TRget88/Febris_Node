// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.UserNode.Portal.BackgroundTasks;
using Xunit;

namespace Febris.UserNode.Portal.Tests
{
    /// <summary>
    /// Pins the configuration contract of the scheduled feed sync.
    ///
    /// <para>
    /// The property that matters most is that it is OFF unless an operator names a feed. With the
    /// manual upload path removed, this service is the only thing that can fill a node's catalogue,
    /// which makes it tempting to default it on. It must not be: a node that reaches out to a
    /// maintainer-chosen host without being asked would break the offline-first posture and the
    /// air-gapped deployment path, both of which are stated guarantees.
    /// </para>
    /// </summary>
    public class PackageFeedOptionsTests
    {
        [Fact]
        public void No_feed_is_configured_by_default()
        {
            // A fresh node must reach out to nothing. Changing this default is a product decision
            // about phoning home, not a convenience tweak, which is why it is pinned here.
            PackageFeedOptions options = new PackageFeedOptions();

            Assert.Equal(string.Empty, options.Url);
        }

        [Fact]
        public void The_defaults_are_sane_once_a_feed_is_named()
        {
            PackageFeedOptions options = new PackageFeedOptions();

            Assert.Equal("stable", options.Channel);
            Assert.Equal(24, options.IntervalHours);
        }

        [Fact]
        public void The_section_name_matches_what_startup_binds()
        {
            // The architecture suite checks that every appsettings section has a reader by looking
            // for the literal. If this constant drifts from the JSON key the section silently stops
            // binding and the sync goes idle with no error anywhere.
            Assert.Equal("PackageFeed", PackageFeedOptions.SectionName);
        }
    }
}
