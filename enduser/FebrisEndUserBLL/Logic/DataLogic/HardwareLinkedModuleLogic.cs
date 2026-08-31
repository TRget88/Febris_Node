// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.DataModels;
using Febris.UserNode.DataAccessLayer.Queries.DataQueries;
using Febris.UserNode.LogicLayer.Logic.AnalyticsLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.DataLogic
{
    public interface IHardwareLinkedModuleLogic
    {
        //Task<List<HardwareLinkedModule>> GetByHardware(long? id);
        //Task<HardwareLinkedModule> Create(long hardwareId, long moduleId);
        //Task<bool> Remove(HardwareLinkedModule link);
        //Task<List<HardwareLinkedModule>> GetByHardware(long? id);
        Task<List<LocalHardwareLinkedModuleViewModel>> GetByHardware(long? id);
        Task<LocalHardwareLinkedModule> Create(long hardwareId, long moduleId);
        Task<bool> Remove(LocalHardwareLinkedModule link);
        /// <summary>
        /// The entitlement-gated module download. Returns Stream (was FileStream) because
        /// store-ingested packages stream from IStorageProvider, which is not
        /// FileStream-shaped on non-filesystem backends. Null when the hardware has no
        /// HardwareLinkedModule entitlement link.
        /// </summary>
        Task<Stream> Download(Hardware hardware, Module module);
        Task<List<Module>> Get(LocalHardware input);
    }
    public class HardwareLinkedModuleLogic: IHardwareLinkedModuleLogic
    {
        //private IHardwareLinkedModuleQueries _hardwareLinkedModuleQueries = new SharedDataAccessLayer.Queries.DataQueries.HardwareLinkedModuleQueries();
        //private HardwareLinkedModuleQueries _context = new SharedDataAccessLayer.Queries.DataQueries.HardwareLinkedModuleQueries();
        private readonly IHardwareLinkedModuleQueries _context;// = new HardwareLinkedModuleQueries();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ClaimsPrincipal User;
        private readonly IModuleFileHandler _fileServerHandler;
        private readonly IHardwareQueries _hardwareContext;
        private readonly IModuleQueries _moduleContext;
        private readonly IModuleDownloadAnalyticsLogic _moduleDownloadAnalyticscontext;
        // Artifact bookkeeping + storage seam for store-ingested packages.
        // Null on the legacy self-newing path, which keeps serving off the legacy file layout.
        private readonly IPackageArtifactQueries _artifactContext;
        private readonly Febris.SharedServices.Storage.IStorageProvider _storage;
        // SCBA-B3 port (node hygiene D): singleton-safe factory used to run the download-analytics
        // write on a FRESH DI scope instead of capturing this request's scoped analytics logic
        // (and its DbContext) across the fire-and-forget boundary. Null on the legacy self-newing
        // path, where ScopedBackgroundWork's legacy fallback preserves the pre-fix behavior.
        private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;
        // DI refactor
        /// <summary>
        /// DI constructor. The storage seam + artifact queries let <see cref="Download"/> serve
        /// store-ingested packages through IStorageProvider while legacy
        /// file-layout modules keep flowing through IModuleFileHandler. The scope factory backs
        /// the SCBA-B3 fire-and-forget analytics write (mirrors the shared ModuleLogic twin).
        /// </summary>
        public HardwareLinkedModuleLogic(IHttpContextAccessor httpContextAccessor, IHardwareLinkedModuleQueries context, IModuleFileHandler fileServerHandler, IHardwareQueries hardwareContext, IModuleQueries moduleContext, IModuleDownloadAnalyticsLogic moduleDownloadAnalyticscontext, IPackageArtifactQueries artifactContext, Febris.SharedServices.Storage.IStorageProvider storage, Microsoft.Extensions.DependencyInjection.IServiceScopeFactory scopeFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor?.HttpContext?.User;
            _context = context;
            _fileServerHandler = fileServerHandler;
            _hardwareContext = hardwareContext;
            _moduleContext = moduleContext;
            _moduleDownloadAnalyticscontext = moduleDownloadAnalyticscontext;
            _artifactContext = artifactContext;
            _storage = storage;
            _scopeFactory = scopeFactory;
        }

        public HardwareLinkedModuleLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            User = _httpContextAccessor.HttpContext.User;
            _context = new HardwareLinkedModuleQueries();
            _fileServerHandler = new ModuleFileHandler();
            _hardwareContext = new HardwareQueries();
            _moduleContext = new ModuleQueries();
            _moduleDownloadAnalyticscontext = new ModuleDownloadAnalyticsLogic(_httpContextAccessor);
            // No storage seam on the legacy path (IStorageProvider cannot be newed from static
            // config); Download falls through to the legacy file handler, which resolves the SAME
            // on-disk location for store-ingested modules (modules/{uuid}.zip is 1:1 with
            // StaticDetails.ModuleFileSystemPath) when the deployment uses the filesystem backend.
            _artifactContext = new PackageArtifactQueries();
            _storage = null;
        }

       
        #region Get
        public async Task<List<LocalHardwareLinkedModule>> Get()
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
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
        public async Task<LocalHardwareLinkedModule> Get(long? input)
        {
            LocalHardwareLinkedModule output = new LocalHardwareLinkedModule();
            try
            {
                output = await _context.Get(input);

            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<LocalHardwareLinkedModule> Get(Guid input)
        {
            LocalHardwareLinkedModule output = new LocalHardwareLinkedModule();
            try
            {
                output = await _context.Get(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByHardware(Guid input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _context.GetByHardware(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        //public async Task<List<LocalHardwareLinkedModule>> GetByHardware(long? input)
        //{
        //    List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
        //    try
        //    {
        //        output = await _context.GetByHardware(input);
        //    }
        //    catch (Exception ex)
        //    {
        //        Febris.SharedServices.FebrisLog.Error(ex);
        //        throw;
        //    }
        //    return output;
        //}

        /// <summary>
        /// I think I can actually use the LocalHardwareLinkedModule instead of using types
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public async Task<List<LocalHardwareLinkedModuleViewModel>> GetByHardware(long? input)
        {
            List<LocalHardwareLinkedModuleViewModel> output = new List<LocalHardwareLinkedModuleViewModel>();
            try
            {
                List<LocalHardwareLinkedModule> preoutput = await _context.GetByHardware(input);

                foreach (var i in preoutput)
                {
                    Module tempModule = await _moduleContext.Get(i.ModuleUUID);
                    //HardwareType tempHardwareType = await _hardwareTypeContext.Get(i.Hardware.HardwareTypeId); //these may not be needed
                    //Hardware tempHardware = new Hardware()
                    //{
                    //    HardwareType = tempHardwareType,
                    //    HardwareTypeUUID = tempHardwareType.UUID,
                    //    DescriptiveName = i.Hardware.DescriptiveName,
                    //    Description = i.Hardware.Description,
                    //    PhysicalLicense = i.Hardware.PhysicalLicense,
                    //    HardwareCondition = i.Hardware.HardwareCondition,
                    //    IsLockedOut = i.Hardware.IsLockedOut
                    //};

                    LocalHardwareLinkedModuleViewModel tempLink = new LocalHardwareLinkedModuleViewModel()
                    {
                        LocalHardwareLinkedModule = i,                                    
                        Module = tempModule,
                    };
                    output.Add(tempLink);
                }

                //foreach(var i in preoutput)
                //{
                //    Module tempModule = await _moduleContext.Get(i.ModuleUUID);
                //    HardwareType tempHardwareType = await _hardwareTypeContext.Get(i.Hardware.HardwareTypeId); //these may not be needed
                //    Hardware tempHardware = new Hardware()
                //    {
                //        HardwareType = tempHardwareType,
                //        HardwareTypeUUID = tempHardwareType.UUID,
                //        DescriptiveName = i.Hardware.DescriptiveName,
                //        Description = i.Hardware.Description,
                //        PhysicalLicense = i.Hardware.PhysicalLicense,
                //        HardwareCondition = i.Hardware.HardwareCondition,
                //        IsLockedOut = i.Hardware.IsLockedOut
                //    };

                //    HardwareLinkedModule tempLink = new HardwareLinkedModule()
                //    {
                //        Hardware = tempHardware,
                //        HardwareUUID = i.Hardware.UUID,
                //        Module = tempModule,
                //        ModuleUUID = tempModule.UUID
                //    };
                //}


            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<Module>> Get(LocalHardware input)
        {
            List<Module> output = new List<Module>();
            try
            {
                LocalHardware hardware = new LocalHardware();
                if (input.Id == 0)
                {
                    hardware = await _hardwareContext.Get(input.UUID);
                }
                else
                {
                    hardware = await _hardwareContext.Get(input.Id);
                }

                List<LocalHardwareLinkedModule> preoutput = await _context.Get(hardware);

                foreach (var i in preoutput)
                {
                    Module tempModule = await _moduleContext.Get(i.ModuleId);
                    output.Add(tempModule);
                }
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByModule(Guid input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _context.GetByModule(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }
        public async Task<List<LocalHardwareLinkedModule>> GetByModule(long input)
        {
            List<LocalHardwareLinkedModule> output = new List<LocalHardwareLinkedModule>();
            try
            {
                output = await _context.GetByModule(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }

        #endregion

        #region Create
        public async Task<LocalHardwareLinkedModule> Create(LocalHardwareLinkedModule input)
        {
            try
            {
                input = await _context.Create(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return input;
        }

        public async Task<LocalHardwareLinkedModule> Create(long hardwareId, long moduleId)
        {
            LocalHardwareLinkedModule output = new LocalHardwareLinkedModule();
            try
            {
                LocalHardware hardware = await _hardwareContext.Get(hardwareId);
                Module module = await _moduleContext.Get(moduleId);

                output = new LocalHardwareLinkedModule()
                {
                    Hardware = hardware,
                    HardwareUUID = hardware.UUID,
                    ModuleId = module.Id,
                    ModuleUUID = module.UUID
                };

                output = await _context.Create(output);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            
        }

        #endregion

        #region Update

        public async Task<LocalHardwareLinkedModule> Update(LocalHardwareLinkedModule input)
        {
            try
            {
                input = await _context.Update(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return input;
        }
        #endregion

        #region Delete

        public async Task<bool> Delete(LocalHardwareLinkedModule input)
        {
            bool output = false;
            try
            {
                output = await _context.Delete(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }

        public async Task<bool> Remove(LocalHardwareLinkedModule input)
        {
            bool output = false;
            try
            {
                output = await _context.Delete(input);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }

            return output;
        }




        #endregion

        /// <inheritdoc />
        public async Task<Stream> Download(Hardware hardware, Module module)
        {

            try
            {
                bool linkExists = await _context.Exists(hardware, module);
                if (!linkExists)
                {
                    return null;
                }
                // SCBA-B3 port (node hygiene D): record the download on its own DI scope -- the
                // request-scoped _moduleDownloadAnalyticscontext (and its AnalyticsDbContext) must
                // not be captured by a Task that outlives the request. Mirrors the shared
                // ModuleLogic twin; the legacy fallback keeps the pre-fix behavior for the
                // self-newing constructor path.
                ScopedBackgroundWork.FireAndForget<IModuleDownloadAnalyticsLogic>(
                    _scopeFactory,
                    l => l.LogRequest(User, hardware, module),
                    () => _moduleDownloadAnalyticscontext.LogRequest(User, hardware, module));

                // Packages ingested through the node's own store (an artifact
                // row exists for the module's conventional key) stream from IStorageProvider;
                // everything else keeps flowing through the legacy file handler untouched.
                string storageKey = Febris.SharedServices.Storage.StorageKeys.Module(module.UUID.ToString() + ".zip");
                if (_storage != null)
                {
                    Febris.ModelLibrary.Models.DataModels.PackageArtifact artifact = await _artifactContext.GetByStorageKey(storageKey);
                    if (artifact != null)
                    {
                        return await _storage.OpenReadAsync(storageKey);
                    }
                }

                Stream output = await _fileServerHandler.Download(module);
                return output;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
        }

        
    }

 
}
