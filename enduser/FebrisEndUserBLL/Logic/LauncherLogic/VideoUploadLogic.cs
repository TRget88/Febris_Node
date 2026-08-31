// SPDX-FileCopyrightText: 2026 Febris
// SPDX-License-Identifier: AGPL-3.0-only
using Febris.ModelLibrary.Models.DataModels;
using Febris.SharedServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Febris.UserNode.LogicLayer.Logic.LauncherLogic
{
    public interface IVideoUploadLogic
    {
        Task<bool> ProcessVideoFiles(IFormFileCollection files);
    }


    public class VideoUploadLogic : IVideoUploadLogic
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IVideoFileHandler _fileServerHandler;
        private readonly Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic _recordingContext;

        /// <summary>
        /// Per-part ceiling. The PC producer splits at 5 MB (`MaxFileSizeMB = 5` in
        /// `VideoFileProcessing`), so this leaves generous headroom while still bounding a single
        /// request. Config key <c>VideoLimits:MaxPartBytes</c>.
        /// </summary>
        private const long DefaultMaxPartBytes = 16L * 1024 * 1024;

        /// <summary>
        /// Per-recording part ceiling. 640 parts at the 16 MiB per-part cap is a 10 GiB hard
        /// ceiling per recording, which matches the 10 GiB the post-merge validator already
        /// declares as the intended maximum video size (and which, until now, was only logged). At
        /// the producer's real 5 MB part size that is roughly 3 GB of actual video, several hours
        /// of a session. Config key <c>VideoLimits:MaxPartsPerRecording</c>.
        /// </summary>
        private const int DefaultMaxPartsPerRecording = 640;

        private readonly long MaxPartBytes;
        private readonly int MaxPartsPerRecording;

        /// <summary>
        /// Counts the parts already on disk for the recording this part belongs to, which is what
        /// bounds a recording rather than trusting the part count the CLIENT declares in the
        /// filename. A client can put any number after the second dot; it cannot fake what is
        /// already in the split directory.
        /// </summary>
        private async Task<int> CountExistingParts(string partFileName)
        {
            try
            {
                const string partToken = ".part_";
                int tokenAt = partFileName.IndexOf(partToken, StringComparison.Ordinal);
                if (tokenAt <= 0) return 0;

                string searchPattern = partFileName.Substring(0, tokenAt) + partToken + "*";
                string[] existing = await _fileServerHandler.GetDirectoryFileList(
                    StaticDetails.SplitVideoFileSystemPath, searchPattern);
                return existing?.Length ?? 0;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex, "VideoUploadLogic.CountExistingParts");
                // Unknown count must not read as "under the limit".
                return int.MaxValue;
            }
        }
        private readonly ModelLibrary.Models.DataModels.License _license;
        private readonly Hardware _hardware;

        // DI refactor
        public VideoUploadLogic(
            IHttpContextAccessor httpContextAccessor,
            IVideoFileHandler fileServerHandler,
            // T6: backs the upload gate. Without it the greedy ctor is unresolvable and MS.DI
            // silently drops to the legacy ctor below, which is exactly how a gate gets bypassed.
            Febris.UserNode.LogicLayer.Logic.DataLogic.IRecordingLogic recordingContext,
            Microsoft.Extensions.Configuration.IConfiguration config
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _fileServerHandler = fileServerHandler;
            _recordingContext = recordingContext;
            MaxPartBytes = config?.GetValue<long?>("VideoLimits:MaxPartBytes") ?? DefaultMaxPartBytes;
            MaxPartsPerRecording = config?.GetValue<int?>("VideoLimits:MaxPartsPerRecording") ?? DefaultMaxPartsPerRecording;
            _license = (ModelLibrary.Models.DataModels.License)_httpContextAccessor.HttpContext.Items["License"] ?? null;
            _hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"] ?? null;
        }

        public VideoUploadLogic(
            IHttpContextAccessor httpContextAccessor
            )
        {
            _httpContextAccessor = httpContextAccessor;
            _fileServerHandler = new VideoFileHandler();
            _recordingContext = new Febris.UserNode.LogicLayer.Logic.DataLogic.RecordingLogic(
                httpContextAccessor,
                new Febris.UserNode.DataAccessLayer.Queries.DataQueries.RecordingQueries(),
                new Febris.UserNode.DataAccessLayer.Queries.DataQueries.ParentLinkedStudentQueries());
            // The legacy self-newing path has no configuration to read, so it takes the
            // defaults. Limits still apply -- an unconfigured host is not an unlimited one.
            MaxPartBytes = DefaultMaxPartBytes;
            MaxPartsPerRecording = DefaultMaxPartsPerRecording;
            _license = (ModelLibrary.Models.DataModels.License)_httpContextAccessor.HttpContext.Items["License"] ?? null;
            _hardware = (Hardware)_httpContextAccessor.HttpContext.Items["Hardware"] ?? null;
        }

        public async Task<bool> ProcessVideoFiles(IFormFileCollection files)
        {
            bool output = false;
            try
            {
                foreach (IFormFile file in files)
                {
                    var FileDataContent = file;
                    if (FileDataContent != null && FileDataContent.Length > 0)
                    {
                        // take the input stream, and save it to a temp folder using  
                        // the original file.part name posted  
                        var stream = FileDataContent.OpenReadStream();
                        var fileName = Path.GetFileName(FileDataContent.FileName);

                        // UPLOAD GATE. Placed here deliberately: the filename is known and NOTHING
                        // has been created, deleted or truncated yet. The first destructive act is
                        // the FileDelete below, and the process-wide merge key is not touched until
                        // later still, so a refusal here leaves no disk state and no held key.
                        //
                        // What it stops: SplitVideos/ and recordings/ are one flat namespace shared
                        // by every device and every learner, and this method used to accept any part
                        // filename from any authenticated device. One device could overwrite another
                        // device's parts, or another learner's finished recording, simply by naming
                        // its upload accordingly. The authenticated device was materialised on every
                        // request (_hardware) and never read.
                        //
                        // Per FILE, not per request: nothing correlates the files in one request,
                        // and a request may legitimately carry parts of different recordings.
                        if (!await _recordingContext.MayAcceptPart(fileName, _hardware?.UUID ?? Guid.Empty))
                        {
                            Febris.SharedServices.FebrisLog.Warn(
                                "Video part '" + fileName + "' refused: not a recording this device minted.");
                            return false;
                        }

                        // QUOTA. There was none of any kind: no part-count cap, no per-device or
                        // per-learner cap, no total-bytes cap and no reaper, so an entitled device
                        // could fill the volume one accepted part at a time.
                        //
                        // Enforced HERE, per route, rather than by lowering the host's multipart
                        // limit: this host also ingests module and software packages
                        // (ModuleController.Upload, SoftwarePackageController.Upload), which are
                        // archives and legitimately large. A host-wide limit small enough to bound
                        // video would have silently broken package ingest.
                        //
                        // The node deliberately does NOT consult the tenant's MaxVideoStorage: that
                        // lives on the central Institution row, which was torn out of this context
                        // with the other central tables, and reaching for it would re-couple the
                        // node to central data. These are node-local limits.
                        if (FileDataContent.Length > MaxPartBytes)
                        {
                            Febris.SharedServices.FebrisLog.Warn(
                                "Video part '" + fileName + "' refused: " + FileDataContent.Length +
                                " bytes exceeds the per-part limit of " + MaxPartBytes + ".");
                            return false;
                        }

                        int existingParts = await CountExistingParts(fileName);
                        if (existingParts >= MaxPartsPerRecording)
                        {
                            Febris.SharedServices.FebrisLog.Warn(
                                "Video part '" + fileName + "' refused: recording already has " +
                                existingParts + " parts, at the limit of " + MaxPartsPerRecording + ".");
                            return false;
                        }

                        //var UploadPath = /*StaticDetails.BulkFolderLocation*//*.BulkTempFolderLocation*//*+*/StaticDetails.SplitFilePath;
                        var UploadPath = StaticDetails.SplitVideoFileSystemPath;
                        //Directory.CreateDirectory(UploadPath); -------I think this litterally just made the split file directory and nothing else
                        await _fileServerHandler.CreateFileDirectory(UploadPath, string.Empty);// this should not be needed. 

                        //string path = Path.Combine(UploadPath, fileName);
                        try
                        {
                            bool exists = await _fileServerHandler.FileExists(UploadPath, fileName);
                            bool isInUse = await _fileServerHandler.IsFileInUse(UploadPath + fileName);

                            if (exists && !isInUse) await _fileServerHandler.FileDelete(UploadPath, fileName);
                            //if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                            //using (var fileStream = System.IO.File.Create(Path.Combine(UploadPath, fileName)))
                            using (FileStream fileStream = await _fileServerHandler.CreationFileStream(UploadPath, fileName))
                            {
                                stream.CopyTo(fileStream);
                            }

                            // T6: this result used to be discarded, and `output` was set to true
                            // unconditionally below, so the node answered 200 {"Success":true}
                            // whether it had assembled a recording, silently skipped the merge, or
                            // written a truncated file. Only two outcomes are failures: an
                            // incomplete part set is the normal case for every part except the
                            // last, and a skip means another merge holds the key.
                            Febris.EnumLibrary.VideoMergeOutcome outcome =
                                await Task.Run(() => MergeFile(fileName));

                            if (outcome == Febris.EnumLibrary.VideoMergeOutcome.Failed)
                            {
                                Febris.SharedServices.FebrisLog.Warn(
                                    "Video merge failed for '" + fileName + "'. Parts left in place for retry.");
                                return false;
                            }
                            if (outcome == Febris.EnumLibrary.VideoMergeOutcome.Skipped)
                            {
                                Febris.SharedServices.FebrisLog.Warn(
                                    "Video merge skipped for '" + fileName + "' because another merge holds the key.");
                            }
                        }
                        catch (IOException ex)
                        {
                            Febris.SharedServices.FebrisLog.Error(ex);
                            return false;
                        }
                    }
                }
                output = true;
            }
            catch (Exception ex)
            {
                Febris.SharedServices.FebrisLog.Error(ex);
                throw;
            }
            return output;
        }


        /// <summary>
        /// T6. Three defects were fixed here together, because they combine into one symptom: the
        /// node reported success while producing no recording.
        /// <list type="number">
        ///   <item><b>The merge key never released.</b> The entry was added and tested under
        ///     <c>SplitVideoFileSystemPath + baseFileName</c> but removed under the bare
        ///     <c>baseFileName</c>, so <c>List.Remove</c> never matched. After the first SUCCESSFUL
        ///     merge of a given name, every later merge of that name was skipped for the process
        ///     lifetime. Normal use poisoned the name, no attacker needed, and because nothing
        ///     binds an upload to a device it was poisoned for every device on the node. The key is
        ///     now captured ONCE and released in a <c>finally</c>, so it also survives exceptions.</item>
        ///   <item><b>No atomicity.</b> The final served path was opened with
        ///     <c>FileMode.Create</c>, truncating any existing recording BEFORE a single chunk had
        ///     been read, and the Portal serves that directory. The merge now assembles into a temp
        ///     file and moves it into place only once it is complete.</item>
        ///   <item><b>Destructive ordering.</b> Source parts were deleted inside the copy loop,
        ///     before the output was closed or checked, and a per-chunk <c>IOException</c> was
        ///     swallowed so the loop continued deleting while silently omitting a chunk. Parts are
        ///     now deleted only after the recording is safely in place, and a failed chunk aborts
        ///     the merge instead of producing a hole.</item>
        /// </list>
        /// <para>
        /// The trailing <c>FileMover</c> call was also removed. It passed the BARE relative
        /// <c>baseFileName</c>, which resolves against the process working directory (<c>/app</c> in
        /// the container) rather than the media root, and <c>FileMover</c> is <c>async void</c> and
        /// calls <c>new Uri(currentPath)</c>, which throws on a relative path where nothing can
        /// observe it. The move it was attempting is what the temp-to-final rename now does
        /// correctly.
        /// </para>
        /// </summary>
        private async Task<Febris.EnumLibrary.VideoMergeOutcome> MergeFile(string FileName)
        {
            // parse out the different tokens from the filename according to the convention  
            string partToken = ".part_";
            string baseFileName = FileName.Substring(0, FileName.IndexOf(partToken));
            string trailingTokens = FileName.Substring(FileName.IndexOf(partToken) + partToken.Length);
            int FileIndex = 0;
            int FileCount = 0;
            int.TryParse(trailingTokens.Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
            int.TryParse(trailingTokens.Substring(trailingTokens.IndexOf(".") + 1), out FileCount);
            // get a list of all file parts in the temp folder  
            string Searchpattern = Path.GetFileName(StaticDetails.SplitVideoFileSystemPath + baseFileName) + partToken + "*";
            //string[] FilesList = Directory.GetFiles(Path.GetDirectoryName(FileName), Searchpattern);
            string path = StaticDetails.SplitVideoFileSystemPath;// + FileName;
            //string[] FilesList = _fileServerHandler.GetDirectoryFileList(Path.GetDirectoryName(FileName), Searchpattern);
            string[] FilesList = await _fileServerHandler.GetDirectoryFileList(path, Searchpattern);

            //  merge .. improvement would be to confirm individual parts are there / correctly in
            // sequence, a security check would also be important
            // only proceed if we have received all the file chunks
            if (FilesList.Count() != FileCount)
            {
                // Not an error: this is every part except the last.
                return Febris.EnumLibrary.VideoMergeOutcome.PartAccepted;
            }

            // Capture the key ONCE. The add, the test and the release must all use the same string,
            // and they did not: the release used the bare baseFileName while the other two used the
            // full split path, so the entry was never removed.
            string mergeKey = StaticDetails.SplitVideoFileSystemPath + baseFileName;

            // use a singleton to stop overlapping processes
            if (await _fileServerHandler.IsFileInUse(mergeKey))
            {
                return Febris.EnumLibrary.VideoMergeOutcome.Skipped;
            }
            await _fileServerHandler.AddFileToMerge(mergeKey);

            try
            {
                //if the file exits delete it.
                if (await _fileServerHandler.FileExists(StaticDetails.SplitVideoFileSystemPath, baseFileName))
                {
                    await _fileServerHandler.FileDelete(StaticDetails.SplitVideoFileSystemPath, baseFileName);
                }

                // add each file located to a list so we can get them into
                // the correct order for rebuilding the file
                List<SortedFile> MergeList = new List<SortedFile>();
                foreach (string File in FilesList)
                {
                    SortedFile sFile = new SortedFile();
                    sFile.FileName = File;
                    baseFileName = File.Substring(0, File.IndexOf(partToken));
                    trailingTokens = File.Substring(File.IndexOf(partToken) + partToken.Length);
                    int.TryParse(trailingTokens.
                       Substring(0, trailingTokens.IndexOf(".")), out FileIndex);
                    sFile.FileOrder = FileIndex;
                    MergeList.Add(sFile);
                }
                // sort by the file-part number to ensure we merge back in the correct order
                var MergeOrder = MergeList.OrderBy(s => s.FileOrder).ToList();

                // Assemble into a temp file beside the destination, NOT into the served path. The
                // Portal serves RecordingsFileSystemPath directly, so opening the final name with
                // FileMode.Create truncated a good recording before any chunk had been read, and a
                // reader could observe a zero-length or half-written file.
                string finalPath = StaticDetails.RecordingsFileSystemPath + baseFileName;
                string tempPath = finalPath + ".merging";

                try
                {
                    using (FileStream FS = new FileStream(tempPath, FileMode.Create))
                    {
                        // merge each file chunk back into one contiguous file stream
                        foreach (var chunk in MergeOrder)
                        {
                            // A failed chunk used to be logged and skipped while the loop carried on
                            // deleting parts, which produced a recording with a hole in it and no
                            // way to tell. A chunk that will not copy now aborts the merge with the
                            // parts still on disk, so the upload can be retried.
                            using (FileStream fileChunk = await _fileServerHandler.MergeFileStream(StaticDetails.SplitVideoFileSystemPath + chunk.FileName, FileMode.Open))
                            {
                                fileChunk.CopyTo(FS);
                            }
                        }
                        await FS.FlushAsync();
                    }

                    // file-upload-hardening: the whole video now exists at the TEMP path.
                    // Validate its real content type (magic-byte) so a disguised non-video is not stored or served.
                    // Log-only for now (no deletion) so a false positive cannot break a legitimate recording upload.
                    // Promote to reject/delete once real-traffic logs confirm no false positives on this pipeline.
                    try
                    {
                        using (FileStream assembled = await _fileServerHandler.MergeFileStream(tempPath, FileMode.Open))
                        {
                            var videoCheck = FileUploadValidator.ValidateVideo(assembled, assembled.Length, baseFileName, 10L * 1024 * 1024 * 1024);
                            if (!videoCheck.IsValid)
                            {
                                Febris.SharedServices.FebrisLog.Warn("Video upload content validation flagged '" + baseFileName + "': " + videoCheck.Reason);
                            }
                        }
                        using (FileStream scanStream = await _fileServerHandler.MergeFileStream(tempPath, FileMode.Open))
                        {
                            var scan = await Febris.SharedServices.FileScanService.ScanAsync(scanStream, baseFileName);
                            if (scan.Scanned && !scan.IsClean) { Febris.SharedServices.FebrisLog.Warn("Video upload flagged by malware scan '" + baseFileName + "': " + (scan.Threat ?? "unknown")); }
                        }
                    }
                    catch (Exception ex)
                    {
                        Febris.SharedServices.FebrisLog.Error(ex, "VideoUploadLogic.MergeFile: post-merge content validation error");
                    }

                    // The recording only becomes visible to the Portal at this point, complete.
                    System.IO.File.Move(tempPath, finalPath, true);
                }
                catch (Exception ex)
                {
                    Febris.SharedServices.FebrisLog.Error(ex, "VideoUploadLogic.MergeFile: merge failed, leaving parts in place for retry");
                    try
                    {
                        if (System.IO.File.Exists(tempPath)) System.IO.File.Delete(tempPath);
                    }
                    catch (Exception cleanupEx)
                    {
                        Febris.SharedServices.FebrisLog.Error(cleanupEx, "VideoUploadLogic.MergeFile: temp cleanup failed");
                    }
                    return Febris.EnumLibrary.VideoMergeOutcome.Failed;
                }

                // Parts are removed only now, once the recording is safely in place. They used to be
                // deleted inside the copy loop, so a failure mid-merge destroyed the only means of
                // retrying.
                foreach (var chunk in MergeOrder)
                {
                    try
                    {
                        await _fileServerHandler.DeleteSplitFiles(StaticDetails.SplitVideoFileSystemPath, chunk.FileName);
                    }
                    catch (Exception ex)
                    {
                        // The recording exists; an orphaned part is a housekeeping problem, not a
                        // reason to report the upload as failed.
                        Febris.SharedServices.FebrisLog.Error(ex, "VideoUploadLogic.MergeFile: could not delete merged part '" + chunk.FileName + "'");
                    }
                }

                return Febris.EnumLibrary.VideoMergeOutcome.Merged;
            }
            finally
            {
                // Released on EVERY path, including exceptions, and under the SAME key it was added
                // with. Both halves of that sentence were broken before.
                MergeFileManager.Instance.RemoveFile(mergeKey);
            }
        }
    }
}
