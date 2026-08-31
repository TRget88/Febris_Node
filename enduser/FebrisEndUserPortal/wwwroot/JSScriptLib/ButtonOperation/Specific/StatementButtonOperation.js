
//Button Action Tree
function StatementButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    var linkBase = $(sender).attr('LinkBase'); 

    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "VoidStatement":
            VoidStatement(item);
            break;
        case "StatementDownload":
            DownloadStatement(item);
            break;
        //case "StatementDetails":
        //    route = "/XAPI/StatementDetailsModalPartial?statementId="
        //    LoadModal(route, item);
        //    break;
        default:
            break;
    }

}

// Statement JSON download, restored. It previously pointed at a downloader route on the old xAPI
// controller, which does not exist in this repo, and handed it to a load-and-save helper that was
// CALLED from three portals and DEFINED in none of them. Clicking the button threw a ReferenceError
// into the console and did nothing.
//
// That helper is deliberately not recreated. The 2021 original fetched the document and then built
// an anchor with a data: URI and a hard-coded download name, so every statement arrived under the
// same filename and the whole document was pushed through a URI. The server now returns
// Content-Disposition with a real per-statement filename, so navigating to the URL is enough and
// the browser saves it directly.
//
// The id is the statement UUID. No antiforgery token: this is a GET and changes nothing.
function DownloadStatement(statementUuid) {
    if (!statementUuid) {
        return;
    }
    window.location.href = "/Statement/StatementDownload?statementId=" + encodeURIComponent(statementUuid);
}

// T5. Voiding is irreversible by owner ruling and by the xAPI spec, so the confirm is not decoration
// -- there is no unvoid to fall back on.
//
// This is a TOKENISED POST. The route it replaces was a bare GET on the old xAPI controller taking
// statementId in the query string, which is the exact shape audit C-07 was raised about: a
// GET-reachable mutator fires from any page a logged-in admin visits. That controller never existed
// in this repo either, so the button had been dead since the port.
//
// The id is the statement UUID, not the table key. The server binds Guid statementId.
function VoidStatement(statementUuid) {
    if (!statementUuid) {
        return;
    }
    if (!confirm("Void this statement?\n\nIt will stop counting toward records and reports, and this cannot be undone.")) {
        return;
    }

    var token = $('#statementAntiForgeryHolder input[name="__RequestVerificationToken"]').val();

    $.post("/Statement/VoidStatement", { statementId: statementUuid, __RequestVerificationToken: token })
        .done(function (data) {
            if (data && data.success) {
                // The statement is now hidden from every ordinary read by the global query filter,
                // so the list it came from is stale. Reload rather than patch the row.
                location.reload();
            } else {
                // The server refuses without saying why on purpose: "not an admin", "no such
                // statement" and "already voided" are all a plain refusal to the browser.
                //
                // ROADMAP 20: a refusal arrives here as HTTP 200 with success:false, which the
                // global net cannot see, so it is announced explicitly. alert() replaced by a
                // toast -- this call site already reported all three outcomes correctly, it just
                // blocked the page to do it.
                window.StatusMessage.refused(
                    "The statement was not voided.",
                    "It may already be voided, or you may not have permission.");
            }
        });
    // No .fail() -- the transport failure is reported by the global net in StatusMessage.js.
}