//Button Action Tree
function LocalSoftwarePackageButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');

    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/LocalSoftwarePackage/edit?id=";
            LoadPage(route, item);
            break;
        case "Create":
            route = "/LocalSoftwarePackage/create";
            LoadPage(route);
            break;
        case "Delete":
            route = "/LocalSoftwarePackage/delete";
            LoadPage(route, item);
            break;
        case "DownloadPackage":
            route = "/LocalSoftwarePackage/download?input=";
            LoadFileAction(route, item);
            break;
        default:
            break;
    }
}


// ROADMAP 20 (found 2026-08-25, in the surface ROADMAP 16 made reachable). This download used to
// report failure with console.error ALONE, which no operator ever sees.
//
// It sat in a double blind spot rather than being an oversight anyone could have spotted: the
// global failure net in StatusMessage.js hooks jQuery's ajaxError, and this call uses `fetch`, which
// jQuery knows nothing about; and the AjaxFeedbackCoverageTests source guard only scans MUTATING
// call sites, and this is a GET. So neither the runtime net nor the build-time ratchet could see it.
// A package download that silently does nothing is exactly the failure ROADMAP 20 exists to remove.
//
// Reported here rather than left to the net, because the net cannot reach fetch. Success stays
// silent: the browser's own download indicator is the confirmation, and a toast on top of it would
// be noise.
function LoadFileAction(route, item) {
    fetch(route + item)
        .then((response) => {
            if (response.status != 200) {
                let errorMessage = "Error processing the request... (" + response.status + " " + response.statusText + ")";
                let httpError = new Error(errorMessage);
                // Carried so the reporter below can name the ACTUAL status. Without it every
                // failure would describe itself as "the request never reached the server", which
                // is the one thing a 404 or a 403 definitely is not.
                httpError.status = response.status;
                throw httpError;
            } else {
                //debugger;
                return response.blob();
            }
        })
        .then((blob) => {
            //debugger;
            downloadData('FebrisSoftwarePackage.zip', blob);
        })
        .catch(error => {
            console.error(error);
            if (window.StatusMessage) {
                // StatusMessage.describe reads only `.status` off its first argument, so an object
                // carrying the status is enough to reuse the shared wording. A fetch that rejects
                // outright has no response and no status, and falls through to 0, which describe
                // correctly renders as "the request never reached the server".
                window.StatusMessage.failed(
                    "The package could not be downloaded.",
                    { status: error && error.status ? error.status : 0 },
                    null);
            }
        });
}


// Solution for big files (source: https://stackoverflow.com/a/25975345/831138)
function downloadData(filenameForDownload, data) {
    var textUrl = URL.createObjectURL(data);
    var element = document.createElement('a');
    element.setAttribute('href', textUrl);
    element.setAttribute('download', filenameForDownload);
    element.style.display = 'none';
    document.body.appendChild(element);
    element.click();
    document.body.removeChild(element);
}
