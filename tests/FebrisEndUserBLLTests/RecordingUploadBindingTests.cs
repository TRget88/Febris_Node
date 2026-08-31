// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// T6: nothing bound a video UPLOAD to the authenticated device.
    ///
    /// <para>
    /// <c>SplitVideos/</c> and <c>recordings/</c> are one flat namespace shared by every device and
    /// every learner, and <c>VideoUploadLogic</c> accepted any part filename from any authenticated
    /// device. One device could overwrite another device's parts, or another learner's finished
    /// recording, simply by naming its upload accordingly. The authenticated device was
    /// materialised on every request into <c>_hardware</c> and never read once.
    /// </para>
    ///
    /// <para>
    /// The gate requires two things, and they close different holes. The recording must be one this
    /// node MINTED, which removes the ability to write arbitrary filenames into the video
    /// directories at all. And it must have been minted BY THIS DEVICE, which is what stops one
    /// device overwriting another's. Comparing the device UUID directly is sound because
    /// <c>HardwareKeyAuthorization.ToHardwareClaim</c> copies <c>UUID</c> verbatim from the node's
    /// own <c>LocalHardware</c> row, so mint time and upload time are the same value in the same
    /// identity space.
    /// </para>
    ///
    /// <para>
    /// The lookup stub here is EXACT-MATCH on purpose. An <c>It.IsAny&lt;string&gt;()</c> stub is
    /// what let a previous entitlement defect ship green: a lookup stub that ignores its argument
    /// cannot test a lookup.
    /// </para>
    /// </summary>
    public class RecordingUploadBindingTests
    {
        private static readonly Guid MintingDevice = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid OtherDevice = Guid.Parse("22222222-2222-2222-2222-222222222222");
        private const string RecordingName = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

        /// <summary>The convention every producer uses: <c>{guid}.mp4.part_{index}.{count}</c>.</summary>
        private static string Part(string recording, int index, int count) =>
            recording + ".mp4.part_" + index + "." + count;

        private static RecordingLogic Build(Recording stored)
        {
            Mock<IHttpContextAccessor> accessor = new Mock<IHttpContextAccessor>();
            accessor.Setup(a => a.HttpContext).Returns(new DefaultHttpContext());

            Mock<IRecordingQueries> recordings = new Mock<IRecordingQueries>();
            recordings.Setup(r => r.GetByName(It.IsAny<string>()))
                .ReturnsAsync((string requested) =>
                    stored != null && requested == stored.Name ? stored : null);

            return new RecordingLogic(
                accessor.Object,
                recordings.Object,
                new Mock<IParentLinkedStudentQueries>().Object);
        }

        private static Recording MintedBy(Guid device) => new Recording
        {
            Name = RecordingName,
            ActorUUID = Guid.NewGuid(),
            HardwareUUID = device
        };

        // ------------------------------------------------------------------

        [Fact]
        public async Task TheMintingDeviceMayUploadItsOwnParts()
        {
            // The path that must keep working, so the gate cannot be "refuse everything".
            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(Part(RecordingName, 1, 3), MintingDevice);

            allowed.Should().BeTrue();
        }

        [Fact]
        public async Task AnotherDeviceMayNotUploadIntoSomeoneElsesRecording()
        {
            // THE defect. Both devices are authenticated; only one minted this recording.
            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(Part(RecordingName, 1, 3), OtherDevice);

            allowed.Should().BeFalse(
                "a device may only upload parts for recordings it minted, or it can overwrite another learner's session");
        }

        [Fact]
        public async Task AnUnknownRecordingNameIsRefused()
        {
            // Removes the ability to write arbitrary filenames into the shared video directories:
            // only names this node actually minted are accepted.
            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(Part("99999999-9999-9999-9999-999999999999", 1, 3), MintingDevice);

            allowed.Should().BeFalse();
        }

        [Fact]
        public async Task TheExtensionInThePartNameDoesNotDefeatTheLookup()
        {
            // The stored name is the bare Guid; the wire name carries ".mp4" before ".part_".
            // Getting this wrong is what broke the VIEW gate, where every lookup missed and the
            // deny-on-miss branch denied everything. Pinned here so the upload gate cannot repeat it.
            Part(RecordingName, 2, 5).Should().StartWith(RecordingName + ".mp4.part_");

            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(Part(RecordingName, 2, 5), MintingDevice);

            allowed.Should().BeTrue("the base name must be normalised to the form Register stores");
        }

        [Fact]
        public async Task AFileThatIsNotAPartIsRefused()
        {
            // MergeFile parses the same token and would throw on a name without it, so a
            // non-conforming name is refused before it can reach disk.
            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(RecordingName + ".mp4", MintingDevice);

            allowed.Should().BeFalse();
        }

        [Fact]
        public async Task AnUnauthenticatedOrDevicelessRequestIsRefused()
        {
            // _hardware is null on any path that did not authenticate a device, which surfaces here
            // as Guid.Empty. It must never satisfy the gate.
            bool allowed = await Build(MintedBy(MintingDevice))
                .MayAcceptPart(Part(RecordingName, 1, 3), Guid.Empty);

            allowed.Should().BeFalse();
        }

        [Fact]
        public async Task ARecordingMintedWithNoDeviceAcceptsNothing()
        {
            // Register warns when it records Guid.Empty. This is the consequence: nothing can ever
            // upload for it. Safe direction, and pinned so it is a known state rather than a
            // surprise.
            bool allowed = await Build(MintedBy(Guid.Empty))
                .MayAcceptPart(Part(RecordingName, 1, 3), MintingDevice);

            allowed.Should().BeFalse();
        }
    }
}
