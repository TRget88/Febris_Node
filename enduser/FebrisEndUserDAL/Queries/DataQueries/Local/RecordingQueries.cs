// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.DataContext;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.DataAccessLayer.Queries.DataQueries
{
    // DAL for session-video ownership. Mirrors the ParentLinkedStudentQueries shape (DataDbContext,
    // DI plus a parameterless ops-based ctor). UUID and TimeStamp are set by the BLL before Create
    // is called, the same convention the other Create methods follow.
    public interface IRecordingQueries
    {
        Task<Recording> Create(Recording input);
        Task<Recording> GetByName(string name);
        Task<List<Recording>> GetByActor(Guid actorUuid);

        /// <summary>
        /// Recordings MINTED BY a given device, newest first, capped.
        ///
        /// <para>
        /// Backs the device-activity panel, which is an OPERATIONS view: a headset attributing to
        /// the wrong roster, a device that has stopped recording, a support call about a missing
        /// video, a device being retired. <c>MayAcceptPart</c> already reads the hardware to gate
        /// uploads, so this column was never write-only. What was missing was a way to see it
        /// without a SQL prompt.
        /// </para>
        ///
        /// <para>
        /// A recording carries the LEARNER (from the launch context) and the DEVICE (proven by the
        /// token). That split is the shared-kiosk design, so several learners per device is normal.
        /// See the ownership ruling in <c>docs/BUGS.md</c>.
        /// </para>
        /// </summary>
        Task<List<Recording>> GetByHardware(Guid hardwareUuid, int limit);

        /// <summary>Total recordings minted by a device, so a capped list can say "showing N of M".</summary>
        Task<int> CountByHardware(Guid hardwareUuid);
        /// <summary>Recordings created before <paramref name="cutoffUtc"/>, for retention.</summary>
        Task<List<Recording>> GetOlderThan(DateTime cutoffUtc);
        Task<bool> Delete(long id);
    }

    public class RecordingQueries : IRecordingQueries
    {
        private readonly DataDbContext _dataDbContext;

        public RecordingQueries(DataDbContext dataDbContext)
        {
            _dataDbContext = dataDbContext;
        }
        public RecordingQueries()
        {
            _dataDbContext = new DataDbContext(DataDbContext.ops.DbOptions);
        }

        public async Task<Recording> Create(Recording input)
        {
            try
            {
                await _dataDbContext.Recording.AddAsync(input);
                await _dataDbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }

        /// <summary>
        /// Resolves the owner of a recording by its on-disk filename stem.
        /// <para>
        /// Returns null when there is no row, and the caller MUST treat that as "deny", not as
        /// "unowned, therefore allowed". Recordings created before this table existed have no row,
        /// so a fallback to allow would leave every historical recording exactly as exposed as it
        /// was before.
        /// </para>
        /// </summary>
        public async Task<Recording> GetByName(string name)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    return null;
                }
                return await _dataDbContext.Recording
                    .AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Name == name);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<Recording>> GetOlderThan(DateTime cutoffUtc)
        {
            try
            {
                // Tracked, not AsNoTracking: the caller deletes these.
                return await _dataDbContext.Recording
                    .Where(i => i.TimeStamp < cutoffUtc)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<bool> Delete(long id)
        {
            try
            {
                Recording existing = await _dataDbContext.Recording.FirstOrDefaultAsync(i => i.Id == id);
                if (existing == null)
                {
                    return false;
                }
                _dataDbContext.Recording.Remove(existing);
                await _dataDbContext.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        public async Task<List<Recording>> GetByActor(Guid actorUuid)
        {
            try
            {
                return await _dataDbContext.Recording
                    .AsNoTracking()
                    .Where(i => i.ActorUUID == actorUuid)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<List<Recording>> GetByHardware(Guid hardwareUuid, int limit)
        {
            try
            {
                // Guid.Empty is what an unset device looks like, and Register writes it when the
                // launch had no hardware. Matching it would gather every unattributed recording and
                // present it as one device's activity, which reads as real evidence and is not.
                if (hardwareUuid == Guid.Empty)
                {
                    return new List<Recording>();
                }

                return await _dataDbContext.Recording
                    .AsNoTracking()
                    .Where(i => i.HardwareUUID == hardwareUuid)
                    .OrderByDescending(i => i.TimeStamp)
                    .Take(limit)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<int> CountByHardware(Guid hardwareUuid)
        {
            try
            {
                if (hardwareUuid == Guid.Empty)
                {
                    return 0;
                }

                return await _dataDbContext.Recording
                    .AsNoTracking()
                    .Where(i => i.HardwareUUID == hardwareUuid)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
    }
}
