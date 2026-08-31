//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function MessageboardButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    var linkBase = $(sender).attr('LinkBase');
    var user = $(sender).attr('UserId')


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/Messageboard/edit?id=";
            LoadPage(route, item);
            break;
        case "Create":
            route = "/Messageboard/create";
            LoadPage(route);
            break;
        case "Delete":
            route = "/Messageboard/delete";
            LoadPage(route, item);
            break;
        default:
            break;
    }

}