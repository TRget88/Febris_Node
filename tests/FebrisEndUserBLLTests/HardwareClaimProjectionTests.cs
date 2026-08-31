// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System;
using System.Text.Json;
using Febris.EnumLibrary;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.LogicLayer.Logic.AuthorizationLogic;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Pins HardwareKeyAuthorization.ToHardwareClaim. The EndUser hardware-token issuer
    /// now serializes a Hardware (the read-side contract every consumer of
    /// context.Items["Hardware"] expects, established by the MDM-B2 fix) projected from
    /// the tenant's LocalHardware, instead of emitting a LocalHardware and relying on
    /// lenient cross-type deserialization.
    /// </summary>
    public class HardwareClaimProjectionTests
    {
        private static LocalHardware Sample() => new LocalHardware
        {
            Id = 42,
            UUID = Guid.NewGuid(),
            TimeStamp = new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc),
            LastUpdateTimeStamp = new DateTime(2026, 6, 22, 1, 0, 0, DateTimeKind.Utc),
            HardwareTypeUUID = Guid.NewGuid(),
            HardwareTypeId = 7,
            DescriptiveName = "Lab Tablet 3",
            Description = "training station",
            PhysicalLicense = "PL-123",
            HardwareCondition = (HardwareCondition)1,
            IsLockedOut = true,
        };

        [Fact]
        public void ToHardwareClaim_CopiesSharedFieldsFromLocalHardware()
        {
            LocalHardware local = Sample();
            Hardware projected = HardwareKeyAuthorization.ToHardwareClaim(local);

            projected.Should().NotBeNull();
            projected.Id.Should().Be(local.Id);
            projected.UUID.Should().Be(local.UUID);
            projected.TimeStamp.Should().Be(local.TimeStamp);
            projected.LastUpdateTimeStamp.Should().Be(local.LastUpdateTimeStamp);
            projected.HardwareTypeUUID.Should().Be(local.HardwareTypeUUID);
            projected.DescriptiveName.Should().Be(local.DescriptiveName);
            projected.Description.Should().Be(local.Description);
            projected.HardwareCondition.Should().Be(local.HardwareCondition);
            projected.IsLockedOut.Should().Be(local.IsLockedOut);

            // PhysicalLicense is DELIBERATELY NOT copied (2026-08-10). This assertion used to read
            // `projected.PhysicalLicense.Should().Be(local.PhysicalLicense)`, because the projection
            // copied every shared field and the credential happened to be one of them -- it was
            // never a requirement that the credential travel.
            //
            // PhysicalLicense is the device AUTHENTICATION CREDENTIAL and a JWT is base64, not
            // encrypted, so copying it here put the credential in every access token, in anything
            // that logged one, and on the wire. Nothing reads it back: ExtractHardwareData uses only
            // Id before re-reading the live row, and the claim's other consumer hands it to the API
            // controllers, where PhysicalLicense appears nowhere at all.
            projected.PhysicalLicense.Should().BeNullOrEmpty(
                "the device credential must never be carried inside a token");
        }

        [Fact]
        public void ToHardwareClaim_RoundTripsAsHardwareThroughJson()
        {
            // The token carries the claim as JSON and the middleware deserializes it as
            // Hardware. Pin that the projected Hardware survives that round-trip with
            // its Id (the field every consumer uses) intact -- no reliance on lenient
            // LocalHardware-as-Hardware deserialization.
            LocalHardware local = Sample();
            Hardware projected = HardwareKeyAuthorization.ToHardwareClaim(local);

            string json = JsonSerializer.Serialize(projected);
            Hardware roundTripped = JsonSerializer.Deserialize<Hardware>(json);

            roundTripped.Id.Should().Be(local.Id);
            roundTripped.UUID.Should().Be(local.UUID);
            roundTripped.DescriptiveName.Should().Be(local.DescriptiveName);
            roundTripped.IsLockedOut.Should().Be(local.IsLockedOut);
        }

        [Fact]
        public void ToHardwareClaim_Null_ReturnsNull()
        {
            HardwareKeyAuthorization.ToHardwareClaim(null).Should().BeNull();
        }
    }
}
