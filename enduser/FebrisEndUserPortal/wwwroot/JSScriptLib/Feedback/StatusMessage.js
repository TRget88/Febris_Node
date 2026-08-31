/* StatusMessage -- browser-side outcome reporting for AJAX (ROADMAP 20, owner-raised 2026-08-05).

   THE RULE: every AJAX call that changes state gets an explicit success AND failure notification.
   Silence is not success.

   Named to mirror the server-side TempData["StatusMessage"] idiom on purpose, so the two read as one
   mechanism rather than two unrelated ones. The server sets TempData["StatusMessage"] and the layout
   surfaces it after a redirect; a script calls StatusMessage.ok() and it surfaces immediately. Same
   name, same job, different half of the round trip.

   ------------------------------------------------------------------------------------------------
   WHO REPORTS WHAT -- this contract is the whole point of doing it once instead of per call site.

     FAILURE is owned by the global net installed at the bottom of this file. Call sites do NOT
     report their own AJAX failures. jQuery's ajaxError fires for every failed request whether or
     not the call has its own error handler, so a call site that also reported would produce two
     toasts for one failure. A call site may still KEEP an error handler to repair local state --
     reverting a checkbox, say -- it just must not announce anything there.

     SUCCESS is owned by the call site, because only the call site knows what succeeded. The net
     cannot tell "archived" from "unarchived" from "member added".

   The consequence worth stating: a mutating call added later, by somebody who forgets this file
   exists, still reports its failures. It just will not congratulate the user. That asymmetry is
   deliberate -- the dangerous silence is the one that hides a refusal.
   ------------------------------------------------------------------------------------------------

   THE THREE STATES the owner asked to be able to tell apart -- "saved", "refused" and "the request
   never arrived" -- come out of the xhr, and describe() below is where they are separated. The one
   that mattered most is xhr.status === 0: the request never reached the server at all, which until
   now was indistinguishable from a refusal because both produced nothing on screen.

   No dependency beyond jQuery, which the layout already loads in <head> before this file. */

(function (window, document) {
    "use strict";

    var CONTAINER_ID = "statusMessageToasts";
    var OK_DISMISS_MS = 5000;
    var WARN_DISMISS_MS = 9000;

    function container() {
        var el = document.getElementById(CONTAINER_ID);
        if (el === null) {
            el = document.createElement("div");
            el.id = CONTAINER_ID;
            // Announced by screen readers as it changes. "polite" rather than "assertive" so a
            // success toast does not interrupt whatever the user is reading; failures are sticky,
            // so they will still be read when the user gets there.
            el.setAttribute("role", "status");
            el.setAttribute("aria-live", "polite");
            document.body.appendChild(el);
        }
        return el;
    }

    /* One toast. kind is "ok", "warn" or "failed". Failures never auto-dismiss: a message the user
       can miss by looking away is the same defect this item exists to remove. */
    function show(kind, text, detail) {
        var host = container();

        var toast = document.createElement("div");
        toast.className = "status-toast status-toast-" + kind;

        var title = document.createElement("span");
        title.className = "status-toast-title";
        title.textContent = text === undefined || text === null || text === ""
            ? "Done."
            : String(text);
        toast.appendChild(title);

        if (detail !== undefined && detail !== null && detail !== "") {
            var sub = document.createElement("span");
            sub.className = "status-toast-detail";
            sub.textContent = String(detail);
            toast.appendChild(sub);
        }

        var close = document.createElement("button");
        close.type = "button";
        close.className = "status-toast-close";
        close.setAttribute("aria-label", "Dismiss");
        close.innerHTML = "&times;";
        close.onclick = function () {
            if (toast.parentNode !== null) {
                toast.parentNode.removeChild(toast);
            }
        };
        toast.appendChild(close);

        host.appendChild(toast);

        // textContent everywhere above, never innerHTML, because some of these strings come from a
        // server response body.
        if (kind !== "failed") {
            var life = kind === "warn" ? WARN_DISMISS_MS : OK_DISMISS_MS;
            window.setTimeout(function () {
                if (toast.parentNode !== null) {
                    toast.parentNode.removeChild(toast);
                }
            }, life);
        }

        return toast;
    }

    /* Turn an xhr into a sentence a node operator can act on. This is where "refused" and "never
       arrived" stop looking alike. */
    function describe(xhr, textStatus) {
        if (textStatus === "timeout") {
            return "The server did not answer in time. It may or may not have saved.";
        }

        // jQuery reports a body it could not parse as an ERROR even though the request itself
        // succeeded. Two call sites here post with dataType "json", so this is reachable, and
        // calling it a failure without saying why would be actively misleading.
        if (textStatus === "parsererror") {
            return "The server replied, but the reply could not be read. The change may have been saved.";
        }

        var status = xhr === undefined || xhr === null ? 0 : xhr.status;

        switch (status) {
            case 0:
                // THE distinction this item was raised for.
                return "The request never reached the server. Check the connection and try again.";
            case 400:
                return "The server rejected the request as malformed.";
            case 401:
                return "You are not signed in any more. Sign in again and retry.";
            case 403:
                // Covers both a genuine permission refusal and a missing antiforgery token, which
                // is why the wording does not promise which.
                return "Refused. You may not have permission, or the page may have gone stale -- reload and try again.";
            case 404:
                return "The server has no such address, or the item is already gone.";
            case 409:
                return "That conflicts with the current state. Reload to see where things stand.";
            case 413:
                return "Too large for the server to accept.";
            case 429:
                return "Too many requests too quickly. Wait a moment and retry.";
            default:
                break;
        }

        if (status >= 500) {
            return "The server failed while doing it (" + status + "). It may or may not have been saved.";
        }

        return "The server answered " + status + ".";
    }

    var StatusMessage = {

        /* Success. The call site owns this, because only it knows what happened. */
        ok: function (text, detail) {
            return show("ok", text || "Saved.", detail);
        },

        /* Something worth seeing that is neither a success nor a transport failure. */
        warn: function (text, detail) {
            return show("warn", text, detail);
        },

        /* A REFUSAL THAT ARRIVED AS A 2xx. The global net cannot see these -- as far as jQuery is
           concerned the request succeeded -- so a call site whose endpoint answers "no" with HTTP
           200 and a payload must say so itself. StatementButtonOperation and the bulk import are
           both this shape. */
        refused: function (text, detail) {
            return show("failed", text || "That did not happen.", detail);
        },

        /* Explicit failure reporting, for the rare call that opts out of the global net. Ordinary
           call sites should NOT call this -- see the contract at the top. */
        failed: function (text, xhr, textStatus) {
            return show("failed", text || "That failed.", describe(xhr, textStatus));
        },

        describe: describe,

        /* Opt a single request out of the global net, for a poll or a background refresh where a
           transient failure is not worth a sticky toast:

               $.ajax({ url: ..., statusMessageSilent: true })

           Deliberately per-request rather than a global off switch. */
        isSilent: function (settings) {
            return settings !== undefined && settings !== null && settings.statusMessageSilent === true;
        }
    };

    window.StatusMessage = StatusMessage;

    /* ---------------------------------------------------------------------------------------- */
    /* The global net. Installed on document, so it covers $.get, $.post, $.ajax AND $(el).load(),
       which is how most of this Portal fetches its partials.

       Requests made with { global: false } bypass jQuery's global events entirely and are therefore
       NOT covered -- nothing in this Portal sets that today, and anything that starts to will have
       to report its own failures. */

    function install($) {

        /* Set once the page is actually being torn down. A request the browser cancels because the
           user navigated away arrives here as status 0 with statusText "error", which is
           INDISTINGUISHABLE from a connection that was refused -- so the only honest discriminator
           is whether the page is on its way out, and that has to be observed rather than guessed.

           This started life as a check on document.visibilityState === "hidden", which was wrong in
           the worst possible direction and was caught in the browser rather than by any test: a
           BACKGROUND TAB is hidden too, so an operator who switched tabs while a save was in flight
           got no failure message at all. It suppressed precisely the case this whole item exists to
           surface -- "the request never arrived" -- and it did it silently. */
        var unloading = false;
        $(window).on("beforeunload pagehide", function () {
            unloading = true;
        });

        $(document).ajaxError(function (event, xhr, settings, thrownError) {
            // An abort is not a failure. Navigating away cancels in-flight requests, and reporting
            // those would put a sticky red toast on the screen every time somebody clicks a link.
            // jQuery puts "abort" in statusText; errorThrown carries it too on some paths.
            var jqStatusText = xhr === undefined || xhr === null ? null : xhr.statusText;
            if (jqStatusText === "abort" || thrownError === "abort") {
                return;
            }
            // The page is being torn down and took its in-flight requests with it. Nothing shown
            // now would ever be read. NOT a visibility check -- see the note on `unloading` above.
            if (unloading) {
                return;
            }

            if (StatusMessage.isSilent(settings)) {
                return;
            }

            // ajaxError does NOT receive jQuery's textStatus, so the two non-HTTP outcomes have to
            // be recovered from the xhr itself.
            //
            // A 2xx that still lands here means the transport succeeded and jQuery could not parse
            // the body -- dataType "json" against a non-JSON reply, which two call sites here can
            // produce. Calling that "the request failed" would be a lie in the more dangerous
            // direction, since the write probably happened.
            var recovered;
            if (jqStatusText === "timeout") {
                recovered = "timeout";
            } else if (xhr !== undefined && xhr !== null && xhr.status >= 200 && xhr.status < 300) {
                recovered = "parsererror";
            }

            var where = settings && settings.url ? String(settings.url).split("?")[0] : null;
            var detail = describe(xhr, recovered);
            if (where !== null) {
                detail = detail + " (" + where + ")";
            }

            show("failed", "That did not work.", detail);

            // Keep the console breadcrumb the old per-site handlers provided. Several of them
            // logged and did nothing else, which is exactly the silence being fixed, but the log
            // itself is still useful to whoever is holding the devtools open.
            if (window.console && window.console.log) {
                window.console.log("StatusMessage: AJAX failure", settings && settings.url, xhr);
            }
        });
    }

    if (window.jQuery) {
        install(window.jQuery);
    } else if (window.console && window.console.error) {
        // Loud, because without jQuery the net is not armed and every failure below is silent
        // again. The layout loads jQuery in <head> ahead of this file, so this should be
        // unreachable; if it ever fires, the script order changed.
        window.console.error("StatusMessage: jQuery is not loaded, AJAX failures will NOT be reported.");
    }

}(window, document));
