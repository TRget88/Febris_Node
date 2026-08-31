// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
//using static Febris.UserNode.Portal.Startup;

namespace Febris.UserNode.Portal.LocalUtility
{
//    public class FileServerHandler
//    {
//        public FileServerHandler()
//        {
//        }

//        private SmbSettings smbSettings()
//        {
//            SmbSettings smbSettings = new SmbSettings()
//            {
//                Secret = Smb.Configuration.GetValue<string>("SmbClient:Secret"),
//                UserName = Smb.Configuration.GetValue<string>("SmbClient:UserName")
//            };
//            return smbSettings;
//        }
//        private SmbSettings smbSettings(IConfiguration configuration)
//        {
//            SmbSettings smbSettings = new SmbSettings()
//            {
//                Secret = configuration.GetValue<string>("SmbClient:Secret"),
//                UserName = configuration.GetValue<string>("SmbClient:UserName")
//            };
//            return smbSettings;
//        }
//        //#############################################################################
//        // File initalizer
//        //#############################################################################
//        public async void FileInitalizer(IConfiguration config)
//        {
//            StaticDetails.BaseFileSystemPath = config.GetValue<string>("SmbClient:Path");

//            StaticDetails.UniqueFileSystemPath = config.GetValue<string>("FileSystem:UniqueFileSystemPath");

//            StaticDetails.SpecificFileSystemPath = StaticDetails.BaseFileSystemPath + StaticDetails.UniqueFileSystemPath;
//#if (DEBUG)
//            StaticDetails.MediaFileSystemPath = StaticDetails.SpecificFileSystemPath + @"media\";
//            StaticDetails.VideoFileSystemPath = StaticDetails.MediaFileSystemPath + @"video\";
//            StaticDetails.SplitVideoFileSystemPath = StaticDetails.VideoFileSystemPath + @"SplitVideos\";
//            StaticDetails.RecordingsFileSystemPath = StaticDetails.VideoFileSystemPath + @"recordings\";
//            StaticDetails.ImageFileSystemPath = StaticDetails.MediaFileSystemPath + @"Images\";
//            StaticDetails.LogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"Logos\";
//            StaticDetails.ProfessionalFileSystemPath = StaticDetails.ImageFileSystemPath + @"ProfessionalImages\";
//            StaticDetails.EduOrgLogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"EduOrgLogos\";
//            StaticDetails.ProfessionalFileSystemPathForDb = @"ProfessionalImages\";
//            StaticDetails.LogoFileSystemPathForDb = @"Logos\";
//            StaticDetails.EduOrgLogoFileSystemPathForDb = @"EduOrgLogos\";

//            StaticDetails.ModuleFileSystemPath = StaticDetails.BaseFileSystemPath + @"modules\";

//            StaticDetails.StatementFileSystemPath = StaticDetails.SpecificFileSystemPath + @"statements\";
//            StaticDetails.VoidStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"voidstatements\";
//            StaticDetails.JSONStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"JSONstatements\";


//            StaticDetails.LogFileSystemPath = StaticDetails.SpecificFileSystemPath + @"logs\";
//            StaticDetails.APILogFileSystemPath = StaticDetails.LogFileSystemPath + @"api\";
//            StaticDetails.PortalLogFileSystemPath = StaticDetails.LogFileSystemPath + @"portal\";
//#elif (STAGING)
//            StaticDetails.MediaFileSystemPath = StaticDetails.SpecificFileSystemPath + @"media/";
//            StaticDetails.VideoFileSystemPath = StaticDetails.MediaFileSystemPath + @"video/";
//            StaticDetails.SplitVideoFileSystemPath = StaticDetails.VideoFileSystemPath + @"SplitVideos/";
//            StaticDetails.RecordingsFileSystemPath = StaticDetails.VideoFileSystemPath + @"recordings/";
//            StaticDetails.ImageFileSystemPath = StaticDetails.MediaFileSystemPath + @"Images/";
//            StaticDetails.LogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"Logos/";
//            StaticDetails.ProfessionalFileSystemPath = StaticDetails.ImageFileSystemPath + @"ProfessionalImages/";
//            StaticDetails.EduOrgLogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"EduOrgLogos/";
//            StaticDetails.ProfessionalFileSystemPathForDb = @"ProfessionalImages/";
//            StaticDetails.LogoFileSystemPathForDb = @"Logos/";
//            StaticDetails.EduOrgLogoFileSystemPathForDb = @"EduOrgLogos/";

//            StaticDetails.ModuleFileSystemPath = StaticDetails.BaseFileSystemPath + @"modules/";

//            StaticDetails.StatementFileSystemPath = StaticDetails.SpecificFileSystemPath + @"statements/";
//            StaticDetails.VoidStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"voidstatements/";
//            StaticDetails.JSONStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"JSONstatements/";


//            StaticDetails.LogFileSystemPath = StaticDetails.SpecificFileSystemPath + @"logs/";
//            StaticDetails.APILogFileSystemPath = StaticDetails.LogFileSystemPath + @"api/";
//            StaticDetails.PortalLogFileSystemPath = StaticDetails.LogFileSystemPath + @"portal/";
//#else
//            StaticDetails.MediaFileSystemPath = StaticDetails.SpecificFileSystemPath + @"media/";
//            StaticDetails.VideoFileSystemPath = StaticDetails.MediaFileSystemPath + @"video/";
//            StaticDetails.SplitVideoFileSystemPath = StaticDetails.VideoFileSystemPath + @"SplitVideos/";
//            StaticDetails.RecordingsFileSystemPath = StaticDetails.VideoFileSystemPath + @"recordings/";
//            StaticDetails.ImageFileSystemPath = StaticDetails.MediaFileSystemPath + @"Images/";
//            StaticDetails.LogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"Logos/";
//            StaticDetails.ProfessionalFileSystemPath = StaticDetails.ImageFileSystemPath + @"ProfessionalImages/";
//            StaticDetails.EduOrgLogoFileSystemPath = StaticDetails.ImageFileSystemPath + @"EduOrgLogos/";
//            StaticDetails.ProfessionalFileSystemPathForDb = @"ProfessionalImages/";
//            StaticDetails.LogoFileSystemPathForDb = @"Logos/";
//            StaticDetails.EduOrgLogoFileSystemPathForDb = @"EduOrgLogos/";

//            StaticDetails.ModuleFileSystemPath = StaticDetails.BaseFileSystemPath + @"modules/";

//            StaticDetails.StatementFileSystemPath = StaticDetails.SpecificFileSystemPath + @"statements/";
//            StaticDetails.VoidStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"voidstatements/";
//            StaticDetails.JSONStatementFileSystemPath = StaticDetails.StatementFileSystemPath + @"JSONstatements/";


//            StaticDetails.LogFileSystemPath = StaticDetails.SpecificFileSystemPath + @"logs/";
//            StaticDetails.APILogFileSystemPath = StaticDetails.LogFileSystemPath + @"api/";
//            StaticDetails.PortalLogFileSystemPath = StaticDetails.LogFileSystemPath + @"portal/";
//#endif            

//            try
//            {
//                List<string> FileList = new List<string>
//                {
//                    StaticDetails.BaseFileSystemPath,
//                    StaticDetails.SpecificFileSystemPath,
//                    StaticDetails.MediaFileSystemPath,
//                    StaticDetails.VideoFileSystemPath,
//                    StaticDetails.SplitVideoFileSystemPath,
//                    StaticDetails.RecordingsFileSystemPath,
//                    StaticDetails.ImageFileSystemPath,
//                    StaticDetails.LogoFileSystemPath,
//                    StaticDetails.ProfessionalFileSystemPath,
//                    StaticDetails.ModuleFileSystemPath,
//                    StaticDetails.StatementFileSystemPath,
//                    StaticDetails.VoidStatementFileSystemPath,
//                    StaticDetails.JSONStatementFileSystemPath,
//                    //StaticDetails.LogoFileSystemPath,
//                    StaticDetails.APILogFileSystemPath,
//                    StaticDetails.PortalLogFileSystemPath,
//                    StaticDetails.EduOrgLogoFileSystemPath,
//                    StaticDetails.LogFileSystemPath,
//                    StaticDetails.PortalLogFileSystemPath,
//                    StaticDetails.APILogFileSystemPath
//                };
//                foreach (var file in FileList)
//                {
//                    try
//                    {
//                        CreateFileDirectory(config, file, string.Empty);
//                        //CreateFileDirectory(file, string.Empty);
//                    }
//                    catch { }
//                }

//            }
//            catch (Exception)
//            {

//                throw;
//            }

//        }
//        //#############################################################################
//        // connecting to the file server to store
//        //#############################################################################
//        public async Task<bool> FileStorage(string path, object input)
//        {
//            try
//            {
//                bool success = false;

//                SmbSettings settings = smbSettings();

//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                File.Create(path + input);

//                return success;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//            return false;
//        }

//        //#############################################################################
//        // FileStream
//        //#############################################################################
//        public FileStream CreationFileStream(string path, string input)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path + input), "Basic", credentials);
//                FileStream stream = File.Create(path + input);

//                return stream;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }
//        #region unused
//        //#############################################################################
//        // FileStream
//        //#############################################################################
//        //public FileStream OutgoingFileStream(string path)
//        //{
//        //    try
//        //    {
//        //        NetworkCredential credentials = new NetworkCredential(smbSettings.UserName, smbSettings.Secret);
//        //        CredentialCache credentialCache = new CredentialCache();
//        //        credentialCache.Add(new Uri(path), "Basic", credentials);
//        //        FileStream stream = new FileStream(path, FileMode.Open);//(path + input);

//        //        return stream;
//        //    }
//        //    catch (Exception)
//        //    {

//        //        throw;
//        //    }
//        //}

//        //#############################################################################
//        // FileStream
//        //#############################################################################
//        //public FileStream MergeFileStream(string path, FileMode mode)
//        //{
//        //    try
//        //    {
//        //        //SmbSettings smbSettings = new SmbSettings()
//        //        //{
//        //        //    Secret = _config["SmbClient:Secret"],
//        //        //    UserName = _config["SmbClient:UserName"]
//        //        //};
//        //        //var smbInfo = _config.Value;
//        //        //var smbSettings = smbInfo;/*.Get<SmbSettings>();*/
//        //        //string newPath = path + input;

//        //        NetworkCredential credentials = new NetworkCredential(smbSettings.UserName, smbSettings.Secret);
//        //        CredentialCache credentialCache = new CredentialCache();
//        //        credentialCache.Add(new Uri(path), "Basic", credentials);
//        //        FileStream stream = File.Open(path, mode);//new FileStream(path, mode);

//        //        return stream;
//        //    }
//        //    catch (Exception)
//        //    {

//        //        throw;
//        //    }
//        //}

//        //#############################################################################
//        // FileStream
//        //#############################################################################
//        #endregion
//        public void StatementCreationFileStream(string path, string StatementUUID, object input)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();

//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                //NetworkCredential credentials = new NetworkCredential(smbSettings.UserName, smbSettings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path + input), "Basic", credentials);
//                //string fullPath = Path.Combine(path, StatementUUID + ".json");
//                string fullPath = path + "/" + StatementUUID + ".json";
//                using (FileStream stream = File.Create(fullPath))
//                {
//                    byte[] statment = new UTF8Encoding(true).GetBytes(input.ToString());
//                    stream.Write(statment, 0, statment.Length);
//                }
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }
//        //#############################################################################
//        // connecting to the file storage retriever
//        //#############################################################################
//        public async void CreateFileDirectory(IConfiguration config, string path, string name)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings(config);
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                Directory.CreateDirectory(path + name);
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }
//        public async void CreateFileDirectory(string path, string name)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                Directory.CreateDirectory(path + name);
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        //#############################################################################
//        // connecting to the file storage retriever - I am not sure if this is ever used. But it may be somewhere in the api
//        //#############################################################################
//        public async Task<object> FileRetrieval(string path, string name)
//        {
//            try
//            {
//                bool success = false;
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                File.GetAttributes(path + name);

//                return success;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//            return false;
//        }



//        ////#############################################################################
//        //// connecting to the file storage retriever
//        ////#############################################################################
//        public async Task<bool> FileExists(string path, string name)
//        {
//            try
//            {
//                bool success = false;
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                success = File.Exists(path + name);

//                return success;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//            return false;
//        }

//        //#############################################################################
//        // delete file
//        //#############################################################################
//        public async void FileDelete(string path, string name)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//#if (DEBUG)
//                File.Delete(path + name);
//#elif (STAGING)
//File.Delete(path+ name);
//#else
//File.Delete(path+ name);
//#endif
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        ////#############################################################################
//        //// Move file
//        ////#############################################################################
//        public async void FileMover(string currentPath, string newPath)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(currentPath), "Basic", credentials);
//                credentialCache.Add(new Uri(newPath), "Basic", credentials);
//                File.Move(currentPath, newPath);
//            }
//            catch (Exception)
//            {
//                //cannot move file to an area with a file that already has that name. 
//                string name = Path.GetFileName(currentPath);
//                string path = Path.GetDirectoryName(currentPath);
//                FileDelete(path, name);
//                //throw;
//            }
//        }

//        //#############################################################################
//        // connecting to the file storage retriever
//        //#############################################################################
//        public string[] GetDirectoryFileList(string path, string searchPattern)
//        {
//            try
//            {
//                string[] fileList = { };
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                var fullFileList = Directory.GetFiles(path, searchPattern)
//                    .Select(x => new FileInfo(x))
//                    .OrderByDescending(x => x.LastWriteTime)
//                    .ToArray();

//                fileList = fullFileList.Select(o => o.Name).ToArray();

//                return fileList;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        ////#############################################################################
//        //// Get all files names from directory
//        ////#############################################################################
//        public IEnumerable<string> GetFullDirectoryFileList(string path)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                //fileList = Directory.GetFiles(path, searchPattern);
//                IEnumerable<string> fileList = Directory.EnumerateFiles(path);

//                return fileList;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        #region unused
//        ////#############################################################################
//        //// connecting to the file storage retriever
//        ////#############################################################################
//        //public async void AddFileToMerge(string path)
//        //{
//        //    try
//        //    {
//        //        SmbSettings settings = smbSettings();
//        //        NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//        //        CredentialCache credentialCache = new CredentialCache();
//        //        credentialCache.Add(new Uri(path), "Basic", credentials);
//        //        MergeFileManager.Instance.AddFile(path);
//        //    }
//        //    catch (Exception)
//        //    {

//        //        throw;
//        //    }
//        //}

//        ////#############################################################################
//        //// connecting to the file storage retriever
//        ////#############################################################################
//        //public bool IsFileInUse(string path)
//        //{
//        //    try
//        //    {
//        //        bool isInUse = true;
//        //        SmbSettings settings = smbSettings();
//        //        NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//        //        CredentialCache credentialCache = new CredentialCache();
//        //        credentialCache.Add(new Uri(path), "Basic", credentials);
//        //        isInUse = MergeFileManager.Instance.InUse(path);
//        //        return isInUse;
//        //    }
//        //    catch (Exception)
//        //    {

//        //        throw;
//        //    }
//        //}
//        #endregion

//        //#############################################################################
//        // delete split files
//        //#############################################################################
//        public async void DeleteSplitFiles(string path, string name)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                File.Delete(name);
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        //#############################################################################
//        // Get image
//        //#############################################################################
//        public byte[] ImageRetrieval(string path)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                byte[] image = { };
//                try
//                {
//                    image = File.ReadAllBytes(path);
//                }
//                catch
//                { }

//                return image;
//            }
//            catch (Exception)
//            {
//                return null;
//            }
//        }
//        //#############################################################################
//        // Get video
//        //#############################################################################
//        public FileStream VideoRetrieval(string path)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                FileStream stream = new FileStream(path, FileMode.Open);

//                return stream;
//            }
//            catch (Exception)
//            {
//                return null;
//            }
//        }

//        //#############################################################################
//        // connecting to the file storage retriever
//        //#############################################################################
//        public async Task<JObject> JsonFileRetrieval(string path, string name)
//        {
//            try
//            {
//                JObject jobj = new JObject();
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                //string file = File.ReadAllText(Path.Combine(path,name));
//#if (DEBUG)
//                string file = File.ReadAllText(Path.Combine(path, name));
//#elif (STAGING)
//                string file = File.ReadAllText(path + name);
//#else
//                string file = File.ReadAllText(path + name);
//#endif

//                jobj = JsonConvert.DeserializeObject<JObject>(file);

//                return jobj;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        //#############################################################################
//        // connecting to the file storage retriever - I am not sure if this is ever used. But it may be somewhere in the api
//        //#############################################################################        
//        public FileStream StatementFileRetrieval(string path, string name)
//        {
//            try
//            {
//#if (DEBUG)
//                string fullPath = Path.Combine(path, name);
//#elif (STAGING)
//                string fullPath = path + "/" + name;
//#else
//                string fullPath = path + "/" + name;
//#endif
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(fullPath), "Basic", credentials);
//                FileStream stream = new FileStream(fullPath, FileMode.Open);

//                return stream;
//            }
//            catch (Exception)
//            {

//                throw;
//            }
//        }

//        //#############################################################################
//        // Get image
//        //#############################################################################
//        public long GetFileLength(string path)
//        {
//            try
//            {
//                SmbSettings settings = smbSettings();
//                NetworkCredential credentials = new NetworkCredential(settings.UserName, settings.Secret);
//                CredentialCache credentialCache = new CredentialCache();
//                credentialCache.Add(new Uri(path), "Basic", credentials);
//                FileInfo info = new FileInfo(path);
//                long fileSize = 0;
//                fileSize = info.Length;
//                return fileSize;
//            }
//            catch (Exception)
//            {
//                return 0;
//            }
//        }

//    }
}
