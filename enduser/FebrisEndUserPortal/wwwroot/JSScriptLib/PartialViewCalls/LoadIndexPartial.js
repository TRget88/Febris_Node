

function loadIndex() {    
    var url = window.location.pathname.split("/");
    var controller = url[1];    
    $("#IndexPartial").load("/" + controller + "/IndexPartial")    
}

window.onload = loadIndex();
//loadIndex();
