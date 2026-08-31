//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function UserButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');    
    //var user = $(sender).attr('UserId');   



    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/User/Edit?id=";
            LoadPage(route, item)
            break;
        
        default:
            //SubmitFeedback(beerID, response);
            break;
    }
}