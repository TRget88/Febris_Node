// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.XApiModels;
using Febris.SharedServices;
using System;
using System.Threading.Tasks;

namespace Febris.PrimaryLogicLayer.Logic.XApiLogic
{
    public interface IStatementDownloadLogic
    {
        /// <summary>
        /// Returns the stored JSON for a statement, or null when the caller may not see it or
        /// nothing is stored for it. The two refusals are deliberately indistinguishable to the
        /// caller so a download cannot be used to probe which statements exist.
        /// </summary>
        Task<byte[]> Get(Guid statementUuid);
    }

    /// <summary>
    /// Statement JSON download. A RESTORED feature, not a new one.
    ///
    /// <para>
    /// It was fully implemented and wired in the 2021 Portal as
    /// <c>xAPIController.StatementDownloader(long id)</c> (retired SVN, dated 2021-06-24) and did
    /// not survive the port into this repo. Four separate pieces were missing here, any one of
    /// which alone was fatal: no controller action and in fact no <c>XAPI</c> controller at all, so
    /// the whole <c>/XAPI/*</c> route family the JS targeted was dead; a <c>LoadStatementAction</c>
    /// helper called from three portals and defined in none of them; no view rendering the button;
    /// and <c>IStatementFileHandler</c> being write-only.
    /// </para>
    ///
    /// <para>
    /// <b>Authorization deliberately follows the READ scope, not the void gate.</b> Downloading a
    /// statement discloses exactly what viewing it discloses, so anyone who may see a statement may
    /// export it. This mirrors the 2021 arrangement, where the download button rendered for
    /// educators and admins while void rendered for staff alone. It is enforced by going through
    /// <see cref="IStatementLogic"/>, whose Get already applies the node's per-role filter: admins,
    /// Febris admins and educators see everything, a parent sees only their linked actors, and a
    /// user sees only their own actor. Re-implementing that filter here would give voiding and
    /// downloading two chances to disagree.
    /// </para>
    /// </summary>
    public class StatementDownloadLogic : IStatementDownloadLogic
    {
        private readonly IStatementLogic _statements;
        private readonly IStatementFileHandler _files;

        public StatementDownloadLogic(IStatementLogic statements, IStatementFileHandler files)
        {
            _statements = statements;
            _files = files;
        }

        public async Task<byte[]> Get(Guid statementUuid)
        {
            if (statementUuid == Guid.Empty)
            {
                return null;
            }

            try
            {
                // THE access check. Get applies the caller's role filter and returns null when the
                // caller may not see this statement. Nothing below runs in that case, so a denied
                // caller never reaches the file system at all.
                Statement visible = await _statements.Get(statementUuid);
                if (visible == null || visible.UUID == Guid.Empty)
                {
                    FebrisLog.Warn("Statement download refused for '" + statementUuid + "': not visible to this caller.");
                    return null;
                }

                byte[] content = await _files.DownloadPackage(statementUuid.ToString());
                if (content == null || content.Length == 0)
                {
                    // Not an error. Statements ingested before the JSON copy was being written have
                    // a row and no file, and the caller reports that as "nothing to download".
                    FebrisLog.Warn("No stored JSON found for statement '" + statementUuid + "'.");
                    return null;
                }

                return content;
            }
            catch (Exception ex)
            {
                // StatementLogic.Get rethrows when the statement does not exist, so an unknown uuid
                // arrives here rather than as a null. It is a refusal, not a server error.
                FebrisLog.Error(ex, "StatementDownloadLogic.Get");
                return null;
            }
        }
    }
}
