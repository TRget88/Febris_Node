$(document).ready(function () {
    var status = "";
    if (document.getElementById('isLockedOut') != undefined) {
        status = document.getElementById('isLockedOut').value
        var route = '/Widget/LockedAccountModal'
        displayStatusModal(route);
    }
    //if (status != "") {

    //}
});