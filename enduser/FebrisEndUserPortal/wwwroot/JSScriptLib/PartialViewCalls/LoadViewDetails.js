modelId = null;
const requestQueue = [];
let isProcessing = false;
const setBatchSize = 4;
function addToRequestQueue(url, options) {
    requestQueue.push({ url, options });
}
if (document.getElementById('modelId') !== null) {
    modelId = document.getElementById('modelId').value
}
function loadPartialDetails() {
    var url = window.location.pathname.split("/");
    var controllerName = url[1];
    var loadingElements = true;
    var cycle = 0;
    while (loadingElements) {
        if (document.getElementById('Element' + cycle.toString()) !== null) {// undefined) {
            var temp = document.getElementById('Element' + cycle.toString()).value
            var divId = "#Division" + cycle.toString();
            singleRequest(controllerName, temp, modelId, divId);
        } else {
            break;
        }
        cycle++;
    }
    processRequests(requestQueue, setBatchSize);
}
function singleRequest(controllerName, variableName, modelId, divId) {
    var urlBuilder = "../Widget/LoadPartialDetail?modelName=" + controllerName + "&variableName=" + variableName + "&modelId=" + modelId;
    addToRequestQueue(urlBuilder, divId);
}
$(document).ready(function () {
    loadPartialDetails();
});
// Function to process a batch of requests
function processBatch(batch) {
    const promises = batch.map(request =>
        new Promise(resolve => {
            $(request.options).load(request.url, () => resolve());
        })
    );
    return Promise.all(promises);
}
// Function to process requests in batches
function processRequests(requests, batchSize) {
    const batches = [];
    for (let i = 0; i < requests.length; i += batchSize) {
        const batch = requests.slice(i, i + batchSize);
        batches.push(batch);
    }
    let promise = Promise.resolve();
    for (const batch of batches) {
        //promise = promise.then(() => processBatch(batch));
        promise = promise.then(() => processBatch(batch)).catch(error => {
            console.error('Error occurred during batch processing:', error);
        });
    }
    return promise;
}




//modelId = null;
////modelLimiter = null;
////limiterId = null;


//if (document.getElementById('modelId') != undefined) {
//    modelId = document.getElementById('modelId').value
//}// else if (document.getElementById('modelLimiter') != undefined) {
////    modelLimiter = document.getElementById('modelLimiter').value
////} else if (document.getElementById('limiterId') != undefined) {
////    limiterId = document.getElementById('limiterId').value
////}

//function loadPartialDetails() {    
//    var url = window.location.pathname.split("/");
//    var controllerName = url[1];    
//    var loadingElements = true;
//    var cycle = 0;    
//    while (loadingElements) {        
//        if (document.getElementById('Element' + cycle.toString()) != undefined)
//        {
//            var temp = document.getElementById('Element' + cycle.toString()).value            
//            $("#Division" + cycle.toString()).load("../Widget/LoadPartialDetail?modelName=" + controllerName + "&variableName=" + temp + "&modelId=" + modelId)
//        } else {
//            loadingElements 
//            break;
//        }
//        cycle++;        
//    }    
//}
//$(document).ready(function () {
//    loadPartialDetails();
//});
