//Generic load page
function LoadPage(route, id, routeBase, linkBase) {
    if (id === undefined && linkBase === undefined) {
        window.location.href = route;
    }
    else if (linkBase === undefined) {
        window.location.href = route + id;
    }
    else {
        window.location.href = route + id + routeBase + linkBase;
    }

}

//Generic Action
//
// ROADMAP 20: was alert("Action Complete") on .done() with NO .fail() at all, which is the defect
// this item exists to remove in its purest form. It announced success on any 2xx, said nothing
// whatsoever on a 403 or a 500, and blocked the page to do it. Failure is now reported by the
// global net in StatusMessage.js, so only the success message belongs here.
//
// NOT FIXED, and out of scope for a feedback change: this issues a GET for something that mutates,
// so it carries no antiforgery token and is replayable from a link. It has no live caller in the
// node -- LeadButtonOperation.js is the only one and there is no LeadController here, though the
// same file IS live in central/FebrisAdminPortal, which keeps its own separate copy of this script.
// Recorded in docs/BUGS.md rather than changed blind.
function LoadAction(route, id, linkBaseId, linkBase) {
    var request;
    if (id === undefined) {
        request = $.get(route);
    }
    else if (linkBaseId === undefined) {
        request = $.get(route + id);
    }
    else {
        request = $.get(route + id + linkBaseId + linkBase);
    }

    request.done(function () {
        window.StatusMessage.ok("Done.");
    });
}


//Generic Modal
function LoadModal(route, id, routeBase, linkBase) {

    // Get the modal
    var modal = document.getElementById('Modal');

    // Get the <span> element that closes the modal
    var span = document.getElementsByClassName("close")[0];

    modal.style.display = "block";

    // When the user clicks on <span>(x), close the modal
    span.onclick = function () {
        modal.style.display = "none";
        $("#modalContent").empty();
    };

    // When the user clicks anywhere outside of the modal, close it
    window.onclick = function (event) {
        if (event.target === modal) {
            modal.style.display = "none";
            $("#modalContent").empty();
        }
    };

    if (id === undefined) {
        $("#modalContent").load(route);
    }
    else if (linkBase === undefined) {
        $("#modalContent").load(route + id);
    }
    else {
        $("#modalContent").load(route + id + routeBase + linkBase);
    }
}


// SubmitFollowing REMOVED (ROADMAP 20, 2026-08-22).
//
// It was `$.get(route + id).done(function () { })` -- an explicitly empty success handler and no
// failure handler at all, so it could not report either outcome by construction. The roadmap named
// it as an offender, and it turned out to be worse than a reporting gap: it never worked.
//
// It had ONE live caller in the node, Views/User/DetailsModal.cshtml, passing '/ArchiveToggle' as
// the route. Concatenating route and id produced a GET to "/ArchiveToggle<guid>", an address no
// controller serves, aimed at an action that does not exist under that name. The real endpoint is
// UserController.LockoutToggle, which is [HttpPost] + [ValidateAntiForgeryToken] and could never
// have been reached by a bare GET regardless. The empty .done() is why nobody noticed: the 404 came
// back, the handler did nothing, and the checkbox sat there looking saved.
//
// The two Hardware call sites had already hit the same defect under audit C-09 and were made
// read-only indicators, because Hardware has no such POST action to point at. User does, so its
// checkbox is wired to it properly instead -- see Views/User/DetailsModal.cshtml.
//
// Not replaced with a fixed generic helper. The C-07 conversions each converted their own call site
// on purpose, and Views/MessageBoard/IndexPartial.cshtml records the reason: changing the shared
// helper would silently BREAK its other callers rather than fix them. The reason a shared helper is
// the wrong shape here in the first place is that it has to know the route, the token holder and how
// to repair the control it belongs to, and guessing those is what produced this bug. Its callers are
// six sites in central/FebrisAdminPortal, which keeps its own copy of this file, plus the one here.