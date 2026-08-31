// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T11: the analytics table grew one row per HTTP request, on both hosts, into one shared table
    /// with no retention and exactly one index.
    ///
    /// <para>
    /// Two things dominated the volume and neither was analytics in any useful sense. Container
    /// health probes hit <c>/health/ready</c> on each host every 15 seconds, which is 11,520 rows a
    /// day on a node with ZERO users. And a single Portal page view wrote a row for every stylesheet,
    /// script, font and favicon it pulled in: measured on the development database, the highest-count
    /// paths were <c>bootstrap.min.css</c>, <c>custom.min.css</c>, <c>favicon.ico</c> and
    /// <c>jquery.min.js</c>.
    /// </para>
    ///
    /// <para>
    /// These tests pin the boundary between NOISE and HISTORY. Getting that boundary wrong in the
    /// other direction would silently drop real traffic from the record, which is worse than the
    /// volume problem it fixes.
    /// </para>
    /// </summary>
    public class AnalyticsVolumeTests
    {
        // ------------------------------------------------------------------
        // What must NOT be recorded
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("/health")]
        [InlineData("/health/ready")]
        [InlineData("/health/live")]
        [InlineData("/HEALTH/READY")]
        public void HealthProbesAreNotRecorded(string path)
        {
            // The single largest contributor on an idle node, and it records the container asking
            // itself whether it is alive. Matched by PREFIX so a future sibling endpoint is excluded
            // by default rather than by somebody remembering to add it.
            AnalyticsRequestFilter.ShouldRecord(new PathString(path)).Should().BeFalse();
        }

        [Theory]
        [InlineData("/gentelella-master/vendors/bootstrap/dist/css/bootstrap.min.css")]
        [InlineData("/gentelella-master/build/css/custom.min.css")]
        [InlineData("/gentelella-master/vendors/jquery/dist/jquery.min.js")]
        [InlineData("/js/site.js.map")]
        [InlineData("/favicon.ico")]
        [InlineData("/images/logo.png")]
        [InlineData("/fonts/glyphicons.woff2")]
        [InlineData("/STYLES/APP.CSS")]
        public void StaticAssetsAreNotRecorded(string path)
        {
            AnalyticsRequestFilter.ShouldRecord(new PathString(path)).Should().BeFalse();
        }

        // ------------------------------------------------------------------
        // What must STILL be recorded
        // ------------------------------------------------------------------

        [Theory]
        [InlineData("/")]
        [InlineData("/Identity/Account/Login")]
        [InlineData("/Statement/IndexPartial")]
        [InlineData("/Widget/LoadPartialDetail")]
        [InlineData("/api/Statement/Submit")]
        [InlineData("/Cohort/ManageMemberIndex")]
        public void RealTrafficIsStillRecorded(string path)
        {
            // The failure mode on the other side of this boundary. A filter that quietly swallowed
            // real requests would leave the analytics screens lying about what happened, which is a
            // worse defect than the row volume it was written to fix.
            AnalyticsRequestFilter.ShouldRecord(new PathString(path)).Should().BeTrue();
        }

        [Fact]
        public void AFailedOrUnauthenticatedRequestIsStillRecorded()
        {
            // Filtering is by PATH only, never by outcome. A rejected login attempt is exactly the
            // kind of event an operator looks for.
            AnalyticsRequestFilter.ShouldRecord(new PathString("/Identity/Account/Login")).Should().BeTrue();
            AnalyticsRequestFilter.ShouldRecord(new PathString("/Admin/Secret")).Should().BeTrue();
        }

        [Theory]
        [InlineData("/users/first.last@example.com/profile")]
        [InlineData("/module/v1.2.3/launch")]
        public void ARouteContainingADotIsNotMistakenForAnAsset(string path)
        {
            // Why the filter matches an explicit extension list rather than "anything with a dot".
            // Email addresses and version strings appear in real routes.
            AnalyticsRequestFilter.ShouldRecord(new PathString(path)).Should().BeTrue();
        }

        [Fact]
        public void AnEmptyPathIsRecordedRatherThanSilentlyDropped()
        {
            // Default to KEEPING the row. An unrecognised shape should never be the reason a real
            // request vanishes from the record.
            AnalyticsRequestFilter.ShouldRecord(new PathString(null)).Should().BeTrue();
            AnalyticsRequestFilter.ShouldRecord(default(PathString)).Should().BeTrue();
        }

        // ------------------------------------------------------------------
        // The read path
        // ------------------------------------------------------------------

        [Fact]
        public void TheAnalyticsLogicExposesAPagedRead()
        {
            // The list screen used to call the unbounded Get(), materialising every row ever
            // recorded, sorting it in memory by an unindexed column and then taking 25. The paged
            // read has to exist for the controller to have anything else to call.
            MethodInfo paged = typeof(ILocalAnalyticsLogic).GetMethod("GetPage");

            paged.Should().NotBeNull("the analytics list must be able to read one page rather than the whole history");
            paged.GetParameters().Select(p => p.Name)
                .Should().Equal(new[] { "searchString", "pageNumber", "pageSize" });
        }

        [Fact]
        public void TheUnboundedReadStillExistsForTheChartAggregates()
        {
            // Deliberately NOT deleted. The chart builders aggregate over the whole history, which
            // is a legitimate use. The defect was the LIST screen using it, not its existence.
            typeof(ILocalAnalyticsLogic).GetMethod("Get", Type.EmptyTypes)
                .Should().NotBeNull();
        }
    }
}
