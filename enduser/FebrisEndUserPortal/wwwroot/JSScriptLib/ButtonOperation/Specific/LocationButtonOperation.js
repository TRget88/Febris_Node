//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function LocationButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    var linkBase = $(sender).attr('LinkBase');


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "Edit":
            route = "/HealthCareProviderLocations/edit?id=";
            LoadPage(route, item);
            break;
        case "Create":
            route = "/HealthCareProviderLocations/create";
            LoadPage(route);
            break;
        case "LocationTestData":
            route = "/HealthCareProviderLocations/LocationTestDetailsModalPartial?locationId=";//not set up
            LoadModal(route, item);
            break;
        //case "LinkLocation":
        //    route = "/Link/LocationLinkerPartial?locationId=";//needs to be created to specifically link to providers
        //    LoadModal(route, item);            
        //    break;
        case "LinkMoverLocation":
            route = "/Link/LocationLinkMoverPartial?locationId=";
            LoadModal(route, item);
            break;
        case "UnlinkLocation": //this is an action but it says its a partial. Dont think it is. This is pointless 
            route = "/Link/RemoveLinkedLocationsAndProvidersPartial?locationId=";
            //linkBaseId = "&providerId="
            LoadModal(route, item);
            break;
        case "DeleteLocation":
            route = "/HealthCareProviderLocations/delete?Id=";
            LoadPage(route, item);
            break;
        case "BulkLocationCreation":
            route = "/Link/BulkLocationInput";
            LoadPage(route);
            break;
        case "HardwareLink":
            route = "/Link/LinkHardwareWithHealthProviderLocations?locationId=";
            LoadPage(route, item);
            break;
        case "ProfessionalRemoval":
            route = "/link/ProfessionalRemoval?locationId=";
            routeBase = "&professionalId="
            LoadModal(route, item, routeBase, linkBase);
            break;  
        default:
            break;
    }

}