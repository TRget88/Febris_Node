// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.SharedServices;
using Febris.ModelLibrary.Interfaces.DataModelInterfaces;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using X.PagedList;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    /// <summary>
    /// The node's hardware BLL.
    ///
    /// <para>
    /// RETYPED to <see cref="LocalHardware"/> throughout. Every member previously returned the
    /// CENTRAL <c>Hardware</c> aggregate, which meant the node read its own device rows out of the
    /// DAL and then hand-copied them field by field into a type it does not own, purely so the
    /// views could bind to it. That projection is gone. It cost four near-identical mapping blocks,
    /// it silently dropped any field nobody remembered to copy, and it is the same leak that pulled
    /// the central entity into the node's EF model and produced the orphan Hardware1 table.
    /// </para>
    ///
    /// <para>
    /// Hardware kind comes from <see cref="HardwareKind"/> on the row itself. The node has no
    /// HardwareType store at all: that vocabulary belongs to the hub, and the node never needed a
    /// copy of it to decide anything.
    /// </para>
    /// </summary>
    public interface IHardwareLogic
    {
        Task<List<LocalHardware>> Get();
        /// <summary>
        /// Registers a device and MINTS its authentication credential. The credential is
        /// returned in plaintext exactly once, for display to the operator; only its hash is
        /// stored, so it can never be read back afterwards.
        /// </summary>
        Task<(LocalHardware Hardware, string Credential)> Create(LocalHardwareCreationViewModel input);

        /// <summary>
        /// Issues a NEW credential for an existing device, invalidating the old one. This is the
        /// only way to recover from a lost credential, because the stored hash cannot be
        /// reversed. Returns the new credential in plaintext once, or null if no such device.
        /// </summary>
        Task<string> RegenerateCredential(long id);
        Task<LocalHardware> Update(LocalHardwareCreationViewModel input);
        Task<LocalHardware> Get(long? id);
        Task<LocalHardwareCreationViewModel> CreationPreperation();
        Task<GenericMixedChart> GetNewHardwareMixedChart();
    }

    public class HardwareLogic: IHardwareLogic
    {
        //private IProfessionalQueries _professionalQueries = new Febris.UserNode.DataAccessLayer.Interfaces.DataInterfaces.IProfessionalQueries();
        //private IHardwareQueries _hardwareQueries = new SharedDataAccessLayer.Queries.DataQueries.HardwareQueries();
        //private HardwareQueries _context = new SharedDataAccessLayer.Queries.DataQueries.HardwareQueries();        
        
        //private readonly IHardwareLinkedModuleQueries _dataLinkedModuleContext;
        //private readonly IHardwareLinkedCurriculumQueries _dataLinkedCurriculumContext;
        //private readonly ILocationLinkedHardwareQueries _dataLinkedLocationContext;
        //private readonly ICurriculumQueries _dataCurriculumContext;
        //private readonly ILocationQueries _dataLocationContext;
        private readonly IHardwareQueries _context;
        // A-02 Stage 2. Null on the legacy self-newing ctor, hence the null-guard at the use site:
        // that path keeps the pre-existing behaviour rather than throwing.
        private readonly IHardwareRevocationList _revocations;
        // Null on the legacy self-newing ctor, same as _revocations. Only read to size the
        // revocation window, which falls back to the shipped default when it is absent.
        private readonly IConfiguration _config;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;

        // DI refactor
        public HardwareLogic(IHttpContextAccessor httpContextAccessor, IHardwareQueries context, IHardwareRevocationList revocations, IConfiguration config)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
            _revocations = revocations;
            _config = config;
        }

        public HardwareLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new HardwareQueries();

        }


        public async Task<List<LocalHardware>> Get()
        {
            List<LocalHardware> output = new List<LocalHardware>();
            try
            {
                output = await _context.Get();
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<LocalHardware> Get(long? id)
        {
            LocalHardware output = null;
            try
            {
                output = await _context.Get(id);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public Task<LocalHardwareCreationViewModel> CreationPreperation()
        {
            // No lookup round-trip and no SelectList. The kind dropdown is rendered straight off
            // the enum by the view, so registration no longer depends on the HardwareType table
            // having been seeded. Previously a miss here produced an empty select list and an
            // operator could register a device against nothing.
            LocalHardwareCreationViewModel output = new LocalHardwareCreationViewModel()
            {
                Hardware = new LocalHardware()
            };
            return Task.FromResult(output);
        }

        /// <summary>
        /// The <c>[Display(Name = ...)]</c> text for a <see cref="HardwareKind"/> member, falling
        /// back to the member name. Two of the three kinds contain a space and so cannot be CLR
        /// identifiers, which is why the attribute exists and why reading it matters here.
        /// </summary>
        private static string DisplayNameOf(HardwareKind kind)
        {
            System.Reflection.MemberInfo member = typeof(HardwareKind).GetMember(kind.ToString()).FirstOrDefault();
            if (member == null)
            {
                return kind.ToString();
            }

            System.ComponentModel.DataAnnotations.DisplayAttribute display =
                (System.ComponentModel.DataAnnotations.DisplayAttribute)Attribute.GetCustomAttribute(
                    member, typeof(System.ComponentModel.DataAnnotations.DisplayAttribute));

            return display == null || string.IsNullOrWhiteSpace(display.Name) ? kind.ToString() : display.Name;
        }

        /// <summary>
        /// Registers a device and MINTS its credential (audit T9).
        ///
        /// <para>
        /// The credential is no longer whatever free text an admin typed. Anything supplied on the
        /// view model is IGNORED: the node generates 256 bits from a CSPRNG, stores only the hash,
        /// and hands the plaintext back once for the operator to copy into the device. There is no
        /// second chance -- RegenerateCredential is the only recovery.
        /// </para>
        /// </summary>
        public async Task<(LocalHardware Hardware, string Credential)> Create(LocalHardwareCreationViewModel input)
        {
            LocalHardware output = null;
            string credential = Febris.SharedServices.DeviceCredential.Generate();
            try
            {
                LocalHardware hardware = new LocalHardware()
                {
                    HardwareKind = input.Hardware.HardwareKind,
                    Description = input.Hardware.Description,
                    DescriptiveName = input.Hardware.DescriptiveName,
                    // Only ever the hash reaches the database.
                    PhysicalLicense = Febris.SharedServices.DeviceCredential.Hash(credential),
                    HardwareCondition = input.Hardware.HardwareCondition,
                    IsLockedOut = input.Hardware.IsLockedOut
                };

                // Previously this returned input.Hardware, the caller's own unsaved instance, so
                // the store-generated Id and UUID were never handed back. Return what was persisted.
                output = await _context.Create(hardware);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            // The plaintext travels back on the RETURN VALUE, never on the entity, so there is no
            // tracked instance carrying it that a later SaveChanges could write over the hash.
            return (output, credential);
        }

        /// <summary>
        /// Issues a NEW credential for an existing device and invalidates the old one, INCLUDING
        /// any session the old credential already established.
        ///
        /// <para>
        /// This exists because the stored hash cannot be reversed: once a credential is minted and
        /// the page is gone, nobody -- including an administrator -- can read it back. Regenerating
        /// is the recovery path, and it deliberately breaks the device until the new credential is
        /// entered on it.
        /// </para>
        ///
        /// <para>
        /// THE DOCSTRING HERE USED TO STOP AT THAT SENTENCE AND IT WAS WRONG IN THE WORST DIRECTION.
        /// Rewriting the hash only breaks a device that has to authenticate AGAIN. A thief who had
        /// already authenticated held a refresh token that rotated on every call, and the refresh
        /// path re-read this device's row but tested only <c>IsLockedOut</c>, never the credential.
        /// So this method broke the honest device, which must now be reconfigured, and did nothing
        /// whatever to the attacker. It was the documented incident response, and following it left
        /// the intruder connected.
        /// </para>
        ///
        /// <para>
        /// Closed in two layers, matching how A-02 Stage 2 handles locking. The DURABLE half is
        /// <see cref="LocalHardware.CredentialRegeneratedAt"/>: refresh refuses any token minted
        /// before that moment, it survives a cache outage, and it needs no TTL guess. The IMMEDIATE
        /// half is the revocation list, which stops the access token the thief is holding right now
        /// rather than waiting for it to expire. Neither alone is sufficient: the list fails open by
        /// design and self-evicts, and the timestamp cannot reach a token already issued.
        /// </para>
        /// </summary>
        public async Task<string> RegenerateCredential(long id)
        {
            #region Filter
            if (!User.IsLocalFebrisAdmin() && !User.IsLocalAdmin() && !User.IsLocalEducator())
            {
                return null;
            }
            #endregion

            try
            {
                LocalHardware hardware = await _context.Get(id);
                if (hardware == null)
                {
                    return null;
                }

                string credential = Febris.SharedServices.DeviceCredential.Generate();
                hardware.PhysicalLicense = Febris.SharedServices.DeviceCredential.Hash(credential);
                hardware.LastUpdateTimeStamp = DateTime.Now;

                // UTC on purpose, and NOT LastUpdateTimeStamp. The comparison partner is
                // RefreshHardwareToken.Created, which is DateTime.UtcNow, so a local timestamp here
                // would be wrong by the host's offset in whichever direction the machine happens to
                // sit. LastUpdateTimeStamp is also re-stamped by every unrelated device edit, so
                // reusing it would sign a device out because someone corrected its description.
                hardware.CredentialRegeneratedAt = DateTime.UtcNow;

                await _context.Update(hardware);

                // Stop the token the thief is holding RIGHT NOW. CredentialRegeneratedAt above
                // already refuses the next refresh, but an access token in flight never touches the
                // refresh path, so without this the old session keeps working until it expires.
                // Same call and same TTL as the locking transition in Update: the entry only has to
                // outlive the access token it revokes, because after that the refresh check is what
                // keeps the device out, permanently and without needing the cache.
                //
                // Null-guarded for the legacy self-newing constructor, as the lock path is.
                if (_revocations != null)
                {
                    // Read the CONFIGURED lifetime rather than repeating the literal 15 minutes the
                    // lock path uses. If an operator lengthens the access token, a hardcoded window
                    // would stop covering it and the revocation would lapse early.
                    TimeSpan window = _config != null
                        ? Febris.SharedServices.JwtLifetimeSettings.AccessTokenLifetime(_config)
                        : Febris.SharedServices.JwtLifetimeSettings.DefaultAccessTokenLifetime;

                    await _revocations.RevokeAsync(hardware.UUID, window);
                }

                Febris.SharedServices.FebrisLog.Warn(
                    "Device credential REGENERATED for hardware id " + id +
                    ". The previous credential is now invalid, any token it already established has " +
                    "been revoked, and the device must be reconfigured.");

                return credential;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }
        
        /// <summary>
        /// this is not complete as it is not getting the id of the hardware that needs to be updated
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<LocalHardware> Update(LocalHardwareCreationViewModel input)
        {
            LocalHardware output = null;
            try
            {
                // Audit C-09: this built a BRAND-NEW LocalHardware with `//Id = input.Id` commented
                // out and then called Update. LocalHardware.Id is store-generated, so EF saw a
                // default key, marked the entity Added, and INSERTED. Editing a device did not fail
                // to save -- it silently created a SECOND device row carrying the SAME
                // PhysicalLicense, which is the authentication credential. GetByKey resolves that
                // credential with an unordered FirstOrDefaultAsync over a column with no unique
                // constraint and no index, so WHICH row authenticates is arbitrary. Locking a
                // device therefore reported success while the device carried on authenticating as
                // the other row. The duplicate also took a fresh uuid_generate_v4() UUID, so
                // anything resolving that device by UUID broke too.
                //
                // The commented line could never have compiled as written: HardwareCreationViewModel
                // has no Id. The real source is input.Hardware.Id, which Edit.cshtml round-trips.
                //
                // Fixed as CohortLogic.Update was: load the stored row, copy the editable fields
                // onto it. HardwareQueries.Get uses FindAsync, which TRACKS, so the copy has to go
                // onto that instance -- passing a second instance with the same key to
                // DbSet.Update throws. Id, UUID and TimeStamp are now preserved by construction
                // instead of by being remembered.
                LocalHardware hardware = await _context.Get(input.Hardware.Id);
                if (hardware == null)
                {
                    return default;
                }

                hardware.HardwareKind = input.Hardware.HardwareKind;
                hardware.Description = input.Hardware.Description;
                hardware.DescriptiveName = input.Hardware.DescriptiveName;
                // PhysicalLicense DELIBERATELY NOT COPIED (audit T9). The stored value is a
                // hash, and the edit form no longer carries the field, so copying whatever the
                // view model happened to hold would overwrite a valid hash with an empty string
                // and silently lock the device out. Use RegenerateCredential to issue a new one.
                hardware.HardwareCondition = input.Hardware.HardwareCondition;
                hardware.IsLockedOut = input.Hardware.IsLockedOut;
                hardware.LastUpdateTimeStamp = DateTime.Now;
                // Re-stamped every save, so a device edited to a different kind does not keep the
                // previous kind's reconciliation carriers.

                output = await _context.Update(hardware);

                // A-02 Stage 2. Persisting IsLockedOut is not enough on its own: the per-request
                // check reads the SIGNED CLAIM, so a device that already holds a token keeps working
                // until it expires. Publishing the revocation makes the middleware refuse it on the
                // very next request instead. TTL matches the access-token lifetime, so the entry
                // retires itself once the token it revokes would have died anyway.
                //
                // Only on the locking transition. Unlocking needs no entry: the existing one expires,
                // and issuance and refresh both re-read the live row, so an unlocked device
                // re-authenticates normally.
                if (_revocations != null && hardware.IsLockedOut)
                {
                    await _revocations.RevokeAsync(hardware.UUID, TimeSpan.FromMinutes(15));
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }



        public async Task<GenericMixedChart> GetNewHardwareMixedChart()
        {
            GenericMixedChart output = new GenericMixedChart()
            {
                Title = "Hardware Registration",
                IdToUse = Guid.NewGuid().ToString().Replace("-", string.Empty),
                Description = "30 Days",
                GenericChartList = new List<GenericChart>()
            };
            List<GenericChartEntry> pieChartEntryList = new List<GenericChartEntry>();
            List<GenericChartEntry> lineChartEntryList = new List<GenericChartEntry>();
            try
            {
                //Build lists
                GenericChart lineChart = new GenericChart()
                {
                    ChartType = ChartType.Line,
                    GenericChartEntryList = lineChartEntryList
                };
                GenericChart pieChart = new GenericChart()
                {
                    ChartType = ChartType.Pie,
                    GenericChartEntryList = pieChartEntryList
                };
                output.GenericChartList.Add(lineChart);
                output.GenericChartList.Add(pieChart);
                //DateTime startDate = DateTime.UtcNow.AddYears(-1).Date;
                DateTime startDate = DateTime.UtcNow.AddDays(-30).Date;
                DateTime endDate = DateTime.UtcNow.Date;
#if (DEBUG)
                List<LocalHardware> tempList = await Get();
#else
List<LocalHardware> tempList = await Get(startDate, endDate);
#endif
                //List<Hardware> tempList = await Get(DateTime.UtcNow.AddDays(-30).Date, DateTime.UtcNow.Date);
                tempList = tempList.OrderBy(i => i.TimeStamp).ToList();
                //tempList.OrderByDescending(i => i.CreationTimeStamp).ToList();               

                for (DateTime i = startDate; endDate >= i; i = i.AddDays(1))
                {
                    int qty = tempList.Where(j => j.TimeStamp.Date == i).Count();
                    GenericChartEntry temp = new GenericChartEntry()
                    {
                        Label = i.ToShortDateString(),
                        Quantity = qty
                    };
                    lineChartEntryList.Add(temp);
                }


                // Slices come off the enum now. This used to project through HardwareType and
                // dereference j.HardwareType.Id per row, which threw a NullReferenceException for
                // any device whose HardwareTypeId did not resolve -- and nothing enforced that it
                // did, since the real Hardware table has no foreign key to the lookup. The whole
                // dashboard chart is inside a catch that swallows, so it failed silently.
                List<HardwareKind> kindList = tempList.Select(i => i.HardwareKind).Distinct().ToList();

                foreach (HardwareKind i in kindList)
                {
                    GenericChartEntry pieTemp = new GenericChartEntry()
                    {
                        // The enum's [Display] name, so a slice reads "Laptop PC" rather than
                        // "LaptopPC". Falls back to the member name if the attribute is removed.
                        Label = DisplayNameOf(i),
                        Quantity = tempList.Where(j => j.HardwareKind == i).Count()
                    };
                    pieChartEntryList.Add(pieTemp);
                }

                //foreach (var i in tempList)
                //{
                //    //if (lineChartEntryList.Any(j => j.Label == i.TimeStamp.ToShortDateString()))
                //    //{
                //    //    GenericChartEntry lineTemp = lineChartEntryList.Where(j => j.Label == i.TimeStamp.ToShortDateString()).First();
                //    //    lineTemp.Quantity++;
                //    //}
                //    //else
                //    //{
                //    //    GenericChartEntry lineTemp = new GenericChartEntry()
                //    //    {
                //    //        Label = i.TimeStamp.ToShortDateString(),
                //    //        Quantity = 1
                //    //    };
                //    //    lineChartEntryList.Add(lineTemp);
                //    //}



                //    if (pieChartEntryList.Any(j => j.Label == i.HardwareType.Name))
                //    {
                //        GenericChartEntry pieTemp = pieChartEntryList.Where(j => j.Label == i.HardwareType.Name).First();
                //        pieTemp.Quantity++;
                //    }
                //    else
                //    {
                //        GenericChartEntry pieTemp = new GenericChartEntry()
                //        {
                //            Label = i.HardwareType.Name,
                //            Quantity = 1
                //        };
                //        pieChartEntryList.Add(pieTemp);
                //    }
                //}

                //output = await ManagementAlgorithms.OrderChartLists(output);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                //throw;
            }
            return output;
        }

        private async Task<List<LocalHardware>> Get(DateTime startDate, DateTime endDate)
        {
            List<LocalHardware> output = new List<LocalHardware>();
            try
            {
                output = await _context.Get(startDate, endDate);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
    }
       
}
