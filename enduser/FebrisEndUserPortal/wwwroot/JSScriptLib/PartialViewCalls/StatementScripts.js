function loadFileUrl(data) {
    // The stored link is minted by the launcher as an absolute URL (LauncherLogic), e.g.
    // https://febr.is/widget/videoloader?videoName=... . The old split(".com") produced
    // "..undefined" for every such link because the minted host has no ".com". The URL is
    // parsed instead and served as the node-relative path, which works for any host (ROADMAP 17).
    var urlLink = $(data).attr('url');
    var parsed = new URL(urlLink, window.location.origin);
    var relativeUrl = parsed.pathname + parsed.search;
     
    var iframeContainer = document.getElementById('iframeContainer')
    try {

        var attributeList = iframeContainer.getAttributeNames()
        if (attributeList.includes('hidden')) {
            iframeContainer.removeAttribute('hidden')
        }
    } catch
    { }   
    var loadingArea = document.getElementById('fileUrlLoad');
    loadingArea.src = relativeUrl;   
}