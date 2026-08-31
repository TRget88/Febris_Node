// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.ViewModels;
using Febris.SharedServices;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Febris.UserNode.DataAccessLayer.DataContext
{
    public class AppConfiguration
    {
        public string DataConnectionString { get; set; }
        public string XApiConnectionString { get; set; }
        public string UserDataConnectionString { get; set; }
        public string AnalyticsConnectionString { get; set; }
        //public SmbSettings SmbSettings { get; set; }

        public AppConfiguration()
        {
#if (DEBUG)
            #region old way
            ConfigurationBuilder configBuilder = new ConfigurationBuilder();
            // Repo-root-relative discovery (mirrors the shared DAL's AppConfiguration). Replaces a
            // hardcoded absolute path into one developer's filesystem, which no longer exists
            // after the move to enduser/. Works from any checkout path.
            string path = ResolveEndUserPortalConfigPath("appsettings.Development.json");
            configBuilder.AddJsonFile(path, false);
            //Db connection strings
            var root = configBuilder.Build();
            #endregion
#elif (STAGING)
            ConfigurationBuilder configBuilder = new ConfigurationBuilder();            
            string path = ResolveEndUserPortalConfigPath("appsettings.Staging.json");
            configBuilder.AddJsonFile(path, false);
            //Db connection strings
            var root = configBuilder.Build();
#else
            #region passing back
            var root = StaticDetails.PassedBackConfig;
            //var dataSetting = root.GetSection("ConnectionStrings:DataDBConnection");//??string.Empty;
            //var userSetting = root.GetSection("ConnectionStrings:UserDBConnection");
            //var xApiSetting = root.GetSection("ConnectionStrings:XAPIDBConnection");
            ////var testUserXApiSetting = root.GetSection("ConnectionStrings:TestUserXAPIDBConnection");
            ////var marketingSetting = root.GetSection("ConnectionStrings:MarketingDBConnection");
            //var analyticsSetting = root.GetSection("ConnectionStrings:AnalyticsDBConnection");
            #endregion
            //string path = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
#endif
            var dataSetting = root.GetSection("ConnectionStrings:DataDBConnection");
            var xApiSetting = root.GetSection("ConnectionStrings:XAPIDBConnection");
            var userSetting = root.GetSection("ConnectionStrings:UserDBConnection");
            //var testUserXApiSetting = root.GetSection("ConnectionStrings:TestUserXAPIDBConnection");
            //var marketingSetting = root.GetSection("ConnectionStrings:MarketingDBConnection");
            var analyticsSetting = root.GetSection("ConnectionStrings:AnalyticsDBConnection");


            DataConnectionString = dataSetting.Value;
            XApiConnectionString = xApiSetting.Value;
            UserDataConnectionString = userSetting.Value;
            AnalyticsConnectionString = analyticsSetting.Value;

            //File system connections
            var smbSecret = root.GetSection("SmbClient:Secret");
            var smbUserName = root.GetSection("SmbClient:UserName");
            var smbPath = root.GetSection("SmbClient:Path");
            //SmbSettings = new SmbSettings()
            //{
            //    UserName = smbUserName.ToString(),
            //    Secret = smbSecret.ToString(),
            //    Path = smbPath.ToString()
            //};
        }

        /// <summary>
        /// Locate enduser/FebrisEndUserPortal/appsettings.Development.json relative to the repo root, found
        /// by walking up from the assembly output dir (then the current directory) looking for the repo-root
        /// marker (.git). Mirrors the shared DAL's AppConfiguration so a DEBUG run works from any checkout
        /// path without a hardcoded absolute path.
        /// </summary>
        private static string ResolveEndUserPortalConfigPath(string fileName)
        {
            string relative = Path.Combine("enduser", "FebrisEndUserPortal", fileName);

            string fromAssembly = TryFindFromAncestors(AppContext.BaseDirectory, relative);
            if (fromAssembly != null) return fromAssembly;

            string fromCwd = TryFindFromAncestors(Directory.GetCurrentDirectory(), relative);
            if (fromCwd != null) return fromCwd;

            throw new FileNotFoundException(
                "Could not locate the repo root (.git) in any ancestor of '" + AppContext.BaseDirectory +
                "' or '" + Directory.GetCurrentDirectory() +
                "'. AppConfiguration's DEBUG path assumes the host is run from inside a Febris checkout.");
        }

        private static string TryFindFromAncestors(string startDir, string repoRelativePath)
        {
            if (string.IsNullOrEmpty(startDir)) return null;
            DirectoryInfo dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                // Repo-root marker: the .git directory (normal checkout) or the .git file (git worktree).
                string gitMarker = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
                {
                    return Path.Combine(dir.FullName, repoRelativePath);
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
