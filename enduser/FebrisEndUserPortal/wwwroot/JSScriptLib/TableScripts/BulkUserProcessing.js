$(function () {
    //passing json data when button is clicked
    $(document).on('click', 'button#exportJsonData', function () {
        /*var data = getAllTableData();*/


        //add in selected occupation to each in the list of        
        //var selectedCohortList = $("#selectedCohortList").val();                
        //var accountType = $("#accountType").val();
               

        //var dataArray = [];
        //var tableData = {};
        //tableData = ProcessData(data);
        ////this changes to headers to match what the model expects
        ////var keyCount = headers.length;

        ////var keyCount = data[0].length;
        ////for (j = 0; j <= data.length - 1; j++) {
        ////    var parsedRow = {};

        ////    for (i = 0; i <= keyCount - 1; i++) {
        ////        switch (i) {
        ////            case 0:
        ////                data[j].IdentificationNumber = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 1:
        ////                data[j].FirstName = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 2:
        ////                data[j].LastName = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 3:
        ////                data[j].EmailAddress = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 4:
        ////                data[j].PhoneNumber = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            //case default:
        ////            //    delete data[j][i];
        ////        }
        ////    }

        ////    dataArray.push(parsedRow);
        ////}

        ////var tableData = {};
        ////tableData = ({ SubmissionList: dataArray });
        ////tableData.SelectedCohortList = selectedCohortList;
        //////tableData.LifecycleStage = lifecycleStage;        
        ////tableData.AccountType = accountType;

        ////var tableData = {};
        ////tableData = ({ SubmissionList: dataArray });
        ////tableData.TagList = tagList;
        ////tableData.LifecycleStage = lifecycleStage;
        ////tableData.LeadRating = leadRating;
        ////tableData.LeadType = leadType;
        ////console.log(tableData);


        ////var bulkDataSubmission = JSON.stringify((tableData), null, 2);
        //var bulkDataSubmission = JSON.stringify(tableData);
        /*var bulkDataSubmission = ProcessData(data);*/
        //var token = gettoken();
        var bulkDataSubmission = ProcessData();
        $.ajax({
            async: false,
            contentType: "application/json; charset=utf-8",
            processData: false,
            traditional: true,
            dataType: "json",
            type: "POST",
            data: bulkDataSubmission,             
            url: "/User/BulkCreatePost",
            beforeSend: function (request) {
                // Antiforgery for a JSON body. Read side: AddAntiforgery(HeaderName) in Startup.
                request.setRequestHeader("RequestVerificationToken", $("[name='__RequestVerificationToken']").val());
            },
            // ROADMAP 20. The old failure handler was alert("Somthing went wrong!" + xhr + param),
            // which concatenates an object and renders literally as "Somthing went wrong![object
            // Object]parsererror" -- a blocking dialog naming neither what failed nor why.
            // Failures are now reported by the global net in StatusMessage.js, which can tell a
            // refusal from a request that never left the browser, so there is no error handler here
            // at all.
            //
            // The server's reply is its own sentence, of the form "3 Were added. 1 Were not added.
            // 1 had duplicate email addresses. 2 cohort links made with users." PARTIAL FAILURE IS
            // THE NORMAL OUTCOME of a bulk paste, and this is the only place those counts are ever
            // shown, so the summary is surfaced verbatim and in the amber tone that has to be read
            // rather than the green one that can be ignored. The counts are deliberately NOT parsed
            // to pick a tone: sniffing a server-formatted string for "0 Were not added" would start
            // lying silently the day somebody rewords it.
            success: function (result) {
                window.StatusMessage.warn("Bulk create finished -- check the counts.", result);
            }//,
            //beforeSend: function () {
            //    //console.log(data);
            //    console.log(bulkInput);
            //}
        });
        return false;
    })

    // CSV alternative to the paste flow (ROADMAP 17): posts the chosen file plus the same
    // accountType and cohort selections to User/BulkCreateCsvPost as multipart form data.
    // The action parses via ICsvUserImporter and reuses the exact create path as the paste
    // flow. Antiforgery and outcome reporting mirror the paste handler above -- failures are
    // reported by the global net in StatusMessage.js, success counts surface in amber because
    // partial failure is the normal outcome of a bulk import.
    $(document).on('click', 'button#uploadCsvData', function () {
        var fileInput = document.getElementById('csvUploadFile');
        if (!fileInput || fileInput.files.length === 0) {
            window.StatusMessage.warn("No CSV selected.", "Choose a .csv file first, then upload.");
            return;
        }
        var formData = new FormData();
        formData.append("file", fileInput.files[0]);
        formData.append("accountType", $("#accountType").val());
        var cohorts = $("#selectedCohortList").val() || [];
        for (var i = 0; i < cohorts.length; i++) {
            if (cohorts[i]) {
                formData.append("selectedCohorts", cohorts[i]);
            }
        }
        $.ajax({
            type: "POST",
            url: "/User/BulkCreateCsvPost",
            data: formData,
            contentType: false,
            processData: false,
            beforeSend: function (request) {
                // Antiforgery for a multipart body. Read side: AddAntiforgery(HeaderName) in Startup.
                request.setRequestHeader("RequestVerificationToken", $("[name='__RequestVerificationToken']").val());
            },
            success: function (result) {
                window.StatusMessage.warn("CSV import finished -- check the counts.", result);
            }
        });
    })

    //function getAllTableData() {
    //    var table = document.getElementById('excelDataTable');
    //    var data = [];

    //    // Iterate over the rows of the table
    //    for (var i = 0; i < table.rows.length; i++) {
    //        var row = table.rows[i];
    //        var rowData = [];

    //        // Iterate over the cells of each row
    //        for (var j = 0; j < row.cells.length; j++) {
    //            var cell = row.cells[j];
    //            rowData.push(cell.innerText);
    //        }

    //        data.push(rowData);
    //    }

    //    return data;
    //}
});


function ProcessData() {
    var data = getAllTableData();
    var dataArray = [];
    var selectedCohortList = $("#selectedCohortList").val();
    var accountType = $("#accountType").val();
    //this changes to headers to match what the model expects
    //var keyCount = headers.length;

    var keyCount = data[0].length;
    for (j = 0; j <= data.length - 1; j++) {
        var parsedRow = {};

        for (i = 0; i <= keyCount - 1; i++) {
            switch (i) {
                case 0:
                    parsedRow.IdentificationNumber = (data[j])[i]
                    //data[j].IdentificationNumber = (data[j])[i]
                    //delete data[j][i];
                    break;
                case 1:
                    parsedRow.FirstName = (data[j])[i]
                    //data[j].FirstName = (data[j])[i]
                    //delete data[j][i];
                    break;
                case 2:
                    parsedRow.LastName = (data[j])[i]
                    //data[j].LastName = (data[j])[i]
                    //delete data[j][i];
                    break;
                case 3:
                    parsedRow.EmailAddress = (data[j])[i]
                    //data[j].EmailAddress = (data[j])[i]
                    //delete data[j][i];
                    break;
                case 4:
                    parsedRow.PhoneNumber = (data[j])[i]
                    //data[j].PhoneNumber = (data[j])[i]
                    //delete data[j][i];
                    break;
                //case default:
                //    delete data[j][i];
            }
        }

        dataArray.push(parsedRow);
    }    
    var tableData = {};
    tableData = ({ SubmissionList: dataArray });
    tableData.SelectedCohortList = selectedCohortList;    
    tableData.AccountType = accountType;
    var bulkDataSubmission = JSON.stringify(tableData);


    return bulkDataSubmission;

    function getAllTableData() {
        var table = document.getElementById('excelDataTable');
        var data = [];

        // Iterate over the rows of the table
        for (var i = 0; i < table.rows.length; i++) {
            var row = table.rows[i];
            var rowData = [];

            // Iterate over the cells of each row
            for (var j = 0; j < row.cells.length; j++) {
                var cell = row.cells[j];
                rowData.push(cell.innerText);
            }

            data.push(rowData);
        }

        return data;
    }

}

$(function () {
    //passing json data when button is clicked
    $(document).on('click', 'button#exportRemovalJsonData', function () {
        //var data = getAllTableData();
       
        //var selectedCohortList = $("#selectedCohortList").val();
        //var accountType = $("#accountType").val();


        //var dataArray = [];

        //dataArray = ProcessData(data);

        ////this changes to headers to match what the model expects
        ////var keyCount = headers.length;

        ////var keyCount = data[0].length;
        ////for (j = 0; j <= data.length - 1; j++) {
        ////    var parsedRow = {};

        ////    for (i = 0; i <= keyCount - 1; i++) {
        ////        switch (i) {
        ////            case 0:
        ////                data[j].IdentificationNumber = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 1:
        ////                data[j].FirstName = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 2:
        ////                data[j].LastName = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 3:
        ////                data[j].EmailAddress = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            case 4:
        ////                data[j].PhoneNumber = (data[j])[i]
        ////                //delete data[j][i];
        ////                break;
        ////            //case default:
        ////            //    delete data[j][i];
        ////        }
        ////    }

        ////    dataArray.push(parsedRow);
        ////}


        //var tableData = {};
        //tableData = ({ SubmissionList: dataArray });
        //tableData.SelectedCohortList = selectedCohortList;
        ////tableData.LifecycleStage = lifecycleStage;        
        //tableData.AccountType = accountType;


        ////var bulkInput = JSON.stringify((tableData), null, 2);
        //var bulkDataSubmission = JSON.stringify(tableData);

        //var bulkInput = JSON.stringify(({ HealthCareProfessionals: data }), null, 2);
        //dumps data into text box
        //$('textarea#jsonDataDump').val(jsonString);

        //Post to controller

        //var postData = { 'bulkInput': bulkInput };
        var bulkDataSubmission = ProcessData();

        $.ajax({
            async: false,
            contentType: "application/json; charset=utf-8",
            processData: false,
            traditional: true,
            dataType: "json",
            type: "POST",
            data: bulkDataSubmission,
            url: "/User/BulkRemovalPost",
            beforeSend: function (request) {
                // Antiforgery for a JSON body. Read side: AddAntiforgery(HeaderName) in Startup.
                request.setRequestHeader("RequestVerificationToken", $("[name='__RequestVerificationToken']").val());
            },
            // ROADMAP 20. The old failure handler was alert("Somthing went wrong!" + xhr + param),
            // which concatenates an object and renders literally as "Somthing went wrong![object
            // Object]parsererror" -- a blocking dialog naming neither what failed nor why.
            // Failures are now reported by the global net in StatusMessage.js, which can tell a
            // refusal from a request that never left the browser, so there is no error handler here
            // at all.
            //
            // The server's reply is its own sentence, of the form "3 Were added. 1 Were not added.
            // 1 had duplicate email addresses. 2 cohort links made with users." PARTIAL FAILURE IS
            // THE NORMAL OUTCOME of a bulk paste, and this is the only place those counts are ever
            // shown, so the summary is surfaced verbatim and in the amber tone that has to be read
            // rather than the green one that can be ignored. The counts are deliberately NOT parsed
            // to pick a tone: sniffing a server-formatted string for "0 Were not added" would start
            // lying silently the day somebody rewords it.
            success: function (result) {
                window.StatusMessage.warn("Bulk removal finished -- check the counts.", result);
            }//,
            //beforeSend: function () {
            //    //console.log(data);
            //    console.log(bulkInput);
            //}
        });       
        return false;

    })
    //function getAllTableData() {
    //    var table = document.getElementById('excelDataTable');
    //    var data = [];

    //    // Iterate over the rows of the table
    //    for (var i = 0; i < table.rows.length; i++) {
    //        var row = table.rows[i];
    //        var rowData = [];

    //        // Iterate over the cells of each row
    //        for (var j = 0; j < row.cells.length; j++) {
    //            var cell = row.cells[j];
    //            rowData.push(cell.innerText);
    //        }

    //        data.push(rowData);
    //    }

    //    return data;
    //}
});