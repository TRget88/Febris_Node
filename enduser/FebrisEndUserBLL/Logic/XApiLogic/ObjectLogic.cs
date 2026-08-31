// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.EnumLibrary;
using Febris.ModelLibrary.LookupModels;
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.ViewModels;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.LogicLayer.Logic.DataLogic;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IObjectLogic
    {
        Task<(ModelLibrary.Models.XApiModels.Object xApiObject, string StatusMessage)> Create(ModuleCreationViewModel input);
        Task<(ModelLibrary.Models.XApiModels.Object xApiObject, string StatusMessage)> Update(ModuleCreationViewModel input);
        Task<ModelLibrary.Models.XApiModels.Object> Get(long id);
        Task<List<ModelLibrary.Models.XApiModels.Object>> Get();
        Task<ModelLibrary.Models.XApiModels.Object> Get(long? id);
        Task<ModelLibrary.Models.XApiModels.Object> Create(ModelLibrary.Models.XApiModels.Object input);
        Task<ModelLibrary.Models.XApiModels.Object> Update(ModelLibrary.Models.XApiModels.Object input);
    }
    public class ObjectLogic : IObjectLogic
    {
        private IObjectQueries _objectQueries;
        private IDefinitionLogic _definitionContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IModuleLinkedObjectLogic _objectLinkContext;
        private readonly ClaimsPrincipal User;

        public ObjectLogic(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
            _objectQueries = new ObjectQueries();
            _definitionContext = new DefinitionLogic(_httpContextAccessor);
            _objectLinkContext = new ModuleLinkedObjectLogic(_httpContextAccessor);
            User = _httpContextAccessor.HttpContext.User;
        }

        // DI refactor
        public ObjectLogic(IHttpContextAccessor httpContextAccessor, IObjectQueries objectQueries, IDefinitionLogic definitionContext, IModuleLinkedObjectLogic objectLinkContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _objectQueries = objectQueries;
            _definitionContext = definitionContext;
            _objectLinkContext = objectLinkContext;
            User = _httpContextAccessor?.HttpContext?.User;
        }

        [TempData]
        public string StatusMessage { get; set; }


        /// <summary>
        /// This seems like detail url
        /// </summary>
        //private string ObjectDetailsUri = string.Empty;

        #region Get
        public async Task<ModelLibrary.Models.XApiModels.Object> Get(long input)
        {
            ModelLibrary.Models.XApiModels.Object output = await _objectQueries.Get(input);
            return output;
        }

        public async Task<ModelLibrary.Models.XApiModels.Object> Get(Guid input)
        {
            ModelLibrary.Models.XApiModels.Object output = await _objectQueries.Get(input);
            return output;
        }

        public async Task<List<ModelLibrary.Models.XApiModels.Object>> Get()
        {
            List<ModelLibrary.Models.XApiModels.Object> output = await _objectQueries.Get();
            return output;
        }

        #endregion

        #region Post
        /// <summary>
        /// Persist an already-built Activity. No caller today (the node mints Objects from modules
        /// via the ModuleCreationViewModel overload below), but its persist was commented out with
        /// the same hub-era assumption, which left a method that silently discarded its input and
        /// returned a blank Object to anyone who found it. Restored so the name matches the
        /// behaviour.
        /// </summary>
        public async Task<Febris.ModelLibrary.Models.XApiModels.Object> Create(Febris.ModelLibrary.Models.XApiModels.Object input)
        {
            Febris.ModelLibrary.Models.XApiModels.Object output = new Febris.ModelLibrary.Models.XApiModels.Object();
            try
            {
                output = await _objectQueries.Create(input);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "ObjectLogic.Create: suppressed exception");
            }

            return output;
        }

        /// <summary>
        /// The module-to-Activity builder: mints the module's xAPI Object (Activity) from its
        /// catalog row and PERSISTS it. Owner ruling (ROADMAP 15): node-local xAPI Objects are
        /// created WITH the module, so the node's package-ingest path calls this and links the
        /// returned Object into ModuleLinkedObject. The IRI is derived from the module's stable
        /// UUID, so re-minting for the same module would duplicate the activity -- callers resolve
        /// an existing link first rather than calling this twice.
        /// </summary>
        public async Task<(ModelLibrary.Models.XApiModels.Object xApiObject, string StatusMessage)> Create(ModuleCreationViewModel input)
        {
            Febris.ModelLibrary.Models.XApiModels.Object output = new Febris.ModelLibrary.Models.XApiModels.Object();

            if (input == null || input.Module == null)
            {
                // Without a Module there is no IRI, no language map and no interaction type. Say so
                // instead of falling into the catch and returning a blank Object with no message.
                StatusMessage = "An xAPI Object cannot be built without a module.";
                return (output, StatusMessage);
            }

            try
            {
                //IRI ID creation
                if (input.IRIId == null)
                {
                    input.IRIId=new Uri(StaticDetails.xApiObjectUri + input.Module.UUID);
                }

                //resolve interaction type
                string responsePattern = InteractionTypeResolver.XApiInteractionTypeResolver(input.Module.XApiInteractionType);
                
                //Build xApi components
                IJsonStringDictionaryBuilder stringBuilder = new JsonStringDictionaryBuilder();
                // xAPI 1.0.3 Language Maps built directly as typed dictionaries (locale -> text),
                // replacing the JSON-string builder that stuffed a serialized map into a string column.
                string moduleLocale = input.Module.Language.ToString().Replace('_', '-');
                Dictionary<string, string> serializedNameDictionaryString = new Dictionary<string, string> { [moduleLocale] = input.Module.Name };
                Dictionary<string, string> serializedDescriptionDictionaryString = new Dictionary<string, string> { [moduleLocale] = input.Module.Description };

                //Extensions extensions = new Extensions()
                //{
                //};


                //create and save definition
                Definition definition = new Definition()
                {
                    //Extensions = extensions,                    
                    Name = serializedNameDictionaryString,
                    Description = serializedDescriptionDictionaryString,
                    Type = new Uri(StaticDetails.xApiObjectUri + input.Module.Id),
                    MoreInfo = new Uri(StaticDetails.xApiObjectUri + input.Module.Id),
                    InteractionType = input.Module.XApiInteractionType.ToString(),
                    InteractionComponents = input.Module.InteractionComponents,
                    //InteractionComponents = serializedInteractionComponentString,
                    // xAPI correctResponsesPattern is a string ARRAY; wrap the resolver's single
                    // pattern string. (The CMI pattern encoding itself is preserved as-is.)
                    CorrectResponsesPattern = string.IsNullOrEmpty(responsePattern) ? null : new List<string> { responsePattern },
                };
                // The Definition is NOT persisted separately: ObjectQueries.Create adds the Object
                // with this instance on its nav property, so EF inserts the Definition row in the
                // same SaveChanges. Persisting it here first would be a second write of the same
                // row. (Left commented rather than deleted -- it is the hub-era shape.)
                //definition = await _definitionContext.Create(definition);

                //create object
                output = new ModelLibrary.Models.XApiModels.Object()
                {
                    ObjectType = "Activity",
                    Id = input.IRIId,
                    Definition = definition
                };
                // ROADMAP 15: the persist that made this builder dead code. Without it the node
                // mints an in-memory Activity with Key 0 and no row, so the module's
                // ModuleLinkedObject can never point at anything and the module cannot launch.
                // Key and UUID are store-generated (identity + uuid_generate_v4), so the returned
                // instance carries the values the link row needs.
                output = await _objectQueries.Create(output);
            }
            catch (Exception ex)
            {
                StatusMessage = ex.Message;
                Febris.SharedServices.FebrisLog.Error(ex);
            }

            return (output,StatusMessage);
        }


        #endregion

        #region Update
        public async Task<(ModelLibrary.Models.XApiModels.Object xApiObject, string StatusMessage)> Update(ModuleCreationViewModel input)
        {
            ModelLibrary.Models.XApiModels.Object xApiObject = new ModelLibrary.Models.XApiModels.Object();
            try
            {
                ///ModuleLinkedObject link = await _objectLinkContext.Get(input.Module);
               // xApiObject = await _objectQueries.Get(link.ObjectId);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                StatusMessage = ex.Message;
                throw;
            }
            return (xApiObject,StatusMessage);
        }

        public Task<ModelLibrary.Models.XApiModels.Object> Get(long? id)
        {
            throw new NotImplementedException();
        }

        public Task<ModelLibrary.Models.XApiModels.Object> Update(ModelLibrary.Models.XApiModels.Object input)
        {
            throw new NotImplementedException();
        }
        //public async Task<Febris.ModelLibrary.Models.XApiModels.Object> Update(Febris.ModelLibrary.Models.XApiModels.Object input)
        //{
        //    Febris.ModelLibrary.Models.XApiModels.Object output = new Febris.ModelLibrary.Models.XApiModels.Object();
        //    try
        //    {
        //        output = await _objectQueries.Update(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        ////public async Task<IObject> Update(IObject input)
        ////{
        ////    IObject output = new Febris.ModelLibrary.Models.XApiModels.Object();
        ////    try
        ////    {
        ////        output = await _objectQueries.Update(input);
        ////    }
        ////    catch
        ////    {

        ////    }

        ////    return output;
        ////}

        //public async Task<Febris.ModelLibrary.Models.XApiModels.Object> Update(ModuleCreationViewModel input)
        //{
        //    Febris.ModelLibrary.Models.XApiModels.Object output = new Febris.ModelLibrary.Models.XApiModels.Object();
        //    try
        //    {
        //        #region create xapi object inputs needed                                
        //        string responsePattern = string.Empty;
        //        string serializedNameDictionaryString = string.Empty;
        //        string serializedDescriptionDictionaryString = string.Empty;
        //        Utilities.IJsonStringDictionaryBuilder stringBuilder = new Utilities.JsonStringDictionaryBuilder();

        //        if (input.IRIId == null)
        //        {
        //            input.IRIId = new Uri(ObjectDetailsUri + input.Module.UUID);
        //        }
        //        responsePattern = InteractionTypeResolver.XApiInteractionTypeResolver(input.Module.XApiInteractionType);

        //        serializedNameDictionaryString = stringBuilder.NewJsonDictionaryStringBuilder(input.Module.Language.ToString().Replace('_', '-'), input.Module.Name);
        //        serializedDescriptionDictionaryString = stringBuilder.NewJsonDictionaryStringBuilder(input.Module.Language.ToString().Replace('_', '-'), input.Module.Description);

        //        #region Create Definition
        //        Definition definition = new Definition()
        //        {
        //            Name = serializedNameDictionaryString,
        //            Description = serializedDescriptionDictionaryString,
        //            Type = new Uri(ObjectDetailsUri + input.Module.Id),
        //            MoreInfo = new Uri(ObjectDetailsUri + input.Module.Id),
        //            InteractionType = input.Module.XApiInteractionType.ToString(),
        //            InteractionComponents = input.Module.InteractionComponents,
        //            CorrectResponsesPattern = responsePattern
        //        };
        //        definition = await _definitionQueries.Update(definition);
        //        #endregion

        //        output = new Febris.ModelLibrary.Models.XApiModels.Object()
        //        {
        //            Id = input.IRIId,
        //            ObjectType = "Activity",
        //            Definition = (Definition)definition
        //        };
        //        #endregion

        //        output = await _objectQueries.Update(output);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<IObject> Update(ModuleCreationViewModel input)
        //{
        //    IObject output = new Febris.ModelLibrary.Models.XApiModels.Object();
        //    try
        //    {
        //        #region create xapi object inputs needed                                
        //        string responsePattern = string.Empty;
        //        string serializedNameDictionaryString = string.Empty;
        //        string serializedDescriptionDictionaryString = string.Empty;
        //        IJsonStringDictionaryBuilder stringBuilder = new JsonStringDictionaryBuilder();

        //        if (input.IRIId == null)
        //        {
        //            input.IRIId = new Uri(StaticDetails.xApiObjectUri + input.ModuleBase.UUID);
        //        }
        //        responsePattern = InteractionTypeResolver.XApiInteractionTypeResolver(input.ModuleBase.XApiInteractionType);

        //        serializedNameDictionaryString = stringBuilder.NewJsonDictionaryStringBuilder(input.ModuleBase.Language.ToString().Replace('_', '-'), input.ModuleBase.Name);
        //        serializedDescriptionDictionaryString = stringBuilder.NewJsonDictionaryStringBuilder(input.ModuleBase.Language.ToString().Replace('_', '-'), input.ModuleBase.Description);

        //        #region Create Definition
        //        IDefinition definition = new Definition()
        //        {
        //            Name = serializedNameDictionaryString,
        //            Description = serializedDescriptionDictionaryString,
        //            Type = new Uri(StaticDetails.xApiObjectUri + input.ModuleBase.Id),
        //            MoreInfo = new Uri(StaticDetails.xApiObjectUri + input.ModuleBase.Id),
        //            InteractionType = input.ModuleBase.XApiInteractionType.ToString(),
        //            InteractionComponents = input.ModuleBase.InteractionComponents,
        //            CorrectResponsesPattern = responsePattern
        //        };
        //        definition = await _definitionQueries.Update(definition);
        //        #endregion

        //        output = new Febris.ModelLibrary.Models.XApiModels.Object()
        //        {
        //            Id = input.IRIId,
        //            ObjectType = "Activity",
        //            Definition = (Definition)definition
        //        };
        //        #endregion

        //        output = await _objectQueries.Update(output);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion

        #region Delete
        //public async Task<bool> Delete(Febris.ModelLibrary.Models.XApiModels.Object input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _objectQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        //public async Task<bool> Delete(IObject input)
        //{
        //    bool output = false;
        //    try
        //    {
        //        output = await _objectQueries.Delete(input);
        //    }
        //    catch
        //    {

        //    }

        //    return output;
        //}
        #endregion
    }

}
