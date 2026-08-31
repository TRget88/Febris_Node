//< !--Markup -->
//    <div id="TestResultList"></div>
//<div id="LoadingDiv" style="display:none">
//    <h4>Loading...</h4>

var pageSize = 20;
var pageIndex = 0;
var timeOut = false;


$(document).ready(function () {
    GetData();
});
    
$('#Modal').scroll(function () {               
    if ($('#modalContent').innerHeight() <= $(this).scrollTop()+500 && timeOut==false) {            
        GetData();
        timeOut = true;
    }
});

function GetData() {    
    $.ajax({
        type: 'GET',
        url: '/xAPI/StatementResultListPartial',
        data: { "pageindex": pageIndex, "pagesize": pageSize, "providerId": providerId, "locationId": locationId, "professionalId": professionalId, "eduOrgId": eduOrgId},
        dataType: 'json',
        success: function (data) {                 
            var el = $('#TestResult');
            //if (data.statementList.length == 0) {
            if (data.statementDataViewModelList.length == 0) {
                timeOut = true;                
            } else if (data != null) {                
                //for (var i = 0; i < data.statementList.length; i++) {                
                for (var i = 0; i < data.statementDataViewModelList.length; i++) {
                    
                    //This is the test result div logic
                    el.clone().appendTo('#TestResultList');
                    $("#visability").show();

                    var tempName = (data.statementDataViewModelList[i].statement.object.definition.name).split(':');
                    
                    //var nestedName = tempName.split('"');
                    $("#testName").html(tempName[1].replace('}',''));
                    $("#date").html(data.statementDataViewModelList[i].statement.timestamp);
                    var score=0;
                    var duration=0;
                    var timeEstimate=15;
                    var restartCounter=0;
                    var success=0;
                    var complete = 0;
                    var notes = "";
                    if (data.statementDataViewModelList[i].statement.result == null)
                    {
                        score = 0;
                        duration = 0;
                        timeEstimate = 30;
                        restartCounter = 1;
                        success = 0;
                        complete = 0;
                    } else if (data.statementDataViewModelList[i].statement.result.score == null)
                    {
                        score = 0;
                        var tempTime = (data.statementDataViewModelList[i].statement.result.duration).split(':');
                        duration = ((parseFloat(tempTime[0]) * 60) + (parseFloat(tempTime[1])) + (parseFloat(tempTime[2]) / 60));                        
                        timeEstimate = 15;                        
                        success = parseFloat(data.statementDataViewModelList[i].statement.result.success ? 1 : 0);                        
                        complete = parseFloat(data.statementDataViewModelList[i].statement.result.completion ? 1 : 0);
                        if (data.statementDataViewModelList[i].xApiResultExtras != null) {
                            restartCounter = data.statementDataViewModelList[i].xApiResultExtras.restartCount ?? 0;
                            notes = data.statementDataViewModelList[i].xApiResultExtras.notesList;
                        }                        
                    }
                    else
                    {
                        score = data.statementDataViewModelList[i].statement.result.score.raw ?? 0;
                        var tempTime = (data.statementDataViewModelList[i].statement.result.duration).split(':');
                        duration = ((parseFloat(tempTime[0]) * 60) + (parseFloat(tempTime[1])) + (parseFloat(tempTime[2]) / 60));                        
                        timeEstimate = 15;                        
                        success = parseFloat(data.statementDataViewModelList[i].statement.result.success ? 1 : 0);                        
                        complete = parseFloat(data.statementDataViewModelList[i].statement.result.completion ? 1 : 0);
                        if (data.statementDataViewModelList[i].xApiResultExtras != null) {
                            restartCounter = data.statementDataViewModelList[i].xApiResultExtras.restartCount ?? 0;
                            notes = data.statementDataViewModelList[i].xApiResultExtras.notesList;
                        }                        
                    }
                    
                                        
                    $("#RadarChart").html("<div id='StatementResultRadarChart_" + data.statementDataViewModelList[i].statement.id +"'></div>");//+ "' statementId='" + data.professionalTestDataList[i].id + "' score='" + data.statementList[i].result.score.raw + "' time = '" + data.statementList[i].result + "' timeEstimate = '" + 30 + "' restartCounter = '" + 1 + "' success = '" + data.statementList[i].result.success.ToString() + "' completion = '" + data.statementList[i].result.complete.ToString() + "'></div>");
                    drawRadarChart(data.statementDataViewModelList[i].statement.id, score, duration, timeEstimate, restartCounter, success, complete);
                    
                    //need to add in graphics for training/pass/fail
                    if (data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Pass") {
                        $("#icon").html("<div class='glyphicon glyphicon-ok' style='color:green'></div>")
                    }
                    if (data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Not_Pass" || data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Terminated" || data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Initialized") {
                        $("#icon").html("<div class='glyphicon glyphicon-remove' style='color:red'></div>")
                    }
                    if (data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Attempted" || data.statementDataViewModelList[i].statement.verb.id == "https://febr.is/xAPI/VerbDetails/Completed") {
                        $("#icon").html("<div class='glyphicon glyphicon-education' style='color:dodgerblue'></div>")
                    }
                    if (data.statementDataViewModelList[i].statement.verb.id == "http://adlnet.gov/expapi/verbs/voided") {
                        $("#icon").html("<div class='glyphicon glyphicon-remove-sign' style='color:secondary'></div>")
                    }                    
                    var notesList = "";
                                        
                    if (notes != null && notes != undefined) {
                        if (notes.length != null && notes.length != undefined && notes.length != 0) {
                            for (j = 0; j < notes.length; j++) {
                                notesList = notesList + "<div>" + notes[j] + "</div>";
                            }
                        } else {
                            notesList = "<div>There are no notes for this session</div>";
                        }                        
                    } else {
                        notesList = "<div>There are no notes for this session</div>";
                    }                    
                    $("#Notes").html(notesList);                    
                    $("#buttonGroup").html('<button type="button" onclick="ButtonAction(this)" class="btn btn-info" identification="' + data.statementDataViewModelList[i].statement.id + '" submitAction="StatementDetails" data-toggle="tooltip" title="More information" ><span class="glyphicon glyphicon-info-sign"></span></button>');                                               
                    ////add in link for video
                    //if (data.statementList[i].isTest == true) {
                    //    //this is in testbuttonoperation.js -- this obviously needs to change. 
                    //    $("#buttonGroup").html('<button type="button" onclick="StatementButtonAction(this)" class="btn btn-info" identification="' + data.statementList[i].id + '" submitAction="StatementVideoReview" data-toggle="tooltip" title="Review More information and video" ><span class="glyphicon glyphicon-film"></span></button>');                                               
                    //} else {
                    //    $("#buttonGroup").html("Buttons leading to information ")
                    //}                    
                }
                pageIndex++;
                timeOut = false;
            } 
        },
        beforeSend: function () {
            $("#LoadingDiv").show();
        },
        complete: function () {
            $("#LoadingDiv").hide();
        },
        error: function () {
            //alert("Error while retrieving data.");            
            timeOut = true;
        }
    });
}
