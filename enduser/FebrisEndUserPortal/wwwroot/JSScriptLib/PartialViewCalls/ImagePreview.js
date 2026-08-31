function imagepreview(data) {    
    var displayarea = $(data).attr('link');    
    var result = document.getElementById(displayarea);    
    result.src = URL.createObjectURL(data.files[0]);
}