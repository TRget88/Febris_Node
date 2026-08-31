// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    public interface ICurriculumQueries
    {
        /// <summary>Every non-obsolete curriculum, classification included. Never null.</summary>
        Task<List<Curriculum>> Get();

        /// <summary>
        /// Every curriculum INCLUDING obsoleted ones. Without this an obsoleted curriculum is
        /// invisible to the UI and therefore impossible to restore -- soft-delete would behave
        /// like a hard delete from the operator's point of view.
        /// </summary>
        Task<List<Curriculum>> GetIncludingObsolete();

        /// <summary>One curriculum by Id, classification included. Null on miss.</summary>
        Task<Curriculum> Get(long? input);

        /// <summary>One curriculum by UUID, classification included. Null on miss.</summary>
        Task<Curriculum> Get(Guid? input);

        /// <summary>Create-or-update by UUID. Returns the persisted row.</summary>
        Task<Curriculum> Upsert(Curriculum input);

        /// <summary>
        /// Soft-delete: flips Obsolete. Curricula are referenced by CohortLinkedCurriculum and
        /// ModuleLinkedCurriculum rows, so a hard delete would orphan them.
        /// </summary>
        Task<bool> SetObsolete(long id, bool obsolete);

        /// <summary>The classification lookup for authoring dropdowns. Never null.</summary>
        Task<List<CurriculumClassification>> GetClassifications();
    }

    /// <summary>
    /// Node-local curriculum store.
    ///
    /// <para>
    /// This class was a Remote-HTTP client reading curricula from the hub, gated closed, so a
    /// self-hosted node saw no curricula and had no way to make any. That is fatal for a
    /// standalone node: the hub-side content developer portal is hub-private and never ships, so
    /// if the node cannot author, a self-hoster has no content at all.
    /// </para>
    ///
    /// <para>
    /// The Curriculum and CurriculumClassification tables have existed in the node's own DataDb
    /// since the Initial migration -- they were simply unreachable, with no DbSet and no local
    /// query surface. This makes them first-class node-owned data. Local-EF bodies mirror the
    /// proven ModuleQueries / ModuleLinkedObjectQueries twins.
    /// </para>
    /// </summary>
    public class CurriculumQueries : ICurriculumQueries
    {
        private readonly DataDbContext _dataDbContext;

        // DI refactor
        public CurriculumQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }

        public CurriculumQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        #region Get
        public async Task<List<Curriculum>> Get()
        {
            List<Curriculum> output = new List<Curriculum>();
            try
            {
                output = await _dataDbContext.Curriculum
                    .AsNoTracking()
                    .Include(i => i.CurriculumClassification)
                    .Where(i => !i.Obsolete)
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<List<Curriculum>> GetIncludingObsolete()
        {
            List<Curriculum> output = new List<Curriculum>();
            try
            {
                output = await _dataDbContext.Curriculum
                    .AsNoTracking()
                    .Include(i => i.CurriculumClassification)
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        public async Task<Curriculum> Get(long? input)
        {
            try
            {
                if (input == null)
                {
                    return null;
                }

                return await _dataDbContext.Curriculum
                    .AsNoTracking()
                    .Include(i => i.CurriculumClassification)
                    .Where(i => i.Id == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<Curriculum> Get(Guid? input)
        {
            try
            {
                if (input == null)
                {
                    return null;
                }

                return await _dataDbContext.Curriculum
                    .AsNoTracking()
                    .Include(i => i.CurriculumClassification)
                    .Where(i => i.UUID == input)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<CurriculumClassification>> GetClassifications()
        {
            List<CurriculumClassification> output = new List<CurriculumClassification>();
            try
            {
                output = await _dataDbContext.CurriculumClassification
                    .AsNoTracking()
                    .OrderBy(i => i.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        #endregion

        #region Write
        public async Task<Curriculum> Upsert(Curriculum input)
        {
            try
            {
                if (input == null)
                {
                    return null;
                }

                if (input.UUID == Guid.Empty)
                {
                    input.UUID = Guid.NewGuid();
                }

                Curriculum existing = await _dataDbContext.Curriculum
                    .Where(i => i.UUID == input.UUID)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    // Null the classification NAVIGATION before Add. EF cascades inserts through
                    // populated navigations, so a stub CurriculumClassification carried in from a
                    // model-bound form would silently create a DUPLICATE classification row every
                    // time a curriculum is authored. Enforced here rather than in the controller so
                    // no future caller has to remember it. AssignClassification below then puts the
                    // link back from a TRACKED lookup, which is the safe way to populate it.
                    //
                    // Corrected 2026-08-25: this used to say "the FK (CurriculumClassificationUUID)
                    // is what actually stores the relationship". It does not. That column is an
                    // unconstrained Guid that joins to nothing, and the real link is the shadow FK
                    // CurriculumClassificationId. Believing this comment is why nothing populated it.
                    input.CurriculumClassification = null;
                    _dataDbContext.Curriculum.Add(input);
                    await AssignClassification(input, input.CurriculumClassificationUUID);
                    await _dataDbContext.SaveChangesAsync();
                    return input;
                }

                // EDITABLE FIELDS ONLY. `Obsolete` is deliberately absent, and its absence is the
                // fix rather than an oversight (2026-08-25).
                //
                // Views/Curriculum/Edit.cshtml renders the obsolete switch `disabled`, on purpose,
                // because obsoleting is a distinct action. The disabled CHECKBOX posts nothing --
                // but ASP.NET Core's InputTagHelper emits a COMPANION HIDDEN FIELD for a bool
                // property, and that one is not disabled, so the browser posts
                // Curriculum.Obsolete=false explicitly. input.Obsolete therefore arrived as a
                // deliberate-looking false on every edit, and this line wrote it over the stored
                // value -- silently un-obsoleting a retired curriculum because somebody corrected
                // its description. (Mechanism corrected 2026-08-25: this said the field simply
                // posted nothing and the binder defaulted. The outcome is identical, but the
                // companion hidden field is why "just mark it disabled" does not protect a flag.)
                //
                // Not fixed by round-tripping the flag through a hidden field: that hands the edit
                // form an overposting surface for a flag owned exclusively by the role-gated
                // ObsoleteToggle. This is the third time this exact shape has been found here
                // (Cohort.Archive and Cohort.LockMembers were the first two, audit C-07), and the
                // settled answer is the one CohortLogic.Update already uses -- copy only the
                // editable fields, so flags are preserved by never being touched. SetObsolete
                // remains the sole writer for an existing row.
                existing.Name = input.Name;
                existing.Description = input.Description;
                existing.Version = input.Version;
                existing.CurriculumClassificationUUID = input.CurriculumClassificationUUID;
                await AssignClassification(existing, input.CurriculumClassificationUUID);
                existing.LastUpdateTimeStamp = DateTime.Now;
                await _dataDbContext.SaveChangesAsync();
                return existing;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <summary>
        /// Store the classification link for real (ROADMAP 11, 2026-08-25).
        ///
        /// <para>
        /// <c>Curriculum</c> declares a <c>CurriculumClassification</c> navigation and a plain
        /// <c>Guid? CurriculumClassificationUUID</c>, and NEITHER of those is the relationship. EF
        /// created a SHADOW foreign key, <c>CurriculumClassificationId</c>, and that column plus its
        /// index and real database FK are what the Initial migration shipped. The UUID column has no
        /// constraint on it at all and joins to nothing. So the write path faithfully recorded the
        /// operator's choice in a column no read can follow, and
        /// <c>Get(...).Include(i =&gt; i.CurriculumClassification)</c> came back null every time.
        /// This is the same shape as <c>CohortMember.CohortUUID</c> versus its shadow
        /// <c>CohortId</c>, which the live-PostgreSQL tests caught.
        /// </para>
        /// <para>
        /// Assigning the navigation from a TRACKED lookup is what populates the shadow FK. It must
        /// be tracked: an <c>AsNoTracking</c> instance attached to an added graph makes EF try to
        /// insert the principal again, which is a duplicate row on the update path and a primary-key
        /// collision on the insert path. That is exactly why the <c>Add</c> branch above nulls a
        /// form-supplied navigation first, and that guard stays.
        /// </para>
        /// <para>
        /// No migration: the column, its index and the FK have existed since the Initial migration.
        /// The only thing missing was anything writing to it.
        /// </para>
        /// <para>
        /// CURRENTLY UNREACHABLE, and fixed anyway. Nothing in <c>enduser/</c> can create a
        /// <c>CurriculumClassification</c>, so the table is empty on a real node and the picker only
        /// offers "[None]". Whether a node should be able to author classifications is an open owner
        /// question (docs/BUGS.md). Leaving the write side broken until then would mean the link
        /// silently fails on the first day it becomes reachable, which is the defect family this
        /// audit exists to remove.
        /// </para>
        /// </summary>
        private async Task AssignClassification(Curriculum target, Guid? classificationUuid)
        {
            if (target == null)
            {
                return;
            }

            if (classificationUuid == null || classificationUuid == Guid.Empty)
            {
                // Explicit clear. "[None]" has to be able to REMOVE an existing link, or the
                // picker becomes a one-way door the way ROADMAP 11 records for obsolete curricula.
                target.CurriculumClassification = null;
                return;
            }

            // Tracked deliberately -- see the remarks above.
            CurriculumClassification classification = await _dataDbContext.CurriculumClassification
                .FirstOrDefaultAsync(i => i.UUID == classificationUuid);

            // A UUID naming no row leaves the link alone rather than throwing. The UUID column is
            // unconstrained, so a stale or hand-edited value is possible, and refusing the whole
            // save over it would lose the operator's other edits.
            if (classification != null)
            {
                target.CurriculumClassification = classification;
            }
        }

        public async Task<bool> SetObsolete(long id, bool obsolete)
        {
            try
            {
                Curriculum existing = await _dataDbContext.Curriculum
                    .Where(i => i.Id == id)
                    .FirstOrDefaultAsync();

                if (existing == null)
                {
                    return false;
                }

                existing.Obsolete = obsolete;
                existing.LastUpdateTimeStamp = DateTime.Now;
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        #endregion
    }
}
