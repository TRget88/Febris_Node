var ajaxOnLoad = (function () {
    var ajaxOnLoad = {};
    var onLoadQueue = [];

    ajaxOnLoad.onLoad = function (fn) {
        onLoadQueue.push(fn);
    }

    ajaxOnLoad.fireOnLoad = function () {
        while (onLoadQueue.length > 0) {
            var fn = onLoadQueue.shift();
            fn();
        }
    }

    window.ajaxOnLoad = ajaxOnLoad;
    return ajaxOnLoad;
})();