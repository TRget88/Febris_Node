// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.ModelLibrary.Models.XApiModels.ExtraModels;
using Febris.ModelLibrary.ViewModels.XApi;
using Newtonsoft.Json;
using Febris.UserNode.DataAccessLayer.Queries.XApiQueries;
using Febris.UserNode.DataAccessLayer.Queries.XAPIQueries;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IStatementFactor
    {
    }

    public class StatementFactor: IStatementFactor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        //private readonly ClaimsPrincipal User;
        
        //statement
        private readonly IStatementQueries _context;
        //private readonly ILocalStatementQueries _localContext;_extensionContext

        //actor 
        private readonly IActorQueries _actorContext;
        private readonly IMemberQueries _memberContext;
        //private readonly IAccountQueries _accountContext;

        //object
        private readonly IObjectQueries _objectContext;

        //verb
        private readonly IVerbQueries _verbContext;

        //result
        private readonly IExtensionsQueries _extensionContext; 

        private readonly IVersionQueries _versionContext;
        //private readonly IContentDeveloperQueries _devContext;
        //private readonly IAccreditationBodyQueries _accContext;
        

        #region [Historical] legacy self-newing ctor (superseded: callers now supply the queries)
        // StatementLogic -- the only construction site -- now builds the factor with ITS OWN query
        // instances through the DI ctor below, so the factor no longer news its own query classes
        // (any `new` bypasses the DI seam and would silently skip the scoped DbContext). Kept for
        // the record of the pre-DI shape, incl. the _extensionContext NRE fix note.
        //public StatementFactor(IHttpContextAccessor httpContextAccessor)
        //{
        //    _httpContextAccessor = httpContextAccessor;
        //    //User = _httpContextAccessor.HttpContext.User;

        //    _context = new StatementQueries();
        //    //_localContext = new LocalStatementQueries();

        //    //Actor
        //    _actorContext = new ActorQueries();
        //    _memberContext = new MemberQueries();

        //    //Object
        //    _objectContext = new ObjectQueries();

        //    //verb
        //    _verbContext = new VerbQueries();


        //    //_devContext = new ContentDeveloperQueries();
        //    //_accContext = new AccreditationBodyQueries();

        //    _versionContext = new VersionQueries();
        //    // _localContext = new LocalStatementQueries();

        //    // Bug fix: _extensionContext was declared at line 43 but
        //    // never initialized -- any call into SetupExtensions /
        //    // SetupExtensionsFromDto that hit the Febris-dialect
        //    // "id" lookup branch would NRE. The shared StatementFactor
        //    // initializes this; the EndUser twin was missing the line.
        //    // Adding it here brings the two factors into shape parity.
        //    _extensionContext = new ExtensionsQueries();
        //}
        #endregion

        // DI refactor
        public StatementFactor(IHttpContextAccessor httpContextAccessor, IStatementQueries context, IActorQueries actorContext, IMemberQueries memberContext, IObjectQueries objectContext, IVerbQueries verbContext, IVersionQueries versionContext, IExtensionsQueries extensionContext)
        {
            _httpContextAccessor = httpContextAccessor;
            //User = _httpContextAccessor.HttpContext.User;

            _context = context;
            //_localContext = new LocalStatementQueries();

            //Actor
            _actorContext = actorContext;
            _memberContext = memberContext;

            //Object
            _objectContext = objectContext;

            //verb
            _verbContext = verbContext;


            //_devContext = new ContentDeveloperQueries();
            //_accContext = new AccreditationBodyQueries();

            _versionContext = versionContext;
            // _localContext = new LocalStatementQueries();

            // Bug fix: _extensionContext was declared at line 43 but
            // never initialized -- any call into SetupExtensions /
            // SetupExtensionsFromDto that hit the Febris-dialect
            // "id" lookup branch would NRE. The shared StatementFactor
            // initializes this; the EndUser twin was missing the line.
            // Adding it here brings the two factors into shape parity.
            _extensionContext = extensionContext;
        }



        //#########################################################################################################################
        // Case-insensitive JObject reads (SDKV-17/18)
        //#########################################################################################################################
        /// <summary>
        /// Case-insensitive child lookup for the JObject factor path.
        /// <para>
        /// The default /Submit route re-serializes the bound DTO via
        /// <c>JObject.FromObject(submission.Dto)</c>, which emits the DTO's
        /// camelCase <c>JsonProperty</c> names (<c>usageType</c>,
        /// <c>contextActivities</c>, ...), while this factor historically read
        /// lowercase Febris-dialect keys with the case-SENSITIVE JObject
        /// indexer -- so those reads silently missed and attachments /
        /// context activities were dropped (SDKV-17/18). Matching with
        /// <see cref="StringComparison.OrdinalIgnoreCase"/> lets BOTH the
        /// lowercase dialect and spec xAPI 1.0.3 casing parse.
        /// </para>
        /// <para>
        /// Accepts fallback names so deliberately-preserved dialect aliases
        /// (e.g. the <c>contextactivites</c> typo) keep working. Returns null
        /// for a non-object input, an absent key, or an explicit JSON null --
        /// the "absent" semantics every Setup* method already handles.
        /// </para>
        /// </summary>
        // Tolerant xAPI Language Map read: accepts a spec Language Map object ({"en":"x"}) OR a bare
        // string (the legacy/dialect form), the latter mapped to the "und" (undetermined) locale.
        // Preserves the node's dual-form ingest tolerance now that the model is a typed Dictionary.
        private static Dictionary<string, string> ReadLanguageMap(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.Object) return token.ToObject<Dictionary<string, string>>();
            return new Dictionary<string, string> { ["und"] = token.ToString() };
        }

        private static JToken GetTokenCaseInsensitive(JToken input, params string[] names)
        {
            if (!(input is JObject obj)) return null;
            foreach (string name in names)
            {
                JToken match = obj.GetValue(name, StringComparison.OrdinalIgnoreCase);
                if (match != null && match.Type != JTokenType.Null)
                {
                    return match;
                }
            }
            return null;
        }

        //#########################################################################################################################
        //May need to convert specific items in string to other strings - ie model names or uuid keys
        //#########################################################################################################################
        public async Task<(Statement Statement, bool ParsedCorrectly)> FactorStatement(JObject input)
        {
            try
            {
                //Statement Variables
                Statement statement = new Statement();
                Actor actor = new Actor();
                Febris.ModelLibrary.Models.XApiModels.Object xAPIObject = new Febris.ModelLibrary.Models.XApiModels.Object();
                Verb verb = new Verb();
                Result result = new Result();
                Context context = new Context();
                Authority authority = new Authority();
                List<Febris.ModelLibrary.Models.XApiModels.Attachment> attachments = new List<Febris.ModelLibrary.Models.XApiModels.Attachment>();
                JToken statementToken = input;
                //***************************************************needs to go to lowercase
                //JObject converToLower = Object.from;
                // NOTE (LMS-B9): EndUser copy has ChangePropertiesToLowerCase commented out here while the shared copy runs it active, so PascalCase keys are silently dropped on the live /Backup endpoint. Proper fix is consolidating the three divergent StatementFactor copies, which is a systemic multi-file change. Deferred rather than changing functionality here.
                //ChangePropertiesToLowerCase(input);


                //try
                //{
                //    //statement = JsonConvert.DeserializeObject<Statement>(input);
                //    statement = input.ToObject<Statement>()??new Statement();
                //    //statement = input.ToObject() as Statement;

                //}
                //catch (Exception ex)
                //{
                //    Febris.SharedServices.FebrisLog.Error(ex);
                //    //throw;
                //}

                if (statement != default && statement?.Id != 0)
                {
                    return (statement, true);
                }

                // SDKV-17/18 sweep: all JObject reads below go through the
                // case-insensitive GetTokenCaseInsensitive helper so BOTH the
                // lowercase Febris dialect and spec xAPI 1.0.3 casing parse.
                //Get actor
                actor = await SetupActor(GetTokenCaseInsensitive(input, "actor"));
                //Get object
                xAPIObject = await SetupObject(GetTokenCaseInsensitive(input, "object"));
                //Get verb
                verb = await SetupVerb(GetTokenCaseInsensitive(input, "verb"));
                //Get result
                result = await SetupResult(GetTokenCaseInsensitive(input, "result"));
                //Get context
                context = await SetupContext(GetTokenCaseInsensitive(input, "context"));
                //Get authority
                authority = await SetupAuthority(GetTokenCaseInsensitive(input, "authority"));
                // Version is NOT set on the tenant -- central owns the xAPI Version (the tenant is a federated client).
                //Get attachments
                attachments = await SetupAttachments(GetTokenCaseInsensitive(input, "attachments"));

                statement = new Statement()
                {
                    // FIX (LMS-B1): null-tolerant read so an omitted optional xAPI timestamp does not throw InvalidCastException. Matches the shared StatementFactor twin.
                    // Old: Timestamp = (DateTime)statementToken["timestamp"],
                    // SDKV-17/18: case-insensitive + explicit-JSON-null tolerant
                    // (the /Submit DTO bridge emits "timestamp": null when the
                    // producer omits it; the helper filters null tokens).
                    Timestamp = GetTokenCaseInsensitive(statementToken, "timestamp")?.Value<DateTime>() ?? default,
                    Actor = actor,
                    Object = xAPIObject,
                    Verb = verb,
                    Result = result,
                    Context = context,
                    Authority = authority,
                    Version = null, // set by central -- the tenant is a federated client and does not own xAPI Version
                    Attachments = attachments
                };
                return (statement, true);
            }
            catch (Exception)
            {
                return (null, false);
                throw;
            }
        }

        #region Actor
        //#########################################################################################################################
        //May need to convert specific items in string to other strings - ie model names or uuid keys
        //#########################################################################################################################
        private async Task<Actor> SetupActor(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }
                //variables
                Actor actor = new Actor();
                Member member = new Member();
                Account account = new Account();
                bool actorFound = false;

                //test if id is already in system-- this SHOULD ALWAYS be the case.
                // SDKV-17/18 sweep: reads are case-insensitive (dialect + spec casing).
                JToken idToken = GetTokenCaseInsensitive(input, "id");
                JToken uuidToken = GetTokenCaseInsensitive(input, "uuid");
                JToken mboxToken = GetTokenCaseInsensitive(input, "mbox");
                JToken mboxSha1SumToken = GetTokenCaseInsensitive(input, "mbox_sha1sum");
                if (idToken != null && (long)idToken != 0)
                {
                    actor = await _actorContext.Get((long)idToken);

                    if (actor != null)
                    {
                        actorFound = true;
                    }
                }
                if ((uuidToken != null && (string)uuidToken != Guid.Empty.ToString()) && actorFound != true)
                {
                    actor = await _actorContext.Get(Guid.Parse((string)uuidToken));

                    if (actor != null)
                    {
                        actorFound = true;
                    }
                }
                if (mboxToken != null && actorFound != true)
                {
                    actor = await _actorContext.GetByMbox(new Uri ((string)mboxToken));

                    if (actor != null)
                    {
                        actorFound = true;
                    }
                }
                if (mboxSha1SumToken != null && actorFound != true)
                {
                    actor = await _actorContext.GetByHashedMbox((string)mboxSha1SumToken);

                    if (actor != null)
                    {
                        actorFound = true;
                    }
                }
                #region cannot use for now. It would create problems
                //if (actorFound != true)
                //{
                //    //get member info
                //    member = SetupMember(input["member"]);
                //    //get account info
                //    account = SetupAccount(input["account"]);

                //    actor = new Actor()
                //    {                        
                //        ObjectType = (string)input["objectType"],
                //        Name = (string)input["name"],
                //        Mbox = (Uri)input["mbox"],
                //        Mbox_sha1sum = (string)input["mbox_sha1sum"],
                //        OpenId = (Uri)input["openId"],
                //        //Account = account,
                //        //Member =  member
                //    };
                //    if(member != null)
                //    {
                //        actor.Member = member;
                //    }
                //    if (account != null)
                //    {
                //        actor.Account = account;
                //    }                    
                //}
                #endregion
                if (!actorFound)
                {
                    return null;
                }

                return (actor);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        //get Member data    
        //#########################################################################################################################
        private async Task<Member> SetupMember(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                Member member = new Member();
                List<Actor> actorList = new List<Actor>();

                if (input["id"] != null && (long)input["id"] != 0)
                {
                    member = await _memberContext.Get((long)input["id"]);
                }
                else
                {
                    foreach (var item in input)//may need to add ["actor"]
                    {
                        Actor actor = await SetupActor(item);
                        //actorList.Append(actor);
                        actorList.Add(actor);
                    }
                    if (actorList.Count == 0)
                    {
                        return null;
                    }

                    member = new Member()
                    {
                        Actors = actorList
                    };
                }

                return (member);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        //get Account data    
        //#########################################################################################################################
        private Account SetupAccount(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                Account account = new Account()
                {
                    HomePage = (Uri)input["homepage"],
                    Name = (string)input["name"],
                };

                return (account);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }


        #endregion

        #region Object
        //#########################################################################################################################
        //May need to convert specific items in string to other strings - ie model names or uuid keys
        //#########################################################################################################################
        private async Task<Febris.ModelLibrary.Models.XApiModels.Object> SetupObject(JToken input)
        {
            try
            {
                // SDKV-17/18 sweep: a non-object token (absent / explicit JSON
                // null on the DTO bridge) must yield null like the old
                // exception-driven reads did -- NOT a transient Key-0 object.
                if (!(input is JObject))
                {
                    return null;
                }

                //get definition
                Definition definition = new Definition();
                Febris.ModelLibrary.Models.XApiModels.Object xAPIObject = new Febris.ModelLibrary.Models.XApiModels.Object();
                bool objectFound = false;

                // SDKV-17/18 sweep: case-insensitive reads (dialect emits
                // "objecttype", the DTO bridge emits "objectType", spec is
                // camelCase -- the old case-sensitive reads missed some form
                // on every route).
                JToken keyToken = GetTokenCaseInsensitive(input, "key");
                JToken uuidToken = GetTokenCaseInsensitive(input, "uuid");
                JToken idToken = GetTokenCaseInsensitive(input, "id");
                if (keyToken != null && (long)keyToken != 0) // This SHOULD ALWAYS be the case
                {
                    xAPIObject = await _objectContext.Get((long)keyToken);
                        //.Object.Include(d => d.Definition).First(k => k.Key == (long)input["key"]);
                    if (xAPIObject != null)
                    {
                        objectFound = true;
                    }
                }
                if (uuidToken != null && (string)uuidToken != Guid.Empty.ToString() && objectFound != true) // This SHOULD ALWAYS be the case
                {
                    xAPIObject = await _objectContext.Get(Guid.Parse((string)uuidToken));
                        //.Object.Include(d => d.Definition).Where(o => o.UUID == (Guid)input["uuid"]).First();
                    if (xAPIObject != null)
                    {
                        objectFound = true;
                    }
                }
                if (idToken != null && objectFound != true) // This SHOULD ALWAYS be the case
                {
                    xAPIObject = await _objectContext.Get(new Uri((string)idToken));
                        //.Object.Include(d => d.Definition).Where(o => o.Id == (Uri)input["id"]).First();
                    if (xAPIObject != null)
                    {
                        objectFound = true;
                    }
                }
                if (objectFound != true)
                {
                    definition = await SetupObjectDefinition(GetTokenCaseInsensitive(input, "definition"));

                    #region [Historical] dead object-level extensions read
                    // extensions = await SetupObjectExtensions(GetTokenCaseInsensitive(input, "extensions"));
                    //
                    // This computed a value into a local that nothing ever read. There was nowhere
                    // for it to go: Object has no Extensions property and the Object table has no
                    // such column (Key, UUID, Id, ObjectType, DefinitionId). Per xAPI 1.0.3 an
                    // Activity's extensions live INSIDE its definition, which is where
                    // SetupObjectDefinition now reads them into Definition.Extensions -- a property
                    // that does exist, with a real ExtensionsId column behind it.
                    #endregion

                    xAPIObject = new Febris.ModelLibrary.Models.XApiModels.Object()
                    {
                        Id = (Uri)idToken,
                        ObjectType = (string)GetTokenCaseInsensitive(input, "objectType"),
                        Definition = definition
                    };

                    // Persist-on-miss: the node owns its Object vocabulary,
                    // so a content-emitted activity is REGISTERED locally on first sight -- it
                    // gets a real Key for LocalStatement.ObjectId and resolves on every later
                    // read (dashboards/launch). Local-store-only; never pushed to a hub. Without
                    // this, the statement would reference Key 0 and reads would lose the Object.
                    // Requires a valid activity id per the xAPI spec; id-less payloads keep the
                    // old transient behavior.
                    if (xAPIObject.Id != null)
                    {
                        xAPIObject = await _objectContext.Create(xAPIObject);
                    }
                }

                return (xAPIObject);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }
        //#########################################################################################################################
        //Setup object definition
        //#########################################################################################################################
        /// <summary>
        /// T3: this method used to be entirely commented out and returned an EMPTY
        /// <see cref="Definition"/> for every activity. Because <c>SetupObject</c> persists a new
        /// Object on first sight, that meant a blank Definition row was written for every activity
        /// the node had never seen, and the human-readable name, description, activity type,
        /// interaction type and correct-responses pattern were all discarded on the way in.
        /// <para>
        /// The commented block could not simply be uncommented: it is stale against the current
        /// model. <c>Name</c>/<c>Description</c> are typed language maps now, not strings, and
        /// <c>CorrectResponsesPattern</c> is a <c>List&lt;string&gt;</c>. It was also written in
        /// the throw-on-missing style that was destroying whole Results elsewhere in this file, so
        /// every read below is null-tolerant instead.
        /// </para>
        /// </summary>
        private async Task<Definition> SetupObjectDefinition(JToken input)
        {
            try
            {
                // Absent definition means NO definition. Returning an empty one here is what wrote
                // a blank row per activity.
                if (!(input is JObject))
                {
                    return null;
                }

                // Spec puts an Activity's extensions inside the definition. This is the read that
                // the dead object-level local in SetupObject was standing in for.
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions =
                    await SetupObjectExtensions(GetTokenCaseInsensitive(input, "extensions"));

                Definition definition = new Definition()
                {
                    // ReadLanguageMap accepts a spec language map OR a bare string, matching the
                    // tolerance SetupVerb already applies to verb display.
                    Name = ReadLanguageMap(GetTokenCaseInsensitive(input, "name")),
                    Description = ReadLanguageMap(GetTokenCaseInsensitive(input, "description")),
                    Type = ReadUri(GetTokenCaseInsensitive(input, "type")),
                    MoreInfo = ReadUri(GetTokenCaseInsensitive(input, "moreInfo", "moreinfo")),
                    Extensions = extensions,
                    InteractionType = (string)GetTokenCaseInsensitive(input, "interactionType", "interactiontype"),
                    CorrectResponsesPattern = ReadStringList(
                        GetTokenCaseInsensitive(input, "correctResponsesPattern", "correctresponsespattern")),
                    InteractionComponents = ReadInteractionComponents(input)
                };

                return (definition);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupObjectDefinition: suppressed exception");
                return (null);
            }
        }
        //#########################################################################################################################
        //Setup object extensions
        //#########################################################################################################################
        /// <summary>
        /// T3: this was a second, entirely commented-out copy of <see cref="SetupExtensions"/> that
        /// returned an empty <see cref="Febris.ModelLibrary.Models.XApiModels.Extensions"/> for any
        /// non-null input. It now delegates to the working implementation rather than keeping two
        /// copies, one of which did nothing.
        /// </summary>
        private async Task<Febris.ModelLibrary.Models.XApiModels.Extensions> SetupObjectExtensions(JToken input)
        {
            return await SetupExtensions(input);
        }

        //#########################################################################################################################
        // Null-tolerant JToken readers (T3)
        //#########################################################################################################################
        /// <summary>
        /// A malformed URI must cost that one field, not the whole enclosing object. The
        /// <c>(Uri)token</c> cast used elsewhere throws, and the surrounding catch then discards
        /// everything parsed so far.
        /// </summary>
        private static Uri ReadUri(JToken token)
        {
            string raw = (string)token;
            if (string.IsNullOrWhiteSpace(raw)) return null;
            Uri parsed;
            return Uri.TryCreate(raw, UriKind.RelativeOrAbsolute, out parsed) ? parsed : null;
        }

        /// <summary>
        /// Reads a JSON array of strings, tolerating a single bare string. xAPI's
        /// <c>correctResponsesPattern</c> is an array, but dialect producers send one value.
        /// </summary>
        private static List<string> ReadStringList(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.Array)
            {
                List<string> values = token.Select(i => (string)i).Where(i => i != null).ToList();
                return values.Count == 0 ? null : values;
            }
            string single = (string)token;
            return string.IsNullOrEmpty(single) ? null : new List<string> { single };
        }

        /// <summary>
        /// xAPI defines five interaction component lists (choices, scale, source, target, steps)
        /// but the model and table carry a single <c>InteractionComponents</c> string, so whichever
        /// are present are preserved as JSON in the column that exists. Storing them beats the old
        /// behaviour of dropping them, and it needs no migration.
        /// </summary>
        private static string ReadInteractionComponents(JToken input)
        {
            JObject components = new JObject();
            foreach (string name in new[] { "choices", "scale", "source", "target", "steps" })
            {
                JToken value = GetTokenCaseInsensitive(input, name);
                if (value != null)
                {
                    components[name] = value;
                }
            }
            return components.Count == 0 ? null : components.ToString(Newtonsoft.Json.Formatting.None);
        }
        #endregion

        #region Verb
        //#########################################################################################################################
        //May need to convert specific items in string to other strings - ie model names or uuid keys
        //#########################################################################################################################
        private async Task<Verb> SetupVerb(JToken input)
        {
            try
            {
                // SDKV-17/18 sweep: a non-object token (absent / explicit JSON
                // null on the DTO bridge) must yield null like the old
                // exception-driven reads did -- NOT a transient empty verb.
                if (!(input is JObject))
                {
                    return null;
                }

                Verb verb = new Verb();
                bool verbFound = false;
                // SDKV-17/18 sweep: case-insensitive reads (dialect + spec casing).
                JToken keyToken = GetTokenCaseInsensitive(input, "key");
                JToken uuidToken = GetTokenCaseInsensitive(input, "uuid");
                JToken idToken = GetTokenCaseInsensitive(input, "id");
                if (keyToken != null && (long)keyToken != 0)
                {
                    verb = await _verbContext.Get((long)keyToken);// _xAPIContext.Verb.Find((long)input["key"]);
                    // FIX: was `if (verb == null)` -- inverted vs central and vs the uuid/id branches below.
                    // Verbs live in central and are required for xAPI statements: a verb found on central
                    // (verb != null) must be treated as found, not fabricated from input.
                    if (verb != null)
                    {
                        verbFound = true;
                    }
                }
                if (uuidToken != null && (string)uuidToken != Guid.Empty.ToString() && verbFound != true)
                {
                    verb = await _verbContext.Get(Guid.Parse((string)uuidToken));
                    //verb = _xAPIContext.Verb.Where(i => i.UUID == (Guid)input["uuid"]).First();
                    if (verb != null)
                    {
                        verbFound = true;
                    }
                }
                if (idToken != null && verbFound != true)
                {
                    verb = await _verbContext.Get(new Uri((string)idToken));
                    //verb = _xAPIContext.Verb.Where(i => i.Id == (Uri)input["id"]).First();
                    if (verb != null)
                    {
                        verbFound = true;
                    }
                }
                if (verbFound != true)
                {
                    // SDKV-17/18: display read is null-tolerant -- a transient
                    // verb without display used to NRE here, dropping the whole
                    // statement (silent learning-record loss).
                    #region [Historical] pre-sweep read (case-sensitive, NRE on missing display)
                    // Display = (string)input["display"].ToString()
                    #endregion
                    verb = new Verb()
                    {
                        Id = (Uri)idToken,
                        Display = ReadLanguageMap(GetTokenCaseInsensitive(input, "display"))
                    };
                }
                return (verb);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }
        #endregion

        #region Result
        //#########################################################################################################################
        // 
        //#########################################################################################################################
        private async Task<Result> SetupResult(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                //variables
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions = new Febris.ModelLibrary.Models.XApiModels.Extensions();
                Score score = new Score();

                //get score
                // SDKV-17/18 sweep: case-insensitive reads (dialect + spec casing).
                score = await SetupScore(GetTokenCaseInsensitive(input, "score"));
                //get extensions
                extensions = await SetupExtensions(GetTokenCaseInsensitive(input, "extensions"));

                TimeSpan formattedDuration = TimeSpan.Zero;
                try
                {
                    formattedDuration = XmlConvert.ToTimeSpan(GetTokenCaseInsensitive(input, "duration")?.Value<string>());
                }
                catch (System.Exception ex)
                { Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupResult: suppressed exception"); }

                Result result = new Result()
                {
                    Score = score,
                    // (bool?) NOT (bool). The previous casts were documented as deliberately
                    // preserving "throw-on-missing" -- and that throw was caught by the outer
                    // handler, which returned null for the WHOLE Result. success and completion
                    // are OPTIONAL in xAPI 1.0.3, so a perfectly valid statement carrying only
                    // a score silently lost its score, duration, response and extensions, while
                    // the node answered the client 200 {"success":true}. Measured against a
                    // running node: a score of 87 stored as ResultId = NULL.
                    //
                    // The nullable cast keeps the dialect coercion the comment was really
                    // about (Json.NET converts "true"/"false" strings) and returns null for an
                    // absent token instead of throwing. Absent now stays absent rather than
                    // being recorded as false, which would assert the learner did not succeed.
                    Success = (bool?)GetTokenCaseInsensitive(input, "success"),
                    Completion = (bool?)GetTokenCaseInsensitive(input, "completion"),
                    Response = (string)GetTokenCaseInsensitive(input, "response"),
                    //Duration = (TimeSpan?)input["duration"]??TimeSpan.Zero,
                    //Duration = TimeSpan.TryParseExact(input["duration"],IsoDateTimeConverter),
                    Duration = formattedDuration,
                    Extensions = extensions
                };
                return (result);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        // 
        //#########################################################################################################################
        private async Task<Score> SetupScore(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                // SDKV-17/18 sweep: case-insensitive reads (dialect + spec casing).
                Score score = new Score()
                {
                    Scaled = (float?)GetTokenCaseInsensitive(input, "scaled") ?? 0f,
                    Raw = (float?)GetTokenCaseInsensitive(input, "raw") ?? 0f,
                    Min = (float?)GetTokenCaseInsensitive(input, "min") ?? 0f,
                    Max = (float?)GetTokenCaseInsensitive(input, "max") ?? 0f
                };

                return (score);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }
        //#########################################################################################################################
        // 
        //#########################################################################################################################
        private async Task<Febris.ModelLibrary.Models.XApiModels.Extensions> SetupExtensions(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                Febris.ModelLibrary.Models.XApiModels.Extensions extensions = new Febris.ModelLibrary.Models.XApiModels.Extensions();

                //if(input.Contains("id")&& (long)input["id"] != 0)

                if (input.Contains("id") && (long)input["id"] != 0) // This SHOULD ALWAYS be the case
                {
                    extensions = await _extensionContext.Get((long)input["id"]);//.Extensions.Find((long)input["id"]);
                }
                else
                {
                    // SDKV-17/18 sweep: case-insensitive read (dialect + spec casing).
                    if ((string)GetTokenCaseInsensitive(input, "extensionmap") != null)
                    {
                        extensions = new Febris.ModelLibrary.Models.XApiModels.Extensions()
                        {
                            ExtensionMap = (string)GetTokenCaseInsensitive(input, "extensionmap")
                        };
                    }
                    else
                    {
                        // T3: a spec producer sends extensions as an IRI-keyed object
                        // ({"http://example.com/ext": "42"}), not as the Febris dialect
                        // "extensionmap" string, so this returned null and the extensions were
                        // dropped. Bridge the spec shape into the dialect string the whole
                        // downstream pipeline already reads.
                        string bridged = BuildExtensionMap(input);
                        if (bridged == null)
                        {
                            return null;
                        }
                        extensions = new Febris.ModelLibrary.Models.XApiModels.Extensions()
                        {
                            ExtensionMap = bridged
                        };
                    }
                }
                return (extensions);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        // Spec extensions -> Febris ExtensionMap dialect (T3)
        //#########################################################################################################################
        /// <summary>
        /// Folds an IRI-keyed xAPI extensions object into the comma-delimited
        /// <c>iri:value</c> string that <c>ExtensionMapParsing.TryParseExtensionEntry</c>,
        /// <c>FactorResultExtensionExtras</c> and the shared <c>StatementFactory</c> all read.
        /// <para>
        /// That format is lossy by construction: entries are split on commas and the value is the
        /// THIRD colon-separated part, so a value containing a comma would be read back as two
        /// entries and a value containing a colon would be read back truncated. Rather than store
        /// something that reads back as different data than the producer sent -- the exact failure
        /// class as the rest of T3 -- each candidate entry is round-tripped through the real
        /// reader and only kept if the value survives intact. Anything that would not is logged
        /// and skipped. Widening the column to a real map is the alternative and was deliberately
        /// not taken, see docs/BUGS.md.
        /// </para>
        /// </summary>
        private static string BuildExtensionMap(JToken input)
        {
            JObject source = input as JObject;
            if (source == null) return null;

            List<string> entries = new List<string>();
            foreach (JProperty property in source.Properties())
            {
                // The dialect keys are handled by the caller and are not extension IRIs.
                if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.Equals(property.Name, "extensionmap", StringComparison.OrdinalIgnoreCase)) continue;
                if (property.Value == null || property.Value.Type == JTokenType.Null) continue;

                string value = property.Value.Type == JTokenType.Object || property.Value.Type == JTokenType.Array
                    ? property.Value.ToString(Newtonsoft.Json.Formatting.None)
                    : (string)property.Value;

                string entry = TryBuildExtensionEntry(property.Name, value);
                if (entry == null)
                {
                    Febris.SharedServices.FebrisLog.ErrorMessage(
                        "StatementFactor.BuildExtensionMap: extension '" + property.Name +
                        "' skipped because its value cannot round-trip through the comma/colon delimited ExtensionMap format.");
                    continue;
                }
                entries.Add(entry);
            }

            return entries.Count == 0 ? null : string.Join(",", entries);
        }

        /// <summary>
        /// Builds one <c>iri:value</c> entry and proves it reads back unchanged through the actual
        /// parser, returning null when it would not.
        /// </summary>
        private static string TryBuildExtensionEntry(string iri, string value)
        {
            if (string.IsNullOrEmpty(iri) || value == null) return null;
            if (iri.Contains(",") || value.Contains(",")) return null;

            string entry = iri + ":" + value;

            Febris.EnumLibrary.ExtensionIRIOptions parsedIri;
            string parsedValue;
            if (!Febris.EnumLibrary.ExtensionMapParsing.TryParseExtensionEntry(entry, out parsedIri, out parsedValue))
            {
                return null;
            }
            return parsedValue == value ? entry : null;
        }


        #endregion

        #region Context
        //#########################################################################################################################
        // Context setup
        //#########################################################################################################################
        private async Task<Context> SetupContext(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                Actor instructor = new Actor();
                List<Actor> group = new List<Actor>();
                ContextActivities contextActivities = new ContextActivities();
                StatementReference statementReference = new StatementReference();
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions = new Febris.ModelLibrary.Models.XApiModels.Extensions();

                //get instructor
                // SDKV-17/18 sweep: case-insensitive reads (dialect + spec casing).
                instructor = await SetupActor(GetTokenCaseInsensitive(input, "instructor"));
                //get group
                group = await SetupActorGroup(GetTokenCaseInsensitive(input, "group"));
                //get context Activites
                // Bug fix (matches shared StatementFactor): previously read
                // `input["contextactivites"]` -- typo, missing 'i' before
                // 'es'. ChangePropertiesToLowerCase lowercases but doesn't
                // rename; correctly-spelled producers (including the
                // simulation integration library) emit `contextActivities`,
                // which lowercases to `contextactivities`. Silent data
                // loss before this fix.
                // SDKV-18: the /Submit DTO bridge emits the camelCase
                // "contextActivities" (case-insensitive match) OR the
                // deliberately-preserved dialect typo alias "contextactivites"
                // (second lookup name) -- both must keep parsing.
                contextActivities = await SetupContextActivites(GetTokenCaseInsensitive(input, "contextactivities", "contextactivites"));
                //get statement reference
                statementReference = await SetupStatementReference(GetTokenCaseInsensitive(input, "statementreference"));
                //get extensions
                extensions = await SetupExtensions(GetTokenCaseInsensitive(input, "extensions"));

                Context context = new Context();

                // T3: these four assignments were commented out, so registration, revision,
                // platform and language were never stored -- even though the model carries all
                // four and the Context table already has all four columns. The dead-context
                // guard below reads every one of them, which is why this looked like working
                // code: Registration could only ever be Guid.Empty and the other three could
                // only ever be null, so the guard was testing constants.
                //
                // Deliberately NOT restored as the commented `(Guid)input["registration"]`.
                // That cast throws on a malformed value, and the outer catch returns null for
                // the WHOLE Context -- the same shape as the optional success/completion cast
                // that was destroying whole Results. TryParse degrades to "no registration"
                // and keeps the rest of the context.
                Guid registration;
                if (Guid.TryParse((string)GetTokenCaseInsensitive(input, "registration"), out registration))
                {
                    context.Registration = registration;
                }
                context.Revision = (string)GetTokenCaseInsensitive(input, "revision");
                context.Platform = (string)GetTokenCaseInsensitive(input, "platform");
                context.Language = (string)GetTokenCaseInsensitive(input, "language");

                if (group != null)
                {
                    context.Group = group;
                }
                if (instructor != null)
                {
                    context.Instructor = instructor;
                }
                if (contextActivities != null)
                {
                    context.ContextActivities = contextActivities;
                }
                if (extensions != null)
                {
                    context.Extensions = extensions;
                }
                if (statementReference != null)
                {
                    context.StatementReference = statementReference;
                }

                if (context.ContextActivities == null && context.Registration == Guid.Empty && context.Revision == null && context.StatementReference == null && context.Extensions == null
                    && context.ContextActivities == null && context.Instructor == null && context.Group == null && context.Platform == null && context.Language == null)
                {
                    return null;
                }

                return (context);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        //get group      
        //#########################################################################################################################
        private async Task<List<Actor>> SetupActorGroup(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                List<Actor> actorList = new List<Actor>();
                foreach (var item in input)//may need to add ["actor"]
                {
                    Actor actor = await SetupActor(item);
                    //actorList.Append(actor);
                    actorList.Add(actor);
                }

                if (actorList.Count == 0)
                {
                    return null;
                }

                return (actorList);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        //#########################################################################################################################
        //get context Activites        
        //#########################################################################################################################
        private async Task<ContextActivities> SetupContextActivites(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                // SDKV-17/18 sweep: case-insensitive, absence-tolerant reads.
                // The old code indexed all four keys case-sensitively and
                // dereferenced .Type unconditionally -- any MISSING key (vs
                // present-with-null) threw NRE and the catch silently dropped
                // ALL context activities. Dialect producers emit all four keys;
                // spec producers may send any subset.
                #region [Historical] pre-sweep reads (case-sensitive, NRE when a key was absent)
                //if (input["parent"].Type == JTokenType.Null && input["grouping"].Type == JTokenType.Null && input["category"].Type == JTokenType.Null && input["other"].Type == JTokenType.Null)
                //{
                //    return null;
                //}

                //ContextActivities contextActivities = new ContextActivities()
                //{
                //    Parent = (string)input["parent"],
                //    Grouping = (string)input["grouping"],
                //    Category = (string)input["category"],
                //    Other = (string)input["other"],
                //};
                #endregion
                string parent = ReadContextActivityValue(GetTokenCaseInsensitive(input, "parent"));
                string grouping = ReadContextActivityValue(GetTokenCaseInsensitive(input, "grouping"));
                string category = ReadContextActivityValue(GetTokenCaseInsensitive(input, "category"));
                string other = ReadContextActivityValue(GetTokenCaseInsensitive(input, "other"));
                if (parent == null && grouping == null && category == null && other == null)
                {
                    return null;
                }

                ContextActivities contextActivities = new ContextActivities()
                {
                    Parent = parent,
                    Grouping = grouping,
                    Category = category,
                    Other = other,
                };

                return (contextActivities);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }
        //#########################################################################################################################
        //context-activity slot value (string IRI vs activity object/array)
        //#########################################################################################################################
        /// <summary>
        /// Shape-tolerant read of one context-activity slot into the domain
        /// model's string column.
        /// <list type="bullet">
        ///   <item>dialect IRI string -- stored as-is (historical behavior);</item>
        ///   <item>spec single Activity object -- its <c>id</c> IRI;</item>
        ///   <item>one-element array (what the /Submit DTO bridge emits after
        ///     the tolerant DTO bind) -- the element's <c>id</c> IRI, i.e. the
        ///     same value the dialect wire carried;</item>
        ///   <item>multi-element array -- compact JSON (best-effort: the domain
        ///     column is a single string; raw bytes preserve the original);</item>
        ///   <item>null / empty array -- null (absent).</item>
        /// </list>
        /// </summary>
        private static string ReadContextActivityValue(JToken token)
        {
            if (token == null || token.Type == JTokenType.Null)
            {
                return null;
            }
            if (token.Type == JTokenType.String)
            {
                return (string)token;
            }
            if (token.Type == JTokenType.Array)
            {
                JArray array = (JArray)token;
                if (array.Count == 0)
                {
                    return null;
                }
                if (array.Count == 1)
                {
                    JToken id = GetTokenCaseInsensitive(array[0], "id");
                    if (id != null)
                    {
                        return (string)id;
                    }
                }
                return token.ToString(Newtonsoft.Json.Formatting.None);
            }
            if (token.Type == JTokenType.Object)
            {
                JToken id = GetTokenCaseInsensitive(token, "id");
                if (id != null)
                {
                    return (string)id;
                }
                return token.ToString(Newtonsoft.Json.Formatting.None);
            }
            return token.ToString();
        }

        //#########################################################################################################################
        //get statement reference
        //#########################################################################################################################
        private async Task<StatementReference> SetupStatementReference(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }
                //if (!input.Contains("objecttype") && !input.Contains("id"))
                //{
                //    return null;
                //}

                // SDKV-17/18 sweep: case-insensitive reads (dialect "objecttype",
                // DTO bridge / spec "objectType").
                StatementReference statementReference = new StatementReference()
                {
                    ObjectType = (string)GetTokenCaseInsensitive(input, "objecttype"),
                    Id = (Guid)GetTokenCaseInsensitive(input, "id")
                };
                return (statementReference);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }
        //#########################################################################################################################        
        //get extensions
        //#########################################################################################################################

        #endregion

        //There has to be more to do here
        #region Authority
        //#########################################################################################################################
        // This is suppose to interact with OAuth but an actor seems to be all it needs. 
        //#########################################################################################################################
        /// <summary>
        /// T2. A client-supplied <c>authority</c> is REFUSED, always.
        ///
        /// <para>
        /// In xAPI the actor is who DID the activity and the authority is who ASSERTS the statement
        /// is true. Letting the submitter choose the authority lets it claim someone else vouched
        /// for the record. This method used to do exactly that: any authority object carrying an
        /// <c>id</c> or <c>uuid</c> was handed whole to <c>SetupActor</c>, which resolves an
        /// existing Actor by id, uuid, mbox OR mbox_sha1sum. So a caller holding any device token
        /// could attach an authority naming a real instructor or administrator provisioned on the
        /// node, and the statement would render and export as though that person had vouched for it.
        /// </para>
        ///
        /// <para>
        /// <b>Storing nothing is strictly better than storing a lie.</b> An absent authority is an
        /// honest "this node does not record who vouched for this". A client-chosen one is a false
        /// attribution that is indistinguishable from a real one afterwards.
        /// </para>
        ///
        /// <para>
        /// This is HALF the answer. The correct end state is for the LRS to STAMP an authority from
        /// the submitting credentials, which needs the authenticated device identity available in
        /// this layer. That plumbing was removed from the statement write path and re-adding it is
        /// the same work as binding the actor to the caller, which is a product decision about how
        /// shared classroom devices are used. Recorded in docs/BUGS.md rather than guessed at here.
        /// </para>
        /// </summary>
        private async Task<Authority> SetupAuthority(JToken input)
        {
            if (input != null)
            {
                Febris.SharedServices.FebrisLog.Warn("Statement ingest: a client-supplied 'authority' was discarded. " +
                    "The submitter does not get to choose who vouched for a statement.");
            }

            return await StampAuthorityFromCredential();
        }

        /// <summary>
        /// The xAPI account IRI namespace for this node's own devices. An Agent needs exactly one
        /// Inverse Functional Identifier, and <c>account</c> is the right one for a machine: a
        /// device has no mailbox.
        /// </summary>
        private const string DeviceAuthorityHomePage = "https://febr.is/node/device";

        /// <summary>
        /// Builds the authority from the CREDENTIAL that submitted the statement, which is what an
        /// LRS is supposed to do.
        ///
        /// <para>
        /// The device is deliberately the AUTHORITY and not the actor. In xAPI the actor is who
        /// performed the activity and the authority is who asserts it happened, so a shared
        /// classroom device submitting for thirty learners in sequence is completely ordinary: one
        /// authority, many actors. Trying to make the device the actor is what would break shared
        /// devices, educator-on-behalf submission, offline batch upload and the launcher.
        /// </para>
        ///
        /// <para>
        /// The device Agent's UUID is the hardware UUID itself. That makes the row deterministic and
        /// self-deduplicating without adding a lookup-by-account method to the DAL: one row per
        /// device, created on its first statement and reused forever. Hardware UUIDs are unique and
        /// learner Actor UUIDs are database-generated, so the two cannot collide.
        /// </para>
        ///
        /// <para>
        /// Returns null when there is no device credential, which is correct rather than a failure:
        /// a Portal-originated statement, a seed or an import genuinely has no device vouching for
        /// it, and an absent authority says so honestly.
        /// </para>
        /// </summary>
        private async Task<Authority> StampAuthorityFromCredential()
        {
            try
            {
                object item = _httpContextAccessor?.HttpContext?.Items != null
                    && _httpContextAccessor.HttpContext.Items.ContainsKey("Hardware")
                        ? _httpContextAccessor.HttpContext.Items["Hardware"]
                        : null;

                Febris.ModelLibrary.Models.DataModels.Hardware hardware =
                    item as Febris.ModelLibrary.Models.DataModels.Hardware;

                if (hardware == null || hardware.UUID == Guid.Empty)
                {
                    return null;
                }

                Actor deviceActor = await _actorContext.Get(hardware.UUID);
                if (deviceActor == null)
                {
                    deviceActor = await _actorContext.Create(new Actor
                    {
                        UUID = hardware.UUID,
                        ObjectType = "Agent",
                        Account = new Account
                        {
                            HomePage = new Uri(DeviceAuthorityHomePage),
                            Name = hardware.UUID.ToString()
                        }
                    });
                }

                if (deviceActor == null)
                {
                    // Never fail an ingest because the authority could not be minted. A statement
                    // with no authority is worse than one with, and far better than a lost record.
                    Febris.SharedServices.FebrisLog.Warn(
                        "Statement ingest: could not resolve a device authority for hardware " + hardware.UUID + ".");
                    return null;
                }

                return new Authority { Actor = deviceActor };
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.StampAuthorityFromCredential");
                return null;
            }
        }
        #endregion

        #region Version
        //#########################################################################################################################
        // Should be able to pull the version directly from db. They should not be able to really set the version
        //#########################################################################################################################
        // SetupVersion removed: the tenant does NOT set xAPI Version. Version is owned by central (the tenant is a
        // federated client); the terminal Statement leaves Version null and central sets it.
        #endregion

        #region Attachments
        //#########################################################################################################################
        // This one may be a little tricky - this is also where video should be input I think
        //#########################################################################################################################
        private async Task<List<Febris.ModelLibrary.Models.XApiModels.Attachment>> SetupAttachments(JToken input)
        {
            try
            {
                //check if token is null
                if (input == null)
                {
                    return null;
                }

                List<Febris.ModelLibrary.Models.XApiModels.Attachment> attachments = new List<Febris.ModelLibrary.Models.XApiModels.Attachment>();

                //create list of attachments then go through and deserialize each one independently in a foreach and add it to the array. 
                foreach (var item in input)
                {
                    Febris.ModelLibrary.Models.XApiModels.Attachment attachment = new Febris.ModelLibrary.Models.XApiModels.Attachment();
                    //{
                    //    UsageType = (Uri)item["usageType"],
                    //    Display = (string)item["display"],
                    //    Description = (string)item["description"],
                    //    ContentType = (string)item["contentType"],
                    //    Length = (int?)item["length"]??0,
                    //    Sha2 = (string)item["sha2"],
                    //    FileURL = (Uri)item["fileUrl"]
                    //};
                    //JsonStringDictionaryBuilder stringBuilder = new JsonStringDictionaryBuilder();
                    //string display = stringBuilder.ConvertStringToJsonStringArrayString((string)item["display"].ToString());
                    //string description = stringBuilder.ConvertStringToJsonStringArrayString((string)item["description"].ToString());
                    //string contenttype = stringBuilder.ConvertStringToJsonStringArrayString((string)item["contenttype"].ToString());                    
                    //string display = stringBuilder.ConvertStringToJsonStringArrayString((string)item["display"]);

                    // SDKV-17: the /Submit DTO bridge emits the DTO's camelCase
                    // keys (usageType/contentType/fileUrl) while this factor read
                    // lowercase dialect keys with the case-SENSITIVE indexer --
                    // item["contenttype"] resolved null, .ToString() threw NRE,
                    // and the catch silently dropped EVERY attachment. Reads are
                    // now case-insensitive AND null-tolerant (a producer omitting
                    // one optional field no longer loses the whole attachment list).
                    #region [Historical] pre-sweep reads (case-sensitive, NRE on missing keys)
                    //attachment.UsageType = (Uri)item["usagetype"];
                    ////attachment.Display = (string)item["display"];//this is the problem
                    //attachment.Display = (string)item["display"].ToString().Replace("\r\n", string.Empty).Replace(@"\", string.Empty);
                    //attachment.Description = (string)item["description"].ToString().Replace("\r\n", string.Empty).Replace(@"\", string.Empty);
                    ////attachment.Display = display;
                    ////attachment.Description = description;
                    //attachment.ContentType = (string)item["contenttype"].ToString();
                    //attachment.Length = (int?)item["length"] ?? 0;
                    //attachment.Sha2 = (string)item["sha2"].ToString();
                    //attachment.FileURL = (Uri)item["fileurl"];
                    #endregion
                    attachment.UsageType = (Uri)GetTokenCaseInsensitive(item, "usagetype");
                    attachment.Display = ReadLanguageMap(GetTokenCaseInsensitive(item, "display"));
                    attachment.Description = ReadLanguageMap(GetTokenCaseInsensitive(item, "description"));
                    attachment.ContentType = GetTokenCaseInsensitive(item, "contenttype")?.ToString();
                    attachment.Length = (int?)GetTokenCaseInsensitive(item, "length") ?? 0;
                    attachment.Sha2 = GetTokenCaseInsensitive(item, "sha2")?.ToString();
                    attachment.FileURL = (Uri)GetTokenCaseInsensitive(item, "fileurl");

                    //attachments.Append(attachment);
                    attachments.Add(attachment);
                }

                return (attachments);
            }
            catch (Exception)
            {
                return (null);
                throw;
            }
        }

        #endregion

        #region extras
        //#########################################################################################################################        
        // convert jobject keys to lowercase (cant do this with everything though)
        //#########################################################################################################################
        public static async Task<XApiResultExtras> FactorResultExtensionExtras(Result result)
        {
            try
            {
                XApiResultExtras extra = new XApiResultExtras();
                string[] extensionMapArray = result.Extensions.ExtensionMap.Split(',');
                string[] notes = { };
                List<string> noteList = new List<string>();
                for (var i = 0; i < extensionMapArray.Length; i++)
                {
                    string[] extensionSingle = extensionMapArray[i].Split(':');
                    string key = extensionSingle[0] + ":" + extensionSingle[1];
                    //ExtensionIRIOptions iri = ExtensionIRIResolver.GetVerbEnum(key);
                    //switch (iri)
                    //{
                    //    case ExtensionIRIOptions.RestartCounterIRI:
                    //        extra.RestartCount = Int32.Parse(extensionSingle[2]);
                    //        break;
                    //    case ExtensionIRIOptions.NotesIRI:
                    //        notes = extensionSingle[2].Split('|');
                    //        for (var j = 0; j < notes.Length; j++)
                    //        {
                    //            if (notes[j] != "|")
                    //            {
                    //                string tempNote = notes[j];
                    //                noteList.Add(tempNote);

                    //            }
                    //        }
                    //        extra.NotesList = noteList;
                    //        break;
                    //}
                }
                extra.Result = result;
                extra.ResultUUID = result.UUID;

                return extra;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.FactorResultExtensionExtras: suppressed exception");

            }
            return null;
        }

        #endregion

        ////#########################################################################################################################
        //// convert jobject keys to lowercase (cant do this with everything though)
        ////#########################################################################################################################
        //public static void ChangePropertiesToLowerCase(JObject jsonObject)
        //{
        //    foreach (var property in jsonObject.Properties().ToList())
        //    {
        //        if (property.Value.Type == JTokenType.Object)// replace property names in child object
        //            ChangePropertiesToLowerCase((JObject)property.Value);

        //        property.Replace(new JProperty(property.Name.ToLower(), property.Value));// properties are read-only, so we have to replace them
        //    }
        //}

        #region Phase 3.3c-deep: Typed twins (XApiStatementDto -> Statement)
        // =====================================================================
        // EndUser BLL twin of the shared StatementFactor typed factor.
        // Identical implementation; the namespace differs and the EndUser
        // file's JObject FactorStatement has ChangePropertiesToLowerCase
        // commented out (line 97), so the typed twin's behavior MAY DIVERGE
        // from the JObject twin's behavior for case-mixed producer payloads.
        // Documented; equivalence tests in this project should cover both
        // lowercase-input and mixed-case-input payloads.
        // =====================================================================

        public async Task<(Statement Statement, bool ParsedCorrectly)> FactorStatementFromDto(XApiStatementDto input)
        {
            if (input == null) return (null, false);
            try
            {
                Actor actor = await SetupActorFromDto(input.Actor);
                Febris.ModelLibrary.Models.XApiModels.Object xAPIObject = await SetupObjectFromDto(input.Object);
                Verb verb = await SetupVerbFromDto(input.Verb);
                Result result = await SetupResultFromDto(input.Result);
                Context context = await SetupContextFromDto(input.Context);
                Authority authority = await SetupAuthorityFromDto(input.Authority);
                // Version is NOT set on the tenant -- central owns the xAPI Version (the tenant is a federated client).
                List<Febris.ModelLibrary.Models.XApiModels.Attachment> attachments = await SetupAttachmentsFromDto(input.Attachments);

                Statement statement = new Statement()
                {
                    Timestamp = input.Timestamp ?? default,
                    Actor = actor,
                    Object = xAPIObject,
                    Verb = verb,
                    Result = result,
                    Context = context,
                    Authority = authority,
                    Version = null, // set by central -- the tenant is a federated client and does not own xAPI Version
                    Attachments = attachments
                };
                return (statement, true);
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.FactorStatementFromDto: suppressed exception");
                return (null, false);
            }
        }

        private async Task<Actor> LookupActorByHints(long? id, Guid? uuid, string mbox, string mboxSha1Sum)
        {
            bool actorFound = false;
            Actor actor = null;

            if (id.HasValue && id.Value != 0)
            {
                actor = await _actorContext.Get(id.Value);
                if (actor != null) actorFound = true;
            }
            if (uuid.HasValue && uuid.Value != Guid.Empty && !actorFound)
            {
                actor = await _actorContext.Get(uuid.Value);
                if (actor != null) actorFound = true;
            }
            if (!string.IsNullOrEmpty(mbox) && !actorFound)
            {
                actor = await _actorContext.GetByMbox(new Uri(mbox));
                if (actor != null) actorFound = true;
            }
            if (!string.IsNullOrEmpty(mboxSha1Sum) && !actorFound)
            {
                actor = await _actorContext.GetByHashedMbox(mboxSha1Sum);
                if (actor != null) actorFound = true;
            }

            return actorFound ? actor : null;
        }

        private async Task<Actor> SetupActorFromDto(XApiActorDto input)
        {
            if (input == null) return null;
            try
            {
                return await LookupActorByHints(input.Id, input.UUID, input.Mbox, input.MboxSha1Sum);
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupActorFromDto: suppressed exception");
                return null;
            }
        }

        /// <summary>
        /// T2. The typed twin of <see cref="SetupAuthority"/>, and it refuses a client-supplied
        /// authority for the same reason: the submitter does not get to choose who vouched for a
        /// statement. It previously resolved a real Actor from client-supplied hints, so this path
        /// carried the identical impersonation route.
        ///
        /// <para>
        /// Both paths are changed together deliberately. <c>XApiFactorEquivalenceTests</c> asserts
        /// the typed and legacy factors assemble the same statement, so changing one alone would
        /// have broken that equivalence, and a divergence between the two ingest shapes is precisely
        /// how one of them would quietly keep the hole.
        /// </para>
        /// </summary>
        private async Task<Authority> SetupAuthorityFromDto(XApiAuthorityDto input)
        {
            if (input != null)
            {
                Febris.SharedServices.FebrisLog.Warn("Statement ingest (typed): a client-supplied 'authority' was discarded.");
            }

            return await StampAuthorityFromCredential();
        }

        private async Task<Febris.ModelLibrary.Models.XApiModels.Object> SetupObjectFromDto(XApiObjectDto input)
        {
            if (input == null) return null;
            try
            {
                bool objectFound = false;
                Febris.ModelLibrary.Models.XApiModels.Object xAPIObject = null;

                if (input.Key.HasValue && input.Key.Value != 0)
                {
                    xAPIObject = await _objectContext.Get(input.Key.Value);
                    if (xAPIObject != null) objectFound = true;
                }
                if (!objectFound && input.UUID.HasValue && input.UUID.Value != Guid.Empty)
                {
                    xAPIObject = await _objectContext.Get(input.UUID.Value);
                    if (xAPIObject != null) objectFound = true;
                }
                if (!objectFound && !string.IsNullOrEmpty(input.Id))
                {
                    Uri parsedUri;
                    if (Uri.TryCreate(input.Id, UriKind.Absolute, out parsedUri))
                    {
                        xAPIObject = await _objectContext.Get(parsedUri);
                        if (xAPIObject != null) objectFound = true;
                    }
                }

                if (!objectFound)
                {
                    Definition definition = await SetupObjectDefinitionFromDto(input.Definition);
                    Uri objIdUri = null;
                    if (!string.IsNullOrEmpty(input.Id) && Uri.TryCreate(input.Id, UriKind.Absolute, out objIdUri)) { /* parsed */ }
                    xAPIObject = new Febris.ModelLibrary.Models.XApiModels.Object
                    {
                        Id = objIdUri,
                        ObjectType = input.ObjectType,
                        Definition = definition
                    };

                    // Persist-on-miss: same as the JObject path -- register
                    // the unseen activity in the node's local Object store so it gets a real Key
                    // and resolves on later reads. Local-store-only; never pushed to a hub.
                    if (xAPIObject.Id != null)
                    {
                        xAPIObject = await _objectContext.Create(xAPIObject);
                    }
                }

                return xAPIObject;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupObjectFromDto: suppressed exception");
                return null;
            }
        }

        /// <summary>
        /// T3: this was a literal stub. It ignored its argument and returned an empty
        /// <see cref="Definition"/>, so the typed route discarded every activity definition just
        /// as the JObject route did -- and, because SetupObject persists on first sight, wrote a
        /// blank Definition row for each new activity. The DTO had parsed all of it correctly.
        /// </summary>
        private async Task<Definition> SetupObjectDefinitionFromDto(XApiActivityDefinitionDto input)
        {
            if (input == null) return null;
            try
            {
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions =
                    await SetupExtensionsFromDto(input.Extensions);

                return new Definition
                {
                    Name = input.Name == null ? null : new Dictionary<string, string>(input.Name),
                    Description = input.Description == null ? null : new Dictionary<string, string>(input.Description),
                    Type = ReadUri(input.Type),
                    MoreInfo = ReadUri(input.MoreInfo),
                    Extensions = extensions,
                    InteractionType = input.InteractionType,
                    CorrectResponsesPattern = input.CorrectResponsesPattern == null || input.CorrectResponsesPattern.Count == 0
                        ? null
                        : input.CorrectResponsesPattern,
                    InteractionComponents = ReadInteractionComponentsFromDto(input)
                };
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupObjectDefinitionFromDto: suppressed exception");
                return null;
            }
        }

        /// <summary>
        /// String overload of <see cref="ReadUri(JToken)"/> for the typed path, with the same
        /// rule: a malformed URI costs that field alone.
        /// </summary>
        private static Uri ReadUri(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            Uri parsed;
            return Uri.TryCreate(raw, UriKind.RelativeOrAbsolute, out parsed) ? parsed : null;
        }

        /// <summary>
        /// Typed twin of <see cref="ReadInteractionComponents"/>: the five spec component lists
        /// folded into the single InteractionComponents column that exists.
        /// </summary>
        private static string ReadInteractionComponentsFromDto(XApiActivityDefinitionDto input)
        {
            JObject components = new JObject();
            AddComponentList(components, "choices", input.Choices);
            AddComponentList(components, "scale", input.Scale);
            AddComponentList(components, "source", input.Source);
            AddComponentList(components, "target", input.Target);
            AddComponentList(components, "steps", input.Steps);
            return components.Count == 0 ? null : components.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static void AddComponentList(JObject target, string name, List<XApiInteractionComponentDto> values)
        {
            if (values == null || values.Count == 0) return;
            target[name] = JArray.FromObject(values);
        }

        private async Task<Verb> SetupVerbFromDto(XApiVerbDto input)
        {
            if (input == null) return null;
            try
            {
                bool verbFound = false;
                Verb verb = null;

                if (input.Key.HasValue && input.Key.Value != 0)
                {
                    // EndUser IVerbQueries has Get(long?), pass the nullable directly.
                    verb = await _verbContext.Get(input.Key);
                    if (verb != null) verbFound = true;
                }
                if (!verbFound && input.UUID.HasValue && input.UUID.Value != Guid.Empty)
                {
                    // EndUser IVerbQueries has Get(Guid) NOT Get(Guid?) -- unwrap.
                    // (Shared IVerbQueries has Get(Guid?); the DAL surfaces
                    // diverge between the two BLLs. Documented in 3.3c-deep.)
                    verb = await _verbContext.Get(input.UUID.Value);
                    if (verb != null) verbFound = true;
                }
                if (!verbFound && !string.IsNullOrEmpty(input.Id))
                {
                    Uri parsedUri;
                    if (Uri.TryCreate(input.Id, UriKind.Absolute, out parsedUri))
                    {
                        verb = await _verbContext.Get(parsedUri);
                        if (verb != null) verbFound = true;
                    }
                }

                if (!verbFound)
                {
                    Uri verbIdUri = null;
                    Uri.TryCreate(input.Id, UriKind.Absolute, out verbIdUri);
                    verb = new Verb
                    {
                        Id = verbIdUri,
                        // Direct typed mapping: the DTO already carries an IDictionary language map.
                        Display = input.Display == null ? null : new Dictionary<string, string>(input.Display)
                    };
                }

                return verb;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupVerbFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<Result> SetupResultFromDto(XApiResultDto input)
        {
            if (input == null) return null;
            try
            {
                Score score = await SetupScoreFromDto(input.Score);
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions =
                    await SetupExtensionsFromDto(input.Extensions);

                TimeSpan formattedDuration = TimeSpan.Zero;
                if (!string.IsNullOrEmpty(input.Duration))
                {
                    try { formattedDuration = XmlConvert.ToTimeSpan(input.Duration); }
                    catch (System.Exception ex) { Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupResultFromDto: suppressed exception"); /* mirror */ }
                }

                return new Result
                {
                    Score = score,
                    // The typed twin used to coerce absent to false. It did not destroy the
                    // Result like the JObject path above, but it fabricated an assertion the
                    // producer never made. Both paths now carry absence through unchanged.
                    Success = input.Success,
                    Completion = input.Completion,
                    Response = input.Response,
                    Duration = formattedDuration,
                    Extensions = extensions
                };
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupResultFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<Score> SetupScoreFromDto(XApiScoreDto input)
        {
            await Task.CompletedTask;
            if (input == null) return null;
            try
            {
                return new Score
                {
                    Scaled = input.Scaled.HasValue ? (float)input.Scaled.Value : 0f,
                    Raw = input.Raw.HasValue ? (float)input.Raw.Value : 0f,
                    Min = input.Min.HasValue ? (float)input.Min.Value : 0f,
                    Max = input.Max.HasValue ? (float)input.Max.Value : 0f
                };
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupScoreFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<Febris.ModelLibrary.Models.XApiModels.Extensions> SetupExtensionsFromDto(IDictionary<string, JToken> input)
        {
            if (input == null) return null;
            try
            {
                if (input.TryGetValue("id", out JToken idToken) && idToken != null)
                {
                    long? idVal = idToken.Type == JTokenType.Integer ? (long?)idToken : null;
                    if (idVal.HasValue && idVal.Value != 0)
                    {
                        // _extensionContext is now initialized in the ctor
                        // (previously a latent NRE bug -- fixed in the same
                        // round as Phase 3.3c-deep cleanup). Safe to call.
                        Febris.ModelLibrary.Models.XApiModels.Extensions existing =
                            await _extensionContext.Get(idVal.Value);
                        if (existing != null) return existing;
                    }
                }

                if (input.TryGetValue("extensionmap", out JToken mapToken) && mapToken != null && mapToken.Type != JTokenType.Null)
                {
                    string mapStr = (string)mapToken;
                    if (!string.IsNullOrEmpty(mapStr))
                    {
                        return new Febris.ModelLibrary.Models.XApiModels.Extensions
                        {
                            ExtensionMap = mapStr
                        };
                    }
                }

                // T3, mirroring the JObject path: a spec producer sends an IRI-keyed object rather
                // than the dialect "extensionmap" string, and the typed path dropped it too.
                string bridged = BuildExtensionMap(JObject.FromObject(input));
                if (bridged != null)
                {
                    return new Febris.ModelLibrary.Models.XApiModels.Extensions
                    {
                        ExtensionMap = bridged
                    };
                }
                return null;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupExtensionsFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<Context> SetupContextFromDto(XApiContextDto input)
        {
            if (input == null) return null;
            try
            {
                Actor instructor = await SetupActorFromDto(input.Instructor);
                // SDKV-15: XApiContextDto.Group is now List<XApiActorDto> (the wire
                // dialect carries context.group as an ARRAY of actors), so consume
                // the list directly -- mirrors the JObject path's SetupActorGroup
                // iteration. Fallback stays the spec team's member list.
                #region [Historical] pre-SDKV-15 read (Group was a single XApiActorDto)
                // List<Actor> group = await SetupActorGroupFromDto(input.Group?.Member ?? input.Team?.Member);
                #endregion
                List<Actor> group = await SetupActorGroupFromDto(input.Group ?? input.Team?.Member);
                // Read the spec-correct ContextActivities directly; fall back
                // to the dialect-typed alias only if the producer happens to
                // send the misspelled name. See the matching comment in the
                // shared StatementFactor for the bug-fix history.
                ContextActivities contextActivities = await SetupContextActivitiesFromDto(
                    input.ContextActivities ?? input.ContextActivitesTyped);
                StatementReference statementReference = await SetupStatementReferenceFromDto(
                    input.StatementReference ?? input.Statement);
                Febris.ModelLibrary.Models.XApiModels.Extensions extensions =
                    await SetupExtensionsFromDto(input.Extensions);

                Context context = new Context();
                // T3, mirroring the JObject path: the typed path never assigned these four
                // either, so the DTO faithfully parsed registration, revision, platform and
                // language off the wire and then dropped all four on the floor.
                Guid registration;
                if (Guid.TryParse(input.Registration, out registration))
                {
                    context.Registration = registration;
                }
                context.Revision = input.Revision;
                context.Platform = input.Platform;
                context.Language = input.Language;
                if (group != null) context.Group = group;
                if (instructor != null) context.Instructor = instructor;
                if (contextActivities != null) context.ContextActivities = contextActivities;
                if (extensions != null) context.Extensions = extensions;
                if (statementReference != null) context.StatementReference = statementReference;

                if (context.ContextActivities == null && context.Registration == Guid.Empty
                    && context.Revision == null && context.StatementReference == null
                    && context.Extensions == null && context.Instructor == null
                    && context.Group == null && context.Platform == null
                    && context.Language == null)
                {
                    return null;
                }
                return context;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupContextFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<List<Actor>> SetupActorGroupFromDto(List<XApiActorDto> input)
        {
            if (input == null) return null;
            try
            {
                List<Actor> actorList = new List<Actor>();
                foreach (XApiActorDto item in input)
                {
                    Actor actor = await SetupActorFromDto(item);
                    actorList.Add(actor);
                }
                if (actorList.Count == 0) return null;
                return actorList;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupActorGroupFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<ContextActivities> SetupContextActivitiesFromDto(XApiContextActivitiesDto input)
        {
            await Task.CompletedTask;
            if (input == null) return null;
            try
            {
                if (input.Parent == null && input.Grouping == null
                    && input.Category == null && input.Other == null)
                {
                    return null;
                }
                return new ContextActivities
                {
                    Parent = null,
                    Grouping = null,
                    Category = null,
                    Other = null
                };
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupContextActivitiesFromDto: suppressed exception");
                return null;
            }
        }

        private async Task<StatementReference> SetupStatementReferenceFromDto(XApiStatementRefDto input)
        {
            await Task.CompletedTask;
            if (input == null) return null;
            try
            {
                Guid parsedId = Guid.Empty;
                Guid.TryParse(input.Id, out parsedId);
                return new StatementReference
                {
                    ObjectType = input.ObjectType,
                    Id = parsedId
                };
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupStatementReferenceFromDto: suppressed exception");
                return null;
            }
        }

        // SetupVersionFromDto removed: same principle as SetupVersion -- the tenant does not set xAPI Version;
        // central owns it. (This DTO twin previously fetched Version from central; removed for consistency so the
        // tenant never sets Version in either ingest path.)

        private async Task<List<Febris.ModelLibrary.Models.XApiModels.Attachment>> SetupAttachmentsFromDto(List<XApiAttachmentDto> input)
        {
            await Task.CompletedTask;
            if (input == null) return null;
            try
            {
                List<Febris.ModelLibrary.Models.XApiModels.Attachment> result =
                    new List<Febris.ModelLibrary.Models.XApiModels.Attachment>();
                foreach (var _ in input)
                {
                    result.Add(new Febris.ModelLibrary.Models.XApiModels.Attachment());
                }
                return result;
            }
            catch (System.Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "StatementFactor.SetupAttachmentsFromDto: suppressed exception");
                return null;
            }
        }

        #endregion

    }


}
