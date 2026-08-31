// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Generic;
using System.Linq;
using Febris.UserNode.DataAccessLayer.Migrations.DataDb;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Febris.UserNode.LogicLayer.Tests
{
    /// <summary>
    /// Audit C-04 -- the parent-link table every layer above it assumed existed.
    ///
    /// <para>
    /// <c>DataDbContext:350</c> declares the DbSet and the DAL, BLL, controller and views all ship,
    /// but no migration ever created the table and the snapshot did not describe it either, so
    /// every parent-link operation failed with <c>relation "ParentLinkedStudent" does not exist</c>.
    /// Parent access is FERPA-relevant.
    /// </para>
    ///
    /// <para>
    /// Asserted against the migration's own operations rather than a live database: the migration
    /// IS the artifact that was missing, and five earlier migrations reached the wrong conclusion
    /// about this exact table by reasoning about it instead of checking. The InMemory provider
    /// cannot see relational schema at all, so it could not have caught this either.
    /// </para>
    /// </summary>
    public class ParentLinkedStudentSchemaTests
    {
        [Fact]
        public void Migration_CreatesTheTable_MatchingTheEntityExactly()
        {
            List<MigrationOperation> up = new ParentLinkedStudentTable().UpOperations.ToList();

            CreateTableOperation create = up.OfType<CreateTableOperation>().Single();
            create.Name.Should().Be("ParentLinkedStudent");
            create.Columns.Select(c => c.Name).Should().BeEquivalentTo(new[]
            {
                "Id", "UUID", "TimeStamp", "LastUpdateTimeStamp",
                "ParentUserId", "StudentUserId", "StudentActorId"
            });
        }

        [Fact]
        public void Migration_EmitsNoColumnDefaults_BecauseTheModelDeclaresNone()
        {
            // Unlike the ~121 entities configured in OnModelCreating, ParentLinkedStudent has no
            // configuration block, so it gets no uuid_generate_v4() / CURRENT_TIMESTAMP defaults.
            // Emitting them here would recreate exactly the model-versus-schema disagreement that
            // C-01 was -- a column the model believes the store generates and the store does not.
            // ParentLinkLogic assigns UUID, TimeStamp and LastUpdateTimeStamp itself on insert.
            new ParentLinkedStudentTable().UpOperations
                .OfType<CreateTableOperation>()
                .Single()
                .Columns.Should().OnlyContain(c => c.DefaultValueSql == null);
        }

        [Fact]
        public void Migration_IndexesTheColumnsTheQueriesActuallyFilterOn()
        {
            // ParentLinkedStudentQueries filters on ParentUserId (:60, :84, :105) and
            // StudentActorId (:60, :125). StudentUserId is stored but never used as a predicate,
            // so it deliberately gets no index.
            new ParentLinkedStudentTable().UpOperations
                .OfType<CreateIndexOperation>()
                .Select(i => i.Columns.Single())
                .Should().BeEquivalentTo(new[] { "ParentUserId", "StudentActorId" });
        }
    }
}
