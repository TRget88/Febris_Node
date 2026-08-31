//< !--Markup -->
//    <div id="TestResultList"></div>
//<div id="LoadingDiv" style="display:none">
//    <h4>Loading...</h4>

//the page stops loading after two cycling though. this has to be something with the triggering of the loading

var pageSize = 20;
var pageIndex = 0;
var timeOut = false;
var resultsArray = [];

$(document).ready(function () {
    InitalizeResults();
    loadData();
});


$('#Modal').scroll(function () {
    if ($('#modalContent').innerHeight() <= $(this).scrollTop() + 500 && timeOut == false) {
        loadData();
        timeOut = true;
    }
});


function InitalizeResults() {
    var list = document.getElementById("testData");
    resultsArray = JSON.parse(list.innerHTML);    
}


function loadData() {
    var el = $('#TestResult');
    if ((pageSize * pageIndex) >= resultsArray.length) {
        timeOut = true;
    }
    else if ((pageSize * pageIndex) <= resultsArray.length) {
        for (var i = pageIndex * pageSize; i < (pageSize * pageIndex) + pageSize; i++) {
            //This is the test result div logic
            el.clone().appendTo('#TestResultList');
            $("#visability").show();            
            $("#testName").html(resultsArray[i].testBase.testName);            
            $("#date").html(resultsArray[i].startTime);
            var tempStart = Date.parse(resultsArray[i].startTime);
            var tempEnd = Date.parse(resultsArray[i].endTime);
            var tempTime = Math.floor((tempEnd - tempStart) / 60000);
            $("#RadarChart").html("<div id='TestResultRadarChart_" + resultsArray[i].id + "' testId='" + resultsArray[i].id + "' score='" + resultsArray[i].score + "' time = '" + tempTime + "'></div>");
            drawRadarChart(resultsArray[i].id, resultsArray[i].score ?? 0, tempTime);

            //need to add in graphics for training/pass/fail
            if (resultsArray[i].pass == true && resultsArray[i].isTest == true) {
                $("#icon").html("<div class='glyphicon glyphicon-ok' style='color:green'></div>")
            }
            if (resultsArray[i].pass != true && resultsArray[i].isTest == true) {
                $("#icon").html("<div class='glyphicon glyphicon-remove' style='color:red'></div>")
            }
            if (resultsArray[i].isTest == false) {
                $("#icon").html("<div class='glyphicon glyphicon-education' style='color:dodgerblue'></div>")
            }

            //add in link for video
            if (resultsArray[i].isTest == true) {
                //change this to work with data set
                //$("#buttonGroup").load('Buttons/GenericButtons/Test/_ProfessionalTestReviewButtonPartial.html', new IndividualButtonPartial { id: data.professionalTestDataList[i].id });
                //$("#buttonGroup").load("@await Html.PartialAsync(Buttons/GenericButtons/Test/_ProfessionalTestReviewButtonPartial, new IndividualButtonPartial { Id = " + data.professionalTestDataList[i].id + " })")
                //$("#buttonGroup").load("@Html.Partial('Buttons/GenericButtons/Test/_ProfessionalTestReviewButtonPartial.html')")
                //var buttonPartial = new IndividualButtonPartial(){ id: resultsArray[i].professionalTestData.id}
                //$("#buttonGroup").load("@Url.Action('Buttons/GenericButtons/Test/_ProfessionalTestReviewButtonPartial.html', new IndividualButtonPartial { id:" + resultsArray[i].id+" })");
            } else {
                $("#buttonGroup").html("Buttons leading to information ")
            }            
        }        
        pageIndex++;
        timeOut = false;
    }
};
       
