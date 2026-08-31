using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Febris.UserNode.DataAccessLayer.Migrations.DataDb
{
    /// <inheritdoc />
    public partial class HashDeviceCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Audit T9. PhysicalLicense is the device AUTHENTICATION CREDENTIAL and was stored in
            // cleartext. Convert every existing row to the hash the code now compares against.
            //
            // ALREADY-PROVISIONED DEVICES KEEP WORKING. The device goes on sending the same string
            // it always did; only the STORED form changes, and GetByKey now hashes the incoming
            // value before comparing. This is a storage change, not a protocol change.
            //
            // The expression must agree byte for byte with DeviceCredential.Hash -- lowercase hex of
            // the UTF-8 bytes. If it did not, every device on the node would silently stop
            // authenticating with nothing in the logs to explain it. Both sides are pinned by
            // DeviceCredentialTests.HashMatchesThePostgresExpressionUsedByTheMigration against the
            // published SHA-256 of "test", and this exact SQL was run against the node's Postgres 16
            // and returned that same value.
            //
            // sha256() is built in from PostgreSQL 11; no pgcrypto extension is required.
            //
            // IDEMPOTENT. The regex guard skips anything already 64 lowercase hex characters, so
            // re-running this cannot double-hash and lock every device out. Rows with NULL or an
            // empty credential are left alone -- an unregistered device must NOT be handed the hash
            // of an empty string, which would be one shared value they could all authenticate with.
            migrationBuilder.Sql(@"
                UPDATE ""Hardware""
                SET ""PhysicalLicense"" = encode(sha256(convert_to(""PhysicalLicense"", 'UTF8')), 'hex')
                WHERE ""PhysicalLicense"" IS NOT NULL
                  AND ""PhysicalLicense"" <> ''
                  AND ""PhysicalLicense"" !~ '^[0-9a-f]{64}$';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A hash cannot be reversed, so there is no honest Down. Doing nothing would be WORSE
            // than failing: the schema would roll back to code that compares cleartext against
            // stored hashes, and every device would stop authenticating with no explanation.
            //
            // Recovery from here is to regenerate each device credential (Hardware list ->
            // Regenerate Credential) and enter the new value on the device.
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    RAISE EXCEPTION 'HashDeviceCredentials cannot be rolled back: device credentials were hashed and hashes are one-way. Regenerate each device credential instead.';
                END $$;
            ");
        }
    }
}
