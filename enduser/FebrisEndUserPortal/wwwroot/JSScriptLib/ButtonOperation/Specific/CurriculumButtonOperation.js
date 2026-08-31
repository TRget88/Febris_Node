//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function CurriculumButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    var linkBase = $(sender).attr('LinkBase');
    var user = $(sender).attr('UserId')


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/Curriculum/edit?id=";
            LoadPage(route, item);
            break;
        case "Create":
            route = "/Curriculum/create";
            LoadPage(route);
            break;
        case "Delete":
            route = "/Curriculum/delete";
            LoadPage(route, item);
            break;
        case "AddModuleLink":
            route = "/Curriculum/ModuleToCurriculumIndex?id=";
            LoadPage(route, item);
            break;
        default:
            break;
    }
}