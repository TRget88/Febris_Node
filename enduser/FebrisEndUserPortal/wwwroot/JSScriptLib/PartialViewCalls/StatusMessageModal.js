function displayStatusModal(status) {
    // Get the modal
    var modal = document.getElementById('Modal');
    // Get the <span> element that closes the modal
    var span = document.getElementsByClassName("close")[0];

    modal.style.display = "block";

    // When the user clicks on <span>(x), close the modal
    span.onclick = function () {
        modal.style.display = "none";
    };

    // When the user clicks anywhere outside of the modal, close it
    window.onclick = function (event) {
        if (event.target === modal) {
            modal.style.display = "none";
        }
    };

    $.ajax({
        url: '/Widget/StatusMessageModal',
        async: true,
        data: 'input=' + status,
        success: function (data) {
            $("#modalContent").html(data);
        }
    });
};


$(document).ready(function () {
    var status = "";
    if (document.getElementById('statusMessage') != undefined) {
        status = document.getElementById('statusMessage').value
        displayStatusModal(status);
    }    
    //if (status != "") {
        
    //}
});