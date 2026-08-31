// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.IO;
using System.Text;
using Febris.UserNode.Portal.IdentityPolicy;
using FluentAssertions;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Verifies the hand-rolled CSV user-import parser (ICsvUserImporter) that feeds the admin bulk-create
    /// backend from an uploaded file (the counterpart to the Excel copy-paste flow).
    /// </summary>
    public class CsvUserImporterTests
    {
        private static Stream Csv(string text) => new MemoryStream(Encoding.UTF8.GetBytes(text));

        [Fact]
        public void Parses_valid_rows_into_the_bulk_submission_list()
        {
            string csv =
                "FirstName,LastName,EmailAddress,PhoneNumber,IdentificationNumber\n" +
                "Ada,Lovelace,ada@acme.com,555-1000,ID1\n" +
                "Alan,Turing,alan@acme.com,555-1001,ID2\n";

            var (model, errors) = new CsvUserImporter().Parse(Csv(csv));

            model.SubmissionList.Should().HaveCount(2);
            model.SubmissionList[0].FirstName.Should().Be("Ada");
            model.SubmissionList[0].EmailAddress.Should().Be("ada@acme.com");
            model.SubmissionList[1].LastName.Should().Be("Turing");
            errors.Should().BeEmpty();
        }

        [Fact]
        public void Skips_a_row_with_no_email_and_records_an_error_instead_of_throwing()
        {
            string csv =
                "FirstName,LastName,EmailAddress\n" +
                "Grace,Hopper,grace@acme.com\n" +
                "NoEmail,Person,\n";

            var (model, errors) = new CsvUserImporter().Parse(Csv(csv));

            model.SubmissionList.Should().HaveCount(1);
            model.SubmissionList[0].EmailAddress.Should().Be("grace@acme.com");
            errors.Should().NotBeEmpty();
        }

        [Fact]
        public void Handles_quoted_fields_containing_commas()
        {
            string csv =
                "FirstName,LastName,EmailAddress\n" +
                "\"Van, der\",Berg,vdb@acme.com\n";

            var (model, errors) = new CsvUserImporter().Parse(Csv(csv));

            model.SubmissionList.Should().HaveCount(1);
            model.SubmissionList[0].FirstName.Should().Be("Van, der");
        }

        [Fact]
        public void Empty_input_yields_no_rows_and_an_error_not_an_exception()
        {
            var (model, errors) = new CsvUserImporter().Parse(Csv(string.Empty));

            (model?.SubmissionList == null || model.SubmissionList.Count == 0).Should().BeTrue();
            errors.Should().NotBeEmpty();
        }
    }
}
