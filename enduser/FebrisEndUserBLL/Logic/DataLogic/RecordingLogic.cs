// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.PrimaryLogicLayer.Logic.XApiLogic;
using Febris.SharedServices;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IRecordingLogic
    {
        Task<Recording> Register(string name, Guid actorUuid, Guid hardwareUuid);
        Task<bool> MayView(string name);
        Task<bool> MayAcceptPart(string partFileName, Guid hardwareUuid);

        /// <summary>
        /// What a device has MINTED, for the device detail screen. The read side of the ownership
        /// ruling of 2026-08-18: the actor on a recording is claimed by the caller and the hardware
        /// is proven, and until now nobody could see the second one without database access.
        /// </summary>
        Task<Febris.ModelLibrary.ViewModels.DeviceRecordingSummaryViewModel> GetRecordingsByDevice(
            Guid hardwareUuid, int limit);
    }

    /// <summary>
    /// Owns the entitlement decision for session video: you may watch a recording if it belongs to
    /// an actor you are entitled to, or you are staff.
    ///
    /// <para>
    /// Before this existed the Portal's two video loaders served any recording to any signed-in end
    /// user who knew its Guid, because nothing recorded which learner a recording belonged to. The
    /// Guid being unguessable was the only protection, which is secrecy of the identifier, not
    /// access control.
    /// </para>
    ///
    /// <para>
    /// The decision is delegated to <see cref="XApiAccessScope.ResolveAsync"/> rather than
    /// open-coded, because that is the node's single existing entitlement resolver and it already
    /// encodes exactly this rule: Educator, Admin and IT Admin are unrestricted; a parent gets the
    /// actors of their linked students, read LIVE so a newly added link takes effect without
    /// re-authenticating; a learner gets their own actor; anything else is denied. Open-coding it
    /// here would have been a second, drifting copy of the same policy.
    /// </para>
    /// </summary>
    public class RecordingLogic : IRecordingLogic
    {
        private readonly IRecordingQueries _context;
        private readonly IParentLinkedStudentQueries _parentLinks;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        public RecordingLogic(
            IHttpContextAccessor httpContextAccessor,
            IRecordingQueries context,
            IParentLinkedStudentQueries parentLinks)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
            _parentLinks = parentLinks;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        /// <summary>
        /// Records who a freshly minted recording name belongs to. Called when the name is created
        /// for the xAPI attachment, which is the only moment the owning actor is known: the upload
        /// itself carries a device token and nothing identifying the learner.
        /// <para>
        /// Failure is logged and swallowed. This runs inside statement initialisation, and a
        /// failure to record ownership must not stop a learner starting their module. The cost of
        /// that choice is a recording nobody can view, which is the safe direction.
        /// </para>
        /// </summary>
        public async Task<Recording> Register(string name, Guid actorUuid, Guid hardwareUuid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(name) || actorUuid == Guid.Empty)
                {
                    FebrisLog.Warn(
                        "RecordingLogic.Register called without a usable name or actor, so this recording will have no owner and will not be viewable.");
                    return null;
                }
                if (hardwareUuid == Guid.Empty)
                {
                    // Not fatal to the launch, but the upload gate will refuse every part for this
                    // recording, so it is worth saying loudly rather than discovering it as a
                    // silently failing upload five minutes later.
                    FebrisLog.Warn(
                        "RecordingLogic.Register called without a device for '" + name +
                        "', so no upload will be accepted for it.");
                }

                Recording input = new Recording
                {
                    Name = name,
                    ActorUUID = actorUuid,
                    HardwareUUID = hardwareUuid,
                    UUID = Guid.NewGuid(),
                    TimeStamp = DateTime.UtcNow,
                    LastUpdateTimeStamp = DateTime.UtcNow
                };

                return await _context.Create(input);
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "RecordingLogic.Register: could not record video ownership");
                return null;
            }
        }

        /// <summary>Default page size for <see cref="GetRecordingsByDevice"/>.</summary>
        public const int DefaultRecordingPageSize = 25;

        /// <inheritdoc />
        public async Task<Febris.ModelLibrary.ViewModels.DeviceRecordingSummaryViewModel> GetRecordingsByDevice(
            Guid hardwareUuid, int limit)
        {
            Febris.ModelLibrary.ViewModels.DeviceRecordingSummaryViewModel output =
                new Febris.ModelLibrary.ViewModels.DeviceRecordingSummaryViewModel
                {
                    HardwareUUID = hardwareUuid,
                    Limit = limit <= 0 ? DefaultRecordingPageSize : limit
                };

            try
            {
                if (hardwareUuid == Guid.Empty)
                {
                    return output;
                }

                output.Recordings = await _context.GetByHardware(hardwareUuid, output.Limit);
                output.TotalCount = await _context.CountByHardware(hardwareUuid);

                // Counted over the returned page and labelled as such at the point of display.
                output.DistinctActorCount = output.Recordings
                    .Select(r => r.ActorUUID)
                    .Distinct()
                    .Count();
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "RecordingLogic.GetRecordingsByDevice");
                throw;
            }

            return output;
        }

        /// <summary>
        /// Reduces a caller-supplied video name to the form <see cref="Register"/> stores.
        /// <para>
        /// Register stores the BARE minted Guid. <c>WidgetController.VideoLoader</c> appends
        /// <c>".mp4"</c> to any name that arrives without an extension, and it does so BEFORE
        /// calling the gate, so the gate is asked about <c>{guid}.mp4</c> while the row says
        /// <c>{guid}</c>. <c>GetByName</c> is an exact match, so every lookup missed and the
        /// deny-on-miss branch denied EVERY recording to EVERYONE, staff included, because the null
        /// check runs before the unrestricted branch.
        /// </para>
        /// <para>
        /// Normalising here rather than at the call site keeps the gate correct for any caller, and
        /// a minted recording name is a Guid, which contains no dot, so stripping the extension
        /// cannot truncate a legitimate name.
        /// </para>
        /// </summary>
        private static string NormaliseName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;
            int lastDot = name.LastIndexOf('.');
            return lastDot > 0 ? name.Substring(0, lastDot) : name;
        }

        /// <summary>
        /// The UPLOAD gate: may this device send a part for this recording?
        ///
        /// <para>
        /// Before this, <c>VideoUploadLogic</c> accepted any part filename from any authenticated
        /// device. <c>SplitVideos/</c> and <c>recordings/</c> are one flat namespace shared by every
        /// device and learner, so one device could overwrite another device's parts, or a finished
        /// recording belonging to a different learner, simply by naming its upload accordingly. The
        /// authenticated device was materialised on every request and then discarded unread.
        /// </para>
        ///
        /// <para>
        /// Two conditions, both required. The recording must be one this node MINTED, which alone
        /// removes the ability to write arbitrary filenames into the video directories. And it must
        /// have been minted BY THIS DEVICE, which is what stops one device from overwriting
        /// another's. Safe to compare directly because mint time and upload time resolve to the
        /// same <c>LocalHardware</c> UUID.
        /// </para>
        ///
        /// <para>
        /// Rejecting is not free: both producers retry the whole recording on the next poll (5 min
        /// on PC, 10 on mobile) and never give up, so an orphaned recording retries indefinitely.
        /// That is accepted deliberately -- the alternative is accepting writes we cannot attribute
        /// -- but it is why the refusal is logged with the name.
        /// </para>
        /// </summary>
        /// <param name="partFileName">The client's part filename, <c>{base}.part_{index}.{count}</c>.</param>
        public async Task<bool> MayAcceptPart(string partFileName, Guid hardwareUuid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(partFileName) || hardwareUuid == Guid.Empty)
                {
                    return false;
                }

                // Recover the recording name from the part name. MergeFile parses the same token,
                // so a name without it is not a part this pipeline can assemble anyway.
                const string partToken = ".part_";
                int tokenAt = partFileName.IndexOf(partToken, StringComparison.Ordinal);
                if (tokenAt <= 0)
                {
                    FebrisLog.Warn("Video part refused: '" + partFileName + "' is not a part filename.");
                    return false;
                }

                string recordingName = NormaliseName(partFileName.Substring(0, tokenAt));
                Recording recording = await _context.GetByName(recordingName);
                if (recording == null)
                {
                    FebrisLog.Warn("Video part refused: no recording '" + recordingName + "' was minted by this node.");
                    return false;
                }
                if (recording.HardwareUUID != hardwareUuid)
                {
                    FebrisLog.Warn("Video part refused: recording '" + recordingName +
                        "' was not minted by the uploading device.");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "RecordingLogic.MayAcceptPart: upload gate failed");
                return false;
            }
        }

        /// <summary>
        /// The gate. Returns false for anything it cannot positively justify.
        /// </summary>
        public async Task<bool> MayView(string name)
        {
            try
            {
                Recording recording = await _context.GetByName(NormaliseName(name));
                if (recording == null)
                {
                    // DENY, deliberately. An unowned recording is not a public one. Recordings that
                    // predate this table have no row, so allowing on a miss would leave every
                    // historical recording exactly as exposed as it was before, which is the whole
                    // defect. The visible cost is that a learner cannot open a recording made
                    // before this shipped; staff are unrestricted and can still reach them.
                    return false;
                }

                ActorAccessScope scope = await XApiAccessScope.ResolveAsync(User, _parentLinks);
                if (scope.Unrestricted)
                {
                    return true;
                }

                return scope.AllowedActorUuids.Contains(recording.ActorUUID);
            }
            catch (Exception ex)
            {
                FebrisLog.Error(ex, "RecordingLogic.MayView: entitlement check failed");
                // A check that threw has not authorised anything.
                return false;
            }
        }
    }
}
