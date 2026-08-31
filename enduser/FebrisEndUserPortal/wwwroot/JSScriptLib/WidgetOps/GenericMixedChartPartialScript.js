//Full model processing
function ProcessGenericMixedChart(genericMixedChartData) {
    const defaultValue = '';
    /*Breaks down to*/
    const description = genericMixedChartData.description;
    const genericChartList = genericMixedChartData.genericChartList;
    const title = genericMixedChartData.title;
    const idToUse = genericMixedChartData.idToUse;
    BuildCharts(genericChartList);
    function BuildCharts(genericChartListInput) {
        var lineChartHoldingArea = [];
        var holdingArea = [];
        var chartConfigDataSets = [];
        //Check for already referenced line chart (for combining)
        var numberOfGenericChartsInList = genericChartListInput.length;
        //console.log(JSON.stringify(genericChartListInput));
        for (var i = 0; numberOfGenericChartsInList > i; i++) {

            if (genericChartListInput[i].chartType === 'undefined' ||
                genericChartListInput[i].chartType === undefined) {
                ///It will not be added to any list
            }
            else if (
                (genericChartListInput[i].chartType !== 'undefined' && genericChartListInput[i].chartType !== undefined) &&
                (genericChartListInput[i].chartType === 100 || genericChartListInput[i].chartType === '100')) {
                lineChartHoldingArea.push(genericChartListInput[i])
            }
            else {
                holdingArea.push(genericChartListInput[i]);
            }
        }

        //process linechart data into one line chart
        if (lineChartHoldingArea.length > 0) {
            var tempdataset = ProcessCompoudGenericChartList(lineChartHoldingArea);
            chartConfigDataSets.push(tempdataset);
        }

        //process all other chart data        
        if (holdingArea.length > 0) {
            for (var k = 0; holdingArea.length > k; k++) {
                var tempdataset2 = ProcessCompoudGenericChartList(holdingArea[k]);
                chartConfigDataSets.push(tempdataset2);
            }
        }

        ///Publish charts
        for (var j = 0; chartConfigDataSets.length > j; j++) {
            PopulateChartCanvas(chartConfigDataSets[j], idToUse);
        }
    }
    function ProcessCompoudGenericChartList(compoundGenericChartList) {
        var output = {};
        var entryHoldingArea = [];
        var labels = [];
        var chartType = "";
        var options = {};
        var index = 0;
        if (compoundGenericChartList.length > 0) {
            //var subtitle = compoundGenericChartList[0].subtitle ?? defaultValue;
            //var subIdToUse = compoundGenericChartList[0].subIdToUse;
            chartType = ConvertChartEnumToString(compoundGenericChartList[0].chartType);

            for (var i = 0; compoundGenericChartList.length > i; i++) {
                index = i;
                var processTempSingleConfigDatasetEntry = ProcessSingleChartList(compoundGenericChartList[i], index);
                entryHoldingArea.push(processTempSingleConfigDatasetEntry.datasetEntry);
                if (labels.length > 0) {
                    labels = PushUniqueAndSort(labels, processTempSingleConfigDatasetEntry.labels);
                } else {
                    labels = processTempSingleConfigDatasetEntry.labels;
                }
            }
        }
        else {
            chartType = ConvertChartEnumToString(compoundGenericChartList.chartType);
            var processSingleConfigDatasetEntry = ProcessSingleChartList(compoundGenericChartList, index);
            //remove properties that could cause conflicts
            //if (
            //    processSingleConfigDatasetEntry.labels === undefined ||
            //    processSingleConfigDatasetEntry.labels.length < 1
            //) {
            //    //console.log("ProcessCompoudGenericChartList Removing the label property from dataset entry");
            //    delete processSingleConfigDatasetEntry.datasetEntry.label;
            //}
            //if (
            //    processSingleConfigDatasetEntry.datasetEntry.type !== 'line'
            //) {

            //    //console.log("Deleting type from dataset because the chart type is not line");
            //    delete processSingleConfigDatasetEntry.datasetEntry.type;
            //}
            entryHoldingArea.push(processSingleConfigDatasetEntry.datasetEntry);
            labels = processSingleConfigDatasetEntry.labels;
        }

        options = BuildChartConfigOptions(chartType);
        console.log("Built Options: " + JSON.stringify(options));
        var dataObject = CreateChartDataObject(labels, entryHoldingArea);
        output = CreateConfigObject(chartType, dataObject, options);
        //if (labels < 1) {
        //    delete output.labels;
        //}        
        return output;
    }
    function ProcessSingleChartList(singleChart, index = 0) {
        var output = {};
        var subtitle = singleChart.subtitle ?? defaultValue;
        var subIdToUse = singleChart.subIdToUse;
        var chartType = ConvertChartEnumToString(singleChart.chartType);
        var genericChartEntryList = singleChart.genericChartEntryList;
        var colorPallet = GenericChartStyling(chartType, index);
        var dataAndLabelData = BuildSingleDataSetEntry(genericChartEntryList, chartType);
        var datasetEntry = CreateChartConfigDatasetEntry(chartType, subtitle, dataAndLabelData.quantityArray, colorPallet.setBackgroundColor, colorPallet.setBorderColor);
        var labels = []
        if (dataAndLabelData.labelArray !== undefined && dataAndLabelData.labelArray.length > 0) {
            labels = dataAndLabelData.labelArray
        }
        //else {
        //    
        //    labels = undefined;
        //}
        output = { datasetEntry: datasetEntry, labels: labels };
        //console.log("Output for ProcessSingleChartList: " + JSON.stringify(output));
        return output;
    }
    function BuildSingleDataSetEntry(genericChartEntryListInput, chartTypeString) {
        var tempDataList = {
            quantityArray: [],
            labelArray: []
        };

        if (chartTypeString === 'line') {
            $.each(genericChartEntryListInput, function (index, value) {
                var tempDataEntry = { x: value.label, y: value.quantity };
                tempDataList.quantityArray.push(tempDataEntry);
                tempDataList.labelArray.push(value.label);
            });
        }
        else if (chartTypeString === 'pie') {
            $.each(genericChartEntryListInput, function (index, value) {
                tempDataList.quantityArray.push(value.quantity);
                tempDataList.labelArray.push(value.label);
            });
        }
        else if (chartTypeString === "bar") {
            $.each(genericChartEntryListInput, function (index, value) {
                tempDataList.quantityArray.push(value.quantity);
                tempDataList.labelArray.push(value.label);
            });
        }
        else if (chartTypeString === "radar") {
            $.each(genericChartEntryListInput, function (index, value) {
                tempDataList.quantityArray.push(value.quantity);
                tempDataList.labelArray.push(value.label);
            });
        }
        else if (chartTypeString === "bubble") {
            $.each(genericChartEntryListInput, function (index, value) {
                tempDataList.quantityArray.push(value.quantity);
                tempDataList.labelArray.push(value.label);
            });
        }

        return tempDataList;
    }
    function BuildChartConfigOptions(chartTypeString) {
        var options = CreateChartConfigOptions(true);
        if (chartTypeString === 'line') {
            options.scales = CreateNestedScales();
            //options.legend = CreateChartLegend();
            //options.plugins = CreateChartLegend();
        }
        else if (chartTypeString === 'pie') {
            //options.legend = CreateChartLegend();
            //options.plugins = CreateChartLegend();
        }
        else if (chartTypeString === "bar") {
            options.scales = CreateNestedScales();
        }
        //else if (chartTypeString === "radar") {

        //}
        //else if (chartTypeString === "bubble") {

        //}
        else {
            options.scales = CreateNestedScales();
        }

        return options;
    }
    function PopulateChartCanvas(configData, containerId) {
        //console.log("PopulateChartCanvas <ContainerID> for <ConfigData>");
        //console.log(JSON.stringify(containerId));
        //console.log(JSON.stringify(configData));

        //Changed to a table so it can be broken up more effectively
        const trElement = document.getElementById(containerId);
        const tdElement = document.createElement('td');
        const canvas = document.createElement('canvas');
        tdElement.appendChild(canvas);
        trElement.appendChild(tdElement);
        const ctx = canvas.getContext('2d');
        const chart = new Chart(ctx, configData);
    }
    ///-------------------------------------------------------------------------------------------------------------
    //Helpers
    function ConvertChartEnumToString(localData) {
        var chartType = "";
        if (localData === 100 || localData === '100') {
            chartType = 'line';
        }
        else if (localData === 200 || localData === '200') {
            chartType = 'pie';
        }
        else if (localData === 300 || localData === '300') {
            chartType = 'bar';
        }
        else if (localData === 400 || localData === '400') {
            //chartType = "radar";
            chartType = 'radar';
        }
        else if (localData === 500 || localData === '500') {
            //chartType = "bubble";
            chartType = 'bubble';
        }
        else {
            //break;
        }
        return chartType
    }
    function GenericChartStyling(chartTypeString, index = 0) {
        //var
        const colors = [
            "#26B99A",
            "#455C73",
            "#9B59B6",
            "#BDC3C7",
            "#3498DB",
            "#F39C12",
            "#E74C3C",
            "#1ABC9C",
            "#8E44AD",
            "#ECF0F1"
        ];
        var opacity = 0.3;
        ///Logic
        var borderColorOutput = SetChartColor(chartTypeString);
        var backgroundColorOutput = Transparentize(borderColorOutput);
        const output = {
            setBorderColor: borderColorOutput,
            setBackgroundColor: backgroundColorOutput
        };
        return output;
        ///Methods
        function SetChartColor(chartType) {
            //var colorPalletToUse = { pallet: colors, opacity: opacity };        
            if (chartType === "line") {
                if (index !== undefined) {
                    return colors[index];
                }
            }
            else if (chartType === "pie") {
                opacity = .9;
                return colors;
            }
            else if (chartType === "bar") {
                opacity = .9;
                if (index !== undefined) {
                    return colors[index];
                }

            }
            else if (chartType === "radar") {
                if (index !== undefined) {
                    return colors[index];
                }
            }
            else if (chartType === "bubble") {
                if (index !== undefined) {
                    return colors[index];
                }
            }
            else {
                return colors;
            }
        }
        function Transparentize(hexColor) {
            if (Array.isArray(hexColor)) {
                // Handle array of hex colors
                return hexColor.map(color => TransparentizeSingle(color));
            } else {
                // Handle single hex color
                return TransparentizeSingle(hexColor);
            }
        }
        function TransparentizeSingle(hexColor) {
            const normalizedHexColor = hexColor.startsWith('#') ? hexColor.slice(1) : hexColor;

            const red = parseInt(normalizedHexColor.substr(0, 2), 16);
            const green = parseInt(normalizedHexColor.substr(2, 2), 16);
            const blue = parseInt(normalizedHexColor.substr(4, 2), 16);
            const alpha = opacity >= 0 && opacity <= 1 ? opacity : 1;

            return `rgba(${red}, ${green}, ${blue}, ${alpha})`;

            //// Remove the '#' symbol if present
            //if (hexColor.startsWith('#')) {
            //    hexColor = hexColor.slice(1);
            //}

            //// Parse the hex color components
            //const red = parseInt(hexColor.substr(0, 2), 16);
            //const green = parseInt(hexColor.substr(2, 2), 16);
            //const blue = parseInt(hexColor.substr(4, 2), 16);

            //// Convert the opacity value to the range [0, 1]
            //const alpha = opacity >= 0 && opacity <= 1 ? opacity : 1;

            //// Create the RGBA color string
            //const rgbaColor = `rgba(${red}, ${green}, ${blue}, ${alpha})`;

            //return rgbaColor;
        }
    }
}
function CreateConfigObject(type, data, options) {
    var output = {
        type: type,
        data: data,
        options: options
    }
    return output;
}
function CreateChartDataObject(labels, datasets = []) {
    var output = {
        labels: labels,
        datasets: datasets
    }
    return output;
}
function CreateChartConfigDatasetEntry(type, label, data, backgroundColor, borderColor) {
    var output = {
        label: label,
        data: data,
        backgroundColor: backgroundColor,
        borderColor: borderColor,
        type: type,
        fill: true,
        borderWidth: 1//,
        /*tension: 0.1*/
    }
    return output;
}
///Option Builders
function CreateChartConfigOptions(responsive) {
    var output = {
        responsive: responsive,
        //scales: scales
    }
    return output;
}
function CreateNestedScales() {
    var output = {
        //scales: {
        //y: {
        //    beginAtZero: true,
        //},
        //},
        yAxes: [{
            ticks: {
                beginAtZero: true
            }
        }]
    }
    return output;
}
function CreateChartLegend() {
    var output = {
        display: true,
        position: 'bottom',
    }
    //var output = {
    //    legend: {
    //        display: true,
    //        position: 'bottom',
    //    },
    //}//,
    //legend: {
    //    position: 'top',
    //}//,
    //title: {
    //    display: true,
    //    text: 'Chart.js Line Chart'
    //}
    /*}*/
    return output;
}
//function PushUniqueAndSort(array, newValue, ascending = true) {
//    console.log("array 1: " + JSON.stringify(array) + " new Value: " + JSON.stringify(newValue));

//    // Flatten the new value array and combine it with the existing array
//    const newArray = [...array, ...newValue.flat()];

//    // Remove duplicates from the combined array
//    const uniqueArray = [...new Set(newArray)];

//    // Sort the unique array in ascending or descending order
//    uniqueArray.sort((a, b) => {
//        const aValue = typeof a === 'string' ? a : String(a);
//        const bValue = typeof b === 'string' ? b : String(b);
//        return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
//    });

//    console.log("New Array: " + JSON.stringify(uniqueArray));
//    return uniqueArray;
//}
//function PushUniqueAndSortDate(array, newValue, ascending = true) {
//    console.log("array 1: " + JSON.stringify(array) + " new Value: " + JSON.stringify(newValue));

//    // Flatten the new value array and combine it with the existing array
//    const newArray = [...array, ...newValue.flat()];

//    // Remove duplicates from the combined array
//    const uniqueArray = [...new Set(newArray)];

//    // Sort the unique array in ascending or descending order
//    uniqueArray.sort((a, b) => {
//        const aValue = a instanceof Date ? a.getTime() : a;
//        const bValue = b instanceof Date ? b.getTime() : b;
//        return ascending ? aValue - bValue : bValue - aValue;
//    });

//    console.log("New Array: " + JSON.stringify(uniqueArray));
//    return uniqueArray;
//}

//function PushUniqueAndSort(array, newValue, ascending = true) {
//    console.log("array 1: " + JSON.stringify(array) + " new Value: " + JSON.stringify(newValue));

//    // Flatten the new value array to extract the values
//    const flattenedNewValue = Array.isArray(newValue) ? newValue.flat() : [newValue];

//    // Check if any of the flattened new values already exist in the array
//    if (flattenedNewValue.some((value) => array.includes(value))) {
//        return array; // Return the original array without any changes
//    }

//    // Create a new array by concatenating the original array and the flattened new values
//    const newArray = [...array, ...flattenedNewValue];

//    // Sort the new array in ascending or descending order
//    newArray.sort((a, b) => {
//        const aValue = typeof a === 'string' ? a : String(a);
//        const bValue = typeof b === 'string' ? b : String(b);
//        return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
//    });
//    console.log("New Array: " + JSON.stringify(newArray));
//    return newArray;
//}

//function PushUniqueAndSort(array, newValue, ascending = true) {
//    console.log("array 1: " + JSON.stringify(array) + " new Value: " + JSON.stringify(newValue));

//    // Check if the new value already exists in the array
//    if (array.includes(newValue)) {
//        return array; // Return the original array without any changes
//    }

//    const newArray = [...array, newValue];

//    // Sort the new array in ascending or descending order
//    newArray.sort((a, b) => {
//        const aValue = typeof a === 'string' ? a : String(a);
//        const bValue = typeof b === 'string' ? b : String(b);
//        return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
//    });

//    console.log("New Array: " + JSON.stringify(newArray));

//    return newArray;

//    //// Convert the array to a Set to remove duplicates
//    //const set = new Set(array);

//    //// Convert the Set back to an array
//    //const uniqueArray = Array.from(set);

//    //// Sort the array in ascending or descending order
//    ////uniqueArray.sort((a, b) => ascending ? a.localeCompare(b) : b.localeCompare(a));
//    //uniqueArray.sort((a, b) => {
//    //    const aValue = typeof a === 'string' ? a : String(a);
//    //    const bValue = typeof b === 'string' ? b : String(b);
//    //    return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
//    //});


//    //// Push the new value to the array
//    //uniqueArray.push(newValue);

//    //// Sort the array again to maintain the proper order
//    ////uniqueArray.sort((a, b) => ascending ? a.localeCompare(b) : b.localeCompare(a));
//    //uniqueArray.sort((a, b) => {
//    //    const aValue = typeof a === 'string' ? a : String(a);
//    //    const bValue = typeof b === 'string' ? b : String(b);
//    //    return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
//    //});

//    //console.log("New Unique Array: " + JSON.stringify(uniqueArray));
//    //return uniqueArray;
//}

///Helpers
function CompareByX(a, b) {
    const valueA = isNaN(a.x) ? new Date(a.x) : Number(a.x);
    const valueB = isNaN(b.x) ? new Date(b.x) : Number(b.x);

    return valueA - valueB;
}
function CompareByXDate(a, b) {
    const dateA = new Date(a.x);
    const dateB = new Date(b.x);

    return dateA - dateB;
}
function CompareByXString(a, b) {
    const valueA = isNaN(a.x) ? a.x : Number(a.x);
    const valueB = isNaN(b.x) ? b.x : Number(b.x);

    if (typeof valueA === 'string' && typeof valueB === 'string') {
        return valueA.localeCompare(valueB); // Compare as strings
    } else {
        return valueA - valueB; // Compare as numbers
    }
}
function PushUniqueAndSort(array, newValue, ascending = true) {
    //console.log("array 1: " + JSON.stringify(array) + " new Value: " + JSON.stringify(newValue));
    // Flatten the new value array and combine it with the existing array
    const newArray = [...array, ...newValue.flat()];
    // Remove duplicates from the combined array
    const uniqueArray = [...new Set(newArray)];
    // Sort the unique array in ascending or descending order
    uniqueArray.sort((a, b) => {
        const aValue = GetDateValue(a);
        const bValue = GetDateValue(b);
        return ascending ? aValue.localeCompare(bValue) : bValue.localeCompare(aValue);
    });
    //console.log("New Array: " + JSON.stringify(uniqueArray));
    return uniqueArray;
}
function GetDateValue(date) {
    if (date instanceof Date) {
        return date.toISOString();
    } else if (typeof date === 'string') {
        const [month, day, year] = date.split('/');
        const standardizedDate = `${year}-${month.padStart(2, '0')}-${day.padStart(2, '0')}`;
        return standardizedDate;
    } else {
        return String(date);
    }
}