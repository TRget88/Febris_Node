//set up DOM Variables


//initalizing variables or setting constants


//Button Action Tree
function ButtonAction(sender) {
    var item = $(sender).attr('identification');
    var actionResponse = $(sender).attr('submitAction');
    //var linkBase = $(sender).attr('LinkBase');
    //var user = $(sender).attr('UserId')


    //this is used to direct button clicked so it can rout you to what you need
    switch (actionResponse) {
        case "CategoryDetails":
            route = "/Category/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "CohortDetails":
            route = "/Cohort/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "CohortMemberDetails":
            route = "/CohortMember/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "DeploymentTypeDetails":
            route = "/DeploymentType/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "FebrisUserDetails":
            route = "/FebrisUser/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "FocusDetails":
            route = "/Focus/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "HardwareDetails":
            route = "/Hardware/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "HardwareTypeDetails":
            route = "/HardwareType/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "IndustryDetails":
            route = "/Industry/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LiabilityWaiverDetails":
            route = "/LiabilityWaiver/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LicenseDetails":
            route = "/License/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LocalSoftwarePackageDetails":
            route = "/LocalSoftwarePackage/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "MessageboardDetails":
            route = "/Messageboard/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "ModuleDetails":
            route = "/Module/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "ProfessionalDetails":
            route = "/Professional/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "TagDetails":
            route = "/Tag/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "XRHardwareModelDetails":
            route = "/XRHardwareModel/DetailsModal?id=";
            LoadModal(route, item)
            break;
        //Marketing
        case "CampaignMemberDetails":
            route = "/CampaignMember/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "CampaignMessageDetails":
            route = "/EmailCampaignMessage/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "CampaignDetails":
            route = "/Campaign/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "CampaignNoteDetails":
            route = "/CampaignNote/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadDetails":
            route = "/Lead/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadInboxDetails":
            route = "/LeadInbox/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadNoteDetails":
            route = "/LeadNote/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadRatingDetails":
            route = "/LeadRating/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadTagDetails":
            route = "/LeadTag/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "LeadLinkedTagDetails":
            route = "/LeadLinkedTag/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "FAQDetails":
            route = "/FAQ/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "PublicationDetails":
            route = "/Publication/DetailsModal?id=";
            LoadModal(route, item)
            break;       
        
        case "TeamMemberAssignedToCampaignDetails":
            route = "/TeamMemberAssignedToCampaign/DetailsModal?id=";
            LoadModal(route, item)
            break;
        //case "PublicationAnalyticsDetails":
        //    route = "/PublicationAnalytics/DetailsModal?id=";
        //    LoadModal(route, item)
        //    break;        
        //xAPI       
        case "ActorDetails":
            route = "/Actor/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "StatementDetails":
            route = "/Statement/DetailsModal?id="
            LoadModal(route, item);
            break;
        //User
        case "UserDetails":
            route = "/User/DetailsModal?id=";
            LoadModal(route, item)
            break;
        case "UserDataDetails":
            route = "/UserData/DetailsModal?id=";
            LoadModal(route, item)
            break;
        //Analytics
        //case "AdminAnalyticsDetails":
        //    route = "/AdminAnalytics/DetailsModal?id=";
        //    LoadModal(route, item)
        //    break;
        //case "DeveloperAnalyticsDetails":
        //    route = "/DeveloperAnalytics/DetailsModal?id=";
        //    LoadModal(route, item)
        //    break;
        //case "MarketingAnalyticsDetails":
        //    route = "/MarketingAnalytics/DetailsModal?id=";
        //    LoadModal(route, item)
        //    break;
        case "UserAnalyticsDetails":
            route = "/UserAnalytics/DetailsModal?id=";
            LoadModal(route, item)
            break;
        //Billing
        case "HelpDetails":
            //LoadHardwareDetailsModal(item);
            break;
        default:
            //SubmitFeedback(beerID, response);
            break;
    }

}