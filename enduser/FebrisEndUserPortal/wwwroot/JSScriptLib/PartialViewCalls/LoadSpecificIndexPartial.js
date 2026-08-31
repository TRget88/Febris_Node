var inputVariable = document.getElementById("inputVariable");

function loadIndex() {    
    var url = window.location.pathname.split("/");
    var controller = url[1];    
    $("#IndexPartial").load("/" + controller + "/SpecificIndexPartial?input=" + inputVariable.value)    
}

//window.onload = function init() {    
//    loadIndex();
//};
loadIndex();
