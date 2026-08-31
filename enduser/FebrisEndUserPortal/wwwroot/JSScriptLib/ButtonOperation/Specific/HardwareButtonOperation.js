
//Button Action Tree
function HardwareButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    //var linkBase = $(sender).attr('LinkBase');


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/Hardware/edit?id=";
            LoadPage(route, item);
            break;
        case "Create":
            route = "/Hardware/create";
            LoadPage(route);
            break;     
        case "Delete":
            route = "/Hardware/delete/";
            LoadPage(route, item);            
            break;
        case "ManageModules":
            route = "/Hardware/ManageModuleIndex/";
            LoadPage(route, item);
            break;
        case "ManageCohorts":
            route = "/Hardware/ManageCohortIndex/";
            LoadPage(route, item);
            break;
        
        default:
            break;
    }

}