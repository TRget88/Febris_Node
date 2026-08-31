//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function CohortButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    var linkBase = $(sender).attr('LinkBase');        


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {        
        case "Edit":
            route = "/Cohort/edit?id=";
            LoadPage(route, item);
            break;                      
        case "Create":
            route = "/Cohort/create";
            LoadPage(route);
            break;       
        case "Delete":
            route = "/Cohort/delete/";
            LoadPage(route, item);
            break;

        //case "QuickAddMember":
        //    route = "/Cohort/CreateByCampaign?id=";
        //    LoadModal(route, item);
        //    break;
        case "ManageMembers":
            route = "/Cohort/ManageMemberIndex?id=";
            LoadPage(route, item);
            break;
        default:
            break;
    }
}