using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Web;
using System.Web.Services;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using System.Xml.XPath;
using CTSmartCitizenConnect.CTDataLayer;
using CTSmartCitizenConnect.SmartConnect;
using log4net;
using CTPass = warwickshire.ConcessionaryTravel.Classes.CTPass;
using CTPassHolder = warwickshire.ConcessionaryTravel.Classes.CTPassHolder;
using CTPassType = warwickshire.ConcessionaryTravel.Classes.CTPassType;

namespace CTSmartCitizenConnect
{

    
    
    public class SmartCitizenConnector
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private CardManagerClient _cmClient;
        private readonly string appDataPath = HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/";
        private Dictionary<int, string> _smartCitizenStatuses = new Dictionary<int, string>();

        public SmartCitizenConnector()
        {
            if (log.IsInfoEnabled) log.Info("Initialising CardManager Client");
            _cmClient = new CardManagerClient("WSHttpBinding_ICardManager1");
            _cmClient.Endpoint.Address = new EndpointAddress(new Uri(ConfigurationManager.AppSettings["SCUrl"]), _cmClient.Endpoint.Address.Identity, _cmClient.Endpoint.Address.Headers);
            if (log.IsDebugEnabled) log.Debug("Initialising Client credentials");
            _cmClient.ClientCredentials.UserName.UserName = ConfigurationManager.AppSettings["SCUserId"];
            _cmClient.ClientCredentials.UserName.Password = ConfigurationManager.AppSettings["SCPassword"];
            if (log.IsDebugEnabled) log.Debug("Client Credentials initialised.");
            if (log.IsDebugEnabled) log.Debug("Bypassing certificate validation.");
            _cmClient.ClientCredentials.ServiceCertificate.Authentication.CertificateValidationMode =
                System.ServiceModel.Security.X509CertificateValidationMode.None;
            if (log.IsInfoEnabled) log.Info("CardManager Client initialised.");
            
            //Initialise Card Statuses
            XElement SmartCitizenCardStatuses = LoadXmlFragment("SmartCitizenCardStatus.xml");
            foreach (XElement statusElement in SmartCitizenCardStatuses.XPathSelectElements("Status"))
            {
                _smartCitizenStatuses.Add(Convert.ToInt32(statusElement.Attribute("id").Value), statusElement.Value);
            }
        }

        /// <summary>
        /// Checks SmartCitizen to verify that a passholder exists and returns their details
        /// </summary>
        /// <param name="surname"></param>
        /// <param name="forename"></param>
        /// <param name="dateOfBirth"></param>
        /// <param name="postcode"></param>
        /// <param name="ISRN"></param>
        /// <returns>Pass Holder Details</returns>
         public SmartCitizenCTPassholder[] SearchPassHolders(string surname, string forename, string dateOfBirth, string postcode, string ISRN)
        //public XmlDocument SearchPassHolders(string surname, string forename, string dateOfBirth, string postcode, string ISRN)
        {
            List<SmartCitizenCTPassholder> searchResults = new List<SmartCitizenCTPassholder>();
            if (log.IsDebugEnabled) log.Debug("Entering");
            logParams(surname, forename, dateOfBirth, postcode, ISRN);

            if (log.IsDebugEnabled) log.Debug("Trimming spaces");
            surname = surname.Trim();
            forename = forename.Trim();
            dateOfBirth = dateOfBirth.Trim();
            postcode = postcode.Trim();
            ISRN = ISRN.Trim();
            if (log.IsDebugEnabled) log.Debug("Spaces Trimmed.");

            //Parse the DoB String
            DateTime parsedDob = new DateTime();

            if (log.IsDebugEnabled) log.Debug("Parsing Date:" + dateOfBirth);
            if (!String.IsNullOrEmpty(dateOfBirth))
            {
                if (DateTime.TryParse(dateOfBirth, out parsedDob) == false)
                    throw new ArgumentException("Date: " + dateOfBirth + " is not a valid Date.");
            }
            if (!String.IsNullOrEmpty(ISRN))
            {
                //Searching by ISRN will only return one value.
                searchResults.Add(GetCTPassholderForPass(ISRN));
                return searchResults.ToArray();
            }

            try
            {
                if (log.IsDebugEnabled) log.Debug("Checking Cardholder Exists");
                //CardholderExistsData requires at least Surname & Postcode.
                CardholderExistsData cardholderExistsData = new CardholderExistsData { Surname = surname, Postcode = postcode };
                if (!String.IsNullOrEmpty(forename))
                    cardholderExistsData.Forename = forename;
                if (!String.IsNullOrEmpty(dateOfBirth))
                    cardholderExistsData.DateOfBirth = parsedDob;

                if (log.IsDebugEnabled) log.Debug("Message sent to SmartCitizen:");
                if (log.IsDebugEnabled) log.Debug(SerializeObj(cardholderExistsData));
                CardholderExistsResponse cardholderExistsResponse = _cmClient.CheckCardholderExists(cardholderExistsData);
                if (log.IsDebugEnabled) log.Debug("Response received from SmartCitizen:");
                if (log.IsDebugEnabled) log.Debug(SerializeObj(cardholderExistsResponse));
                //if there is 1/1 matches, .RecordExists will be true. If there are potential matches, .RecordExists will be false, but there will be items in the NonUniquePotentialMatches enum.
                if (!cardholderExistsResponse.RecordExists && cardholderExistsResponse.NonUniquePotentialMatches == null)
                    throw new CTSmartCitizen.ScValidationException(CTSmartCitizen.ScValidationException.ScValidationReason.CitizenDataNotFound);


                RecordIdentifier cardHolderUniqueId;
                //If there is a match where the initial matches, this is where the data will be...
                if (cardholderExistsResponse.UniqueMatchIdentifier != null)
                {
                    if (log.IsDebugEnabled) log.Debug("Single Exact Match found. Using Card Holder ID:" + cardholderExistsResponse.UniqueMatchIdentifier.CardholderID);
                    cardHolderUniqueId = cardholderExistsResponse.UniqueMatchIdentifier;
                    // Need to validate that if we return an exact match and the 
                    searchResults.Add(GetCtPassHolder(cardHolderUniqueId));
                   
                }
                else
                {
                    if (log.IsDebugEnabled) log.Debug("No exact match of initial only. Parsing through NonUnique matches.");
                    //If there are any NonUniquePotentialMatehes, this is where the data will be.
                    if (log.IsDebugEnabled)
                        log.Debug("Checking through NonUniquePotential matches to try and match data");

                    if (cardholderExistsResponse.NonUniquePotentialMatches.Length >= 1)
                    {
                            //Loop through matches to find forename and check that it completely matches.
                            searchResults.AddRange(cardholderExistsResponse.NonUniquePotentialMatches.Select(nonUniquePotentialMatch => GetCtPassHolder(nonUniquePotentialMatch.UniqueIdentifier)));
                    }
                }
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error(ex.Message);
                return searchResults.ToArray();
            }

            if (log.IsDebugEnabled) log.Debug("Returning Pass Data.");
            if (log.IsDebugEnabled) log.Debug("Exiting");
            return searchResults.ToArray();

        }

        public SmartCitizenCTPassholder GetCtPassHolder(string recordId)
        {
            RecordIdentifier clientRecordIdentifier = new RecordIdentifier {CardholderID = Convert.ToInt32(recordId)};
            return GetCtPassHolder(clientRecordIdentifier);
        }

        public SmartCitizenCTPassholder GetCtPassHolder(RecordIdentifier cardHolderIdentifier)
        {
            SmartCitizenCTPassholder passHolder = new SmartCitizenCTPassholder();
            GetCardholderResponse cardHolderDetails = getFullClientDetails(cardHolderIdentifier);

            passHolder.PassHolderNumber = cardHolderDetails.Identifier.CardholderID.ToString();
            passHolder.RecordID = cardHolderDetails.Identifier.CardholderID;
            passHolder.Title = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='TITLE']").Value;
            passHolder.FirstNameOrInitial = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='FORENAME']").Value;
            passHolder.Surname = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='SURNAME']").Value;
            passHolder.DateOfBirth = DateTime.Parse(cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='DOB']").Value);
            //Gender
            switch (cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='GENDER']").Value)
            {
                case "1":
                    passHolder.Gender = "M";
                    break;
                case "2":
                    passHolder.Gender = "F";
                    break;
                default:
                    passHolder.Gender = "U";
                    break;

            }
           

            //Address
            passHolder.HouseOrFlatNumberOrName = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='HOUSE NUMBER/NAME']").Value;
            passHolder.BuildingName = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='FLAT']").Value;
            passHolder.Street = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='STREET']").Value;
            passHolder.VillageOrDistrict = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='LOCALITY']").Value;
            passHolder.TownCity = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='TOWN']").Value;
            passHolder.County = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='COUNTY']").Value;
            passHolder.PostCode = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='POSTCODE']").Value;
            passHolder.CPICC =
                getCPICCForName(
                    cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='LOCAL AUTHORITY']")
                        .Value);
            passHolder.UPRN = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='UPRN']").Value;
           

            if (log.IsDebugEnabled) log.Debug("Determining if the pass record has a photograph");
            {
                if (cardHolderDetails.CitizenData.XPathSelectElement(
                    "Services/Service[@application='Photo Id']/Item[@name='PHOTO']") != null)
                {
                    if (
                        cardHolderDetails.CitizenData.XPathSelectElement(
                            "Services/Service[@application='Photo Id']/Item[@name='PHOTO']").Value.Length > 3)
                    // Check the string is a valid image...
                    {
                        try
                        {
                            byte[] imageBytes = Convert.FromBase64String(
                                cardHolderDetails.CitizenData.XPathSelectElement(
                                    "Services/Service[@application='Photo Id']/Item[@name='PHOTO']").Value);
                            ImageConverter ic = new ImageConverter();
                            ic.ConvertFrom(imageBytes);
                            passHolder.PhotographBytes = imageBytes;
                        }
                        catch (Exception ex)
                        {
                            if (log.IsInfoEnabled) log.Info("Could not convert image string to a valid image");
                            if (log.IsDebugEnabled) log.Debug(ex.Message);
                        }
                    }
                }

            }
            
            CTPass passForPassHolder = GetCtPassForPassholder(cardHolderDetails);
            passHolder.CtPass = passForPassHolder;

            if ((passHolder.CtPass.PassType == CTPassType.Disabled ||
                passHolder.CtPass.PassType == CTPassType.DisabledTemporary) && cardHolderDetails.CitizenData.XPathSelectElement("Services/Service[@application='ENCTS']")
                        .Attribute("refinement").Value.ToLower() != "disabled")
            {
                // If the refinement is "Disabled" then there is no disability category
                    passHolder.DisabilityCategory =
                        cardHolderDetails.CitizenData.XPathSelectElement("Services/Service[@application='ENCTS']")
                            .Attribute("refinement").Value[0];
                }
            

            return passHolder;
        }

        //ToDo Write UpdatePassImage method.
        public void UpdatePassImage(RecordIdentifier cardHolderIdentifier, string passImageString)
        {
            //GetCardholderResponse existingCardHolderData = getFullClientDetails(cardHolderIdentifier);
            SmartCitizenCTPassholder existingPassholder = GetCtPassHolder(cardHolderIdentifier);
            existingPassholder.PassImageString = passImageString;
            UpdateCardholderData cardholderData = new UpdateCardholderData();
            cardholderData.Identifier = cardHolderIdentifier;
            
            cardholderData.CitizenData = existingPassholder.UpdateImageCitizenData;

                //cardholderData.CitizenData.XPathSelectElement("Services/Service[@name='Photo Id']/Item[@itemId='16']").Value = passImageString;

            

            try
            {
_cmClient.UpdateCardholder(cardholderData);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error updating Cardholder Photo:" + ex.Message);
                throw;
            }
            

        }

        public void FlagPassHolder(string passHolderNumber, string flagDescription)
        {
            SmartCitizenCTPassholder passHolder = GetCtPassHolder(passHolderNumber);
            UpdateCardData cardData = new UpdateCardData{};
            cardData.CardStatus = GetFlagStatusId(flagDescription);
            cardData.Identifier = new RecordIdentifier{CardID = passHolder.CtPass.ISRN};
            try
            {
_cmClient.UpdateCard(cardData);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not flag pass with ISRN:[" + passHolder.CtPass.ISRN + "]");
                if (log.IsErrorEnabled) log.Error(ex.Message);
                throw;
            }
            
        }

        private int GetFlagStatusId(string flagDescription)
        {
            var cardStatus = _smartCitizenStatuses.FirstOrDefault(x => x.Value.Contains(flagDescription.ToLower())).Key;
            return cardStatus;
        }

        public void UpdatePassImage(string recordId, string passImageString)
        {
            UpdatePassImage(new RecordIdentifier {CardholderID = Convert.ToInt32(recordId)}, passImageString);
        }


        public CTPass IssuePass(string title, string forename, string surname, string dateofbirth, string gender,
            string email, string homephone, string mobilePhone, string flatNumber, string houseNumberOrName,
            string street, string locality, string town,
            string county, string UPRN, string CPICC, string postcode, string passImageString, string CaseID, CTPassType typeOfPass, string disabilityCategory, Proof addressProof)
        {
            return IssuePass(title, forename, surname, dateofbirth, gender,
                email, homephone, mobilePhone, flatNumber, houseNumberOrName,
                street, locality, town,
                county, UPRN, CPICC, postcode, passImageString, CaseID, typeOfPass, disabilityCategory, addressProof, null, null);
        }

        public CTPass IssuePass(string title, string forename, string surname, string dateofbirth, string gender, string email, string homephone, string mobilePhone, string flatNumber, string houseNumberOrName, string street, string locality, string town,
            string county, string UPRN, string CPICC, string postcode, string passImageString, string CaseID, CTPassType typeOfPass, string disabilityCategory, Proof addressProof, Proof ageProof, Proof disabilityProof)
             {
            CreateCardholderData createCardholderRequest = new CreateCardholderData();

            
            SmartCitizenCTPassholder newPassHolder = new SmartCitizenCTPassholder
            {
                Title = title, FirstNameOrInitial = forename, Surname = surname, Gender = gender, Email = email, HomePhone = homephone, MobilePhone = mobilePhone, Street = street, VillageOrDistrict = locality, TownCity = town,
                County = county, UPRN = UPRN, CPICC = CPICC, PostCode = postcode, PassImageString = passImageString, AgeProof = ageProof, AddressProof = addressProof, DisabilityProof = disabilityProof
            };
            // Check to see if property has a SAON
            if (!String.IsNullOrEmpty(flatNumber))
            {
                newPassHolder.HouseOrFlatNumberOrName = flatNumber;
                newPassHolder.BuildingName = houseNumberOrName;
            }
            else
            {
                newPassHolder.HouseOrFlatNumberOrName = houseNumberOrName;
            }
            DateTime parsedDoB;
            if (
                DateTime.TryParse(dateofbirth, out parsedDoB))
                newPassHolder.DateOfBirth = parsedDoB;
            // Disability category stored on the CTPass object. need to create as it won't exist yet...
            newPassHolder.CtPass = new CTPass();
            newPassHolder.CtPass.PassType = typeOfPass;
            if(typeOfPass != CTPassType.Age)
                newPassHolder.DisabilityCategory = disabilityCategory[0];

            createCardholderRequest.CitizenData = newPassHolder.IssuePassCitizenDataXml;
            createCardholderRequest.StageID = 6;


            try
                {
                    if (log.IsDebugEnabled) log.Debug(SerializeObj(createCardholderRequest));
                    RecordIdentifier id = _cmClient.CreateCardholder(createCardholderRequest);
                    if (log.IsInfoEnabled) log.Info("Record Created. Record ID:" + id.CardholderID);

                    // Try and create a new pass

                    // CardLocation is hard-coded to 3: 'unknown' - not sure if this needs to be parameterised.
                    //UpdateCardData cardDataToUpdate = new UpdateCardData() { Identifier = id, CardLocation = 3, CardStatus = 17, AdditionalInformation = "Renewal requested through CRM. Case Referece Number:" + CaseID, ReplaceCard = true, IssuerId = 2 };

                    /*RecordIdentifier responseIdentifier = null;
                    try
                    {
                        if (log.IsDebugEnabled) log.Debug("Creating Card.");
                        if (log.IsDebugEnabled) log.Debug(SerializeObj(cardDataToUpdate));
                        responseIdentifier = _cmClient.UpdateCard(cardDataToUpdate);
                        if (log.IsDebugEnabled) log.Debug(SerializeObj(responseIdentifier));
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not create card:" + ex.Message);
                        throw ex;
                    }*/

                    return GetCtPassForPassholder(id);


                }
                catch (Exception ex)
                {

                    if (log.IsErrorEnabled) log.Error("Could not create CardHolder." + ex.Message);
                }

                
           

            return new CTPass();
        }

        

        private SmartCitizenCTPassholder GetCTPassholderForPass(string ISRN)
        {
            RecordIdentifier cardIdentifier = new RecordIdentifier {CardID = ISRN};
            return GetCtPassHolder(cardIdentifier);
        }

        private CTPass GetCtPassForPassholder(GetCardholderResponse cardHolderDetails)
        {
            CTPass passForPassHolder = new CTPass();
            passForPassHolder.ISRN = cardHolderDetails.Identifier.CardID;

            if (cardHolderDetails.CitizenData.XPathSelectElement("Services/Service[@application='ENCTS']") != null && cardHolderDetails.CitizenData.XPathSelectElement("Services/Service[@application='ENCTS']")
                .Attribute("refinement") != null)
            {
            string passType = cardHolderDetails.CitizenData.XPathSelectElement("Services/Service[@application='ENCTS']")
                .Attribute("refinement").Value;

                if (passType.ToLower().Contains("older"))
                    passForPassHolder.PassType = CTPassType.Age;
                else
                {
                    if (String.IsNullOrEmpty(cardHolderDetails.CitizenData.XPathSelectElement(
                        "Services/Service[@application='ENCTS']/Item[@name='RENEWREFER']").Value))
                        passForPassHolder.PassType = CTPassType.Disabled;
                    else
                    {


                        switch (
                            cardHolderDetails.CitizenData.XPathSelectElement(
                                "Services/Service[@application='ENCTS']/Item[@name='RENEWREFER']").Value.ToLower())
                        {
                            case "renew":
                                passForPassHolder.PassType = CTPassType.Disabled;
                                break;
                            case "refer":
                                passForPassHolder.PassType = CTPassType.DisabledTemporary;
                                break;
                        }
                    }

                }
            }

            else
            {
                passForPassHolder.PassType = CTPassType.NotSet;
            }

            DateTime expiryDate;
            if (
                DateTime.TryParse(
                    cardHolderDetails.CitizenData.XPathSelectElement("Services/Service/Item[@name='EXPIRY DATE']")
                        .Value, out expiryDate))
                passForPassHolder.ExpiryDate = expiryDate;
            return passForPassHolder;
        }


        //ToDo - Generate the CitizenData from the object.
        //public XmlDocument UpdatePassHolderDetails(int cardHolderId, string title, string forename, string surname, string dateOfBirth, string gender, string disabilitycategory)
        public SmartCitizenCTPassholder UpdatePassHolderDetails(SmartCitizenCTPassholder passHolderToUpdate)
        {
            XmlDocument responseDoc = new XmlDocument();
            try
            {
                responseDoc.Load(appDataPath + "CTSelfPassHolderUpdateResponse.xml");
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not load Response XML file:" + appDataPath + "CTSelfPassHolderUpdateResponse.xml");
                throw ex;
            }
            UpdateCardholderData cardholderData = new UpdateCardholderData();
            cardholderData.Identifier = new RecordIdentifier() { CardholderID = passHolderToUpdate.RecordID };
            // map the input fields to the Citizen XML fragment...
            /*XElement citizenDataXElement = LoadXmlFragment("WCCUpdateCardholderFragment.xml");
            /*
             * Item IDs:
             * 68: Title
             * 3: Forename
             * 4: Surname
             * 5: Date of Birth (yyyy-mm-dd hh:mm:ss)
             * 7: Gender (1=M, 2=F, 0=Trans, 9=Unknown)
             * 21: Display Name
             * 20: Mobile Phone
             * 19: Work Phone
             * 18: Home Phone
             * 22: Notes
             * 119: Record ID
             


            #region Personal Data

            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='68']").Value
               = passHolderToUpdate.Title.ToTitleCase();
            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='3']").Value
                = passHolderToUpdate.FirstNameOrInitial.ToTitleCase();
            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='4']").Value
                = passHolderToUpdate.Surname.ToTitleCase();
            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='5']").Value
                = passHolderToUpdate.DateOfBirth != null ? passHolderToUpdate.DateOfBirth.Value.ToString("yyyy-MM-dd 00:00:00") : "1900-01-01 00:00:00";
            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='21']").Value
                = passHolderToUpdate.FirstNameOrInitial.ToTitleCase() + " " +
                  passHolderToUpdate.Surname.ToTitleCase();
            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='119']").Value =
                passHolderToUpdate.RecordID.ToString();

            

            int genderInt;
            switch (passHolderToUpdate.Gender.ToUpper()[0])
            {
                case 'M':
                    genderInt = 1;
                    break;
                case 'F':
                    genderInt = 2;
                    break;
                default:
                    genderInt = 9;
                    break;
            }

            citizenDataXElement.XPathSelectElement("/Services/Service[@name='CCDA']/Item[@itemId='7']").Value
                = genderInt.ToString();
            cardholderData.CitizenData = citizenDataXElement;

            if (passHolderToUpdate.DisabilityCategory != '\0')
            {
                XElement disabilityServiceXElement =
                    XElement.Parse(
                        "<Service name=\"\" serviceId=\"\"><Item itemId=\"\" dtype=\"lookup\"></Item></Service>");
                XElement disabilityLookupFragment = LoadXmlFragment("WCC_SC_DisabilityServiceXRef.xml");
                XElement selectedDisabilityElement = disabilityLookupFragment.XPathSelectElement("/DisabilityFragment[starts-with(@disabilityCategory,'" + passHolderToUpdate.DisabilityCategory + "')]");
                disabilityServiceXElement.SetAttributeValue("name", selectedDisabilityElement.XPathSelectElement("ServiceName").Value);
                disabilityServiceXElement.SetAttributeValue("serviceId", selectedDisabilityElement.XPathSelectElement("ServiceId").Value);
                disabilityServiceXElement.XPathSelectElement("Item").SetAttributeValue("itemId", selectedDisabilityElement.XPathSelectElement("ItemId").Value);
                disabilityServiceXElement.XPathSelectElement("Item").Value = "Renew";
                //selectedDisabilityElement.XPathSelectElement("PermanentLookupId").Value;
                citizenDataXElement.XPathSelectElement("/Services").Add(disabilityServiceXElement);
            }
            #endregion
            #region AddressData

            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='POSTCODE']").Value
                = passHolderToUpdate.PostCode;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='FLAT']").Value =
                passHolderToUpdate.BuildingName;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='HOUSE NUMBER/NAME']").Value =
                passHolderToUpdate.HouseOrFlatNumberOrName;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='STREET']").Value =
                passHolderToUpdate.Street;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='LOCALITY']").Value =
                passHolderToUpdate.VillageOrDistrict;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='TOWN']").Value =
                passHolderToUpdate.TownCity;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='COUNTY']").Value =
                passHolderToUpdate.County;
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='LOCAL AUTHORITY']").Value =
                getNameForCPICC(passHolderToUpdate.CPICC);
            //citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='LOCAL AUTHORITY']").Value = "Test";
            citizenDataXElement.XPathSelectElement("/Addresses/Address/Item[@name='UPRN']").Value =
                passHolderToUpdate.UPRN;
            #endregion*/
            cardholderData.CitizenData = passHolderToUpdate.IssuePassCitizenDataXml;
            try
            {
                if (log.IsDebugEnabled) log.Debug("Update Pass Data Request:");
                if (log.IsDebugEnabled) log.Debug(SerializeObj(cardholderData));
                _cmClient.UpdateCardholder(cardholderData);
                if (log.IsInfoEnabled) log.Info("Passholder ID:" + passHolderToUpdate.RecordID + "updated.");
                responseDoc.SelectSingleNode("PassHolderUpdate/RequestStatus").InnerText = "Success";

                

            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error updating Card Holder Data for cardholder ID:" + passHolderToUpdate.RecordID + " - " + ex.Message);
                responseDoc.SelectSingleNode("PassHolderUpdate/RequestStatus").InnerText = "Failure";

            }

            SmartCitizenCTPassholder updatedPassHolder = GetCtPassHolder(passHolderToUpdate.RecordID.ToString());


            return updatedPassHolder;
        }

        private string getNameForCPICC(string CPICC)
        {
            switch (CPICC)
            {
                case "29040":
                    return "Warwick";
                    
                case "28976":
                    return "North Warwickshire";
                    
                case "29024":
                    return "Stratford-on-Avon";
                case "29008":
                    return "Rugby";
                    
                case "28992":
                    return "Nuneaton and Bedworth";
            }
            return "";
        }

        private string getCPICCForName(string authorityName)
        {
            switch (authorityName)
            {
                case "Warwick":
                    return "29040";

                case "North Warwickshire":
                    return "28976";

                case "Stratford-on-Avon":
                    return "29024";
                case "Rugby":
                    return "29008";

                case "Nuneaton and Bedworth":
                    return "28992";
            }
            return "";
        }

        public XmlDocument ReplacePass(int cardHolderId, string ISRN, int cardStatus, string caseNumber) //, string title, string forename, string dateOfBirth, string gender, string disabilityCategory, string caseId)
        {
            if (log.IsDebugEnabled) log.Debug("Entering");
            if (log.IsInfoEnabled) log.Info("Replacing pass for recordID:" + cardHolderId);
            logParams(cardHolderId, ISRN, cardStatus, caseNumber);//, title, forename, dateOfBirth, gender, disabilityCategory, caseId);


            // code below here for SmartCitizen connection.
            if (log.IsDebugEnabled) log.Debug("Loading Response XML file");
            XmlDocument responseDoc = new XmlDocument();
            try
            {
                responseDoc.Load(appDataPath + "CTSelfPassRenewalResponse.xml");
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not load Response XML file:" + appDataPath + "CTSelfPassRenewalResponse.xml");
                throw ex;
            }

            if (log.IsDebugEnabled) log.Debug("Response Document Loaded.");




            RecordIdentifier cardHolderRecordId = new RecordIdentifier() { CardholderID = cardHolderId, CardID = ISRN };
            //IssuerId is hard-coded to 2 - renew for this service as it is only for renewals.
            // CardLocation is hard-coded to 3: 'unknown' - not sure if this needs to be parameterised.
            UpdateCardData cardDataToUpdate = new UpdateCardData() { Identifier = cardHolderRecordId, CardLocation = 3, CardStatus = cardStatus, AdditionalInformation = "Renewal requested through CRM. Case Referece Number:" + caseNumber, ReplaceCard = true, IssuerId = 2 };

            RecordIdentifier responseIdentifier = null;
            try
            {
                if (log.IsDebugEnabled) log.Debug("Updating Card.");
                if (log.IsDebugEnabled) log.Debug(SerializeObj(cardDataToUpdate));
                responseIdentifier = _cmClient.UpdateCard(cardDataToUpdate);
                if (log.IsDebugEnabled) log.Debug(SerializeObj(responseIdentifier));
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error:" + ex.Message);
                var requestStatusNode = responseDoc.SelectSingleNode("PassRenewal/RequestStatus");
                if (requestStatusNode != null)
                    requestStatusNode.InnerText = "Failure";
                return responseDoc;
            }

            if (responseIdentifier != null)
            {
                if (log.IsDebugEnabled) log.Debug("Pass Successfully renewed. Getting details of new card");
                CTPass newPass = GetCtPassForPassholder(cardHolderId);
                //SmartCitizenCard cardForPerson = getSmartCitizenCardForPerson(responseIdentifier);
                responseDoc.SelectSingleNode("PassRenewal/CardNumber").InnerText = newPass.ISRN;
                responseDoc.SelectSingleNode("PassRenewal/ExpiryDate").InnerText = newPass.ExpiryDate.ToShortDateString();
                responseDoc.SelectSingleNode("PassRenewal/RequestStatus").InnerText = "Success";
            }
            else
            { responseDoc.SelectSingleNode("PassRenewal/RequestStatus").InnerText = "Failure"; }


            if (log.IsDebugEnabled) log.Debug("Exiting");
            return responseDoc;


        }

        public void GetProofList()
        {
            DocumentaryProofsListResponse[] response = _cmClient.GetCitizenProofRequired(new RecordIdentifier {CardholderID = 596195});
            log.Debug(SerializeObj(response));
        }


        /// <summary>
        /// Gets the CT Pass from the Record Identifier
        /// </summary>
        /// <param name="cardHolderRecordIdentifier">Record Identifier for Passholder</param>
        /// <returns></returns>
        private CTPass GetCtPassForPassholder(RecordIdentifier cardHolderRecordIdentifier)
        {
        GetCardholderResponse cardHolderDetails = getFullClientDetails(cardHolderRecordIdentifier);
            return GetCtPassForPassholder(cardHolderDetails);
        }

        /// <summary>
        /// Gets the CT Pass from an int of the PassholderId.
        /// </summary>
        /// <param name="passholderId"></param>
        /// <returns></returns>
        private CTPass GetCtPassForPassholder(int passholderId)
        {
            RecordIdentifier cardHolRecordIdentifier = new RecordIdentifier() {CardholderID = passholderId};
            return GetCtPassForPassholder(cardHolRecordIdentifier);
        }

        private GetCardholderResponse getFullClientDetails(RecordIdentifier cardHolderRecordIdentifier)
        {
            if (log.IsInfoEnabled) log.Info("Retrieving Cardholder details for CardHolder:" + cardHolderRecordIdentifier.CardholderID);
            GetCardholderResponse cardHolderDetails = _cmClient.GetCardholder(new GetCardholderData() { CardholderIdentifier = cardHolderRecordIdentifier });
            if (log.IsDebugEnabled) log.Debug("Retrieved Customer record from ID. Output of Raw response:");
            if (log.IsDebugEnabled) log.Debug(SerializeObj(cardHolderDetails));
            return cardHolderDetails;
        }

        private string SerializeObj<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(obj.GetType());
            using (StringWriter txtWriter = new StringWriter())
            {
                xmlSerializer.Serialize(txtWriter, obj);
                return txtWriter.ToString();
            }
        }

        /// <summary>
        /// Loads a predefined XML Fragment
        /// </summary>
        /// <param name="xmlFragmentFileName">Fragment Filename. File needs to exist in app_data</param>
        /// <returns>XElement</returns>
        private XElement LoadXmlFragment(string xmlFragmentFileName)
        {
            XElement xmlFragment = null;
            using (var txtReader = new XmlTextReader(appDataPath + xmlFragmentFileName))
            {
                xmlFragment = XElement.Load(txtReader);
            }

            return xmlFragment;

        }

        

        private void logParams(params object[] parms)
        {
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            if (log.IsDebugEnabled) log.Debug("Output of parameters supplied for:" + stackTrace.GetFrame(1).GetMethod().Name);
            for (int i = 0; i < parms.Length; i++)
            {
                if (log.IsDebugEnabled) log.Debug("Parameter [" + i + "]: Name:" + stackTrace.GetFrame(1).GetMethod().GetParameters()[i].Name + " Value:[" + parms[i] + "]");
            }
        }

        public class Proof
        {
            private int _proofId;
            private string _proofReference;
            private DateTime? _proofExpiry;
            private DateTime _dateOnProof;

            public Proof(int proofId, string proofReference, DateTime? proofExpiry, DateTime dateOnProof)
            {
                _proofId = proofId;
                _proofReference = proofReference;
                _proofExpiry = proofExpiry;
                _dateOnProof = dateOnProof;
            }

            public XElement ProofXml
            {
                get
                {
                    XElement proofElement = new XElement("Proof");
                    if (_proofExpiry.HasValue)
                    {
                        proofElement.SetAttributeValue("expiresOn", _proofExpiry.Value); //.ToString("yyyy-MM-dd"));
                    }
                    proofElement.SetAttributeValue("dateOn", _dateOnProof);
                    proofElement.SetAttributeValue("reference", _proofReference);
                    proofElement.SetAttributeValue("kindId", _proofId);

                    return proofElement;
                }
            }

        }



        private class SmartCitizenCard
        {
            private DateTime _expiryDate;
            internal string ISRN { get; set; }
            internal string ExpiryDateString { get { return ExpiryDate.ToShortDateString(); } }
            internal bool IsExpired
            {
                get
                {
                    if (DateTime.Now > _expiryDate) return true;
                    return false;
                }
            }
            internal bool IsValid { get; set; }

            internal DateTime ExpiryDate
            {
                get { return _expiryDate; }
            }

            internal bool CanBeRenewed
            {
                get
                {
                    if (DateTime.Now.Date.AddYears(-1) < _expiryDate)
                        return false;
                    if (DateTime.Now.AddMonths(1) > _expiryDate)
                        return false;

                    return true;
                }
            }
        }
        public EntityDetailsListResponse getPassStatus(int recordID)
        {
            RecordIdentifier scRecordID = new RecordIdentifier();
            scRecordID.CardholderID = recordID;

            if (log.IsDebugEnabled) log.Debug("Message sent to SmartCitizen:");
            if (log.IsDebugEnabled) log.Debug(SerializeObj(scRecordID));

            EntityDetailsListResponse[] entityDetails =  _cmClient.GetEntityList(scRecordID);
            DateTime latestAuthDate =  new DateTime(1900,1,1);
            int latestEntry = 0;
            for (int i = 0; i < entityDetails.Length; i++)
            {
                log.Debug(SerializeObj(entityDetails[i]));
                if (latestAuthDate > entityDetails[i].AuthDate)
                {
                    latestEntry = i;
                    latestAuthDate = entityDetails[i].AuthDate.Value;
                }

            }

            return entityDetails[latestEntry];
        
        }
    }

    public class SmartCitizenCTPassholder : CTPassHolder
    {

        private readonly string appDataPath = HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/";
        private SmartCitizenConnector.Proof addressProof;
        private SmartCitizenConnector.Proof ageProof;
        private SmartCitizenConnector.Proof disabilityProof;
        private string _email;
        private string _homePhone;
        private string _mobilePhone;

        protected internal XmlDocument UpdatePassHolderCitizenDataXml
        {
            get
            {
                XmlDocument CitizenData = new XmlDocument();
                CitizenData.LoadXml(appDataPath + "WCCUpdateCardholderFragment.xml");

                return CitizenData;
            }
        }

        protected internal XElement IssuePassCitizenDataXml
        {
            get
            {
                using (var txtReader = new XmlTextReader(appDataPath + "SmartCitizenCreateTemplate.xml"))
                {
                    XElement citizenData = XElement.Load(txtReader);
                    if(!String.IsNullOrEmpty(Title))
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='68']").Value = Title;
                    if(!String.IsNullOrEmpty(FirstNameOrInitial))
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='3']").Value =
                        FirstNameOrInitial;
                    if(!String.IsNullOrEmpty(Surname))
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='4']").Value = Surname;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='21']").Value =
                        FirstNameOrInitial + ' ' + Surname;
                    if (DateOfBirth.HasValue)
                    {
                        citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='5']").Value =
                            DateOfBirth.Value.ToString("yyyy-MM-dd");
                    }

                    if(!String.IsNullOrEmpty(Email))
                     citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='14']").Value = Email;
                    if(!String.IsNullOrEmpty(MobilePhone))
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='20']").Value = MobilePhone;
                    if(!String.IsNullOrEmpty(HomePhone))
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='18']").Value = HomePhone;
                    if (!String.IsNullOrEmpty(Gender))
                    {
                        int genderInt;
                        switch (Gender.ToUpper()[0])
                        {
                            case 'M':
                                genderInt = 4;
                                break;
                            case 'F':
                                genderInt = 5;
                                break;
                            default:
                                genderInt = 6;
                                break;

                        }

                        citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='7']")
                            .SetAttributeValue("lookupId", genderInt);
                    }

                    //Photo
                    if(this.PhotoAssociated == 'Y')
                    citizenData.XPathSelectElement("Services/Service[@name='Photo Id']/Item[@itemId='16']").Value = Convert.ToBase64String(PhotographBytes);

                    //Address Details...
                    if(!String.IsNullOrEmpty(PostCode))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='POSTCODE']").Value = PostCode;
                    if (!String.IsNullOrEmpty(BuildingName))
                    {
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='FLAT']").Value =
                            HouseOrFlatNumberOrName;
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='HOUSE NUMBER/NAME']").Value =
                            BuildingName;
                    }
                    else
                    {
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='HOUSE NUMBER/NAME']").Value =
                            HouseOrFlatNumberOrName;
                    }

                    if(!String.IsNullOrEmpty(Street))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='STREET']").Value = Street;
                    if(!String.IsNullOrEmpty(VillageOrDistrict))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='LOCALITY']").Value = VillageOrDistrict;
                    if(!String.IsNullOrEmpty(TownCity))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='TOWN']").Value = TownCity;
                    if(!String.IsNullOrEmpty(County))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='COUNTY']").Value = County;
                    if (!String.IsNullOrEmpty(CPICC))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='LOCAL AUTHORITY']").Value =
                        getNameForCPICC(CPICC);;
                    if(!String.IsNullOrEmpty(UPRN))
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='UPRN']").Value = UPRN;

                    if (AddressProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(AddressProof.ProofXml);
                    }
                    if (DisabilityProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(DisabilityProof.ProofXml);
                    }
                    if (AgeProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(AgeProof.ProofXml);
                    }

                    //Type of pass
                    switch (CtPass.PassType)
                    {
                            case CTPassType.Age:
                            XElement olderPersonXElement = new XElement("Service");
                            olderPersonXElement.SetAttributeValue("name", "Older person");
                            olderPersonXElement.SetAttributeValue("serviceId", "42");
                            citizenData.XPathSelectElement("Services").Add(olderPersonXElement);
                            break;
                            case CTPassType.Disabled:
                            case CTPassType.DisabledTemporary:
                            citizenData.XPathSelectElement("Services").Add(getDisabilityXElement());
                            break;
                    }
                    

                    return citizenData;


                }
            }
        }

        protected internal XElement getIssuePassCitizenDataXml()
        {
            
                using (var txtReader = new XmlTextReader(appDataPath + "SmartCitizenCreateTemplate.xml"))
                {
                    XElement citizenData = XElement.Load(txtReader);
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='68']").Value = Title;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='3']").Value =
                        FirstNameOrInitial;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='4']").Value = Surname;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='21']").Value =
                        FirstNameOrInitial + ' ' + Surname;
                    if (DateOfBirth.HasValue)
                    {
                        citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='5']").Value =
                            DateOfBirth.Value.ToString("yyyy-MM-dd");
                    }

                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='14']").Value = Email;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='20']").Value = MobilePhone;
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='18']").Value = HomePhone;
                    int genderInt;
                    switch (Gender.ToUpper()[0])
                    {
                        case 'M':
                            genderInt = 1;
                            break;
                        case 'F':
                            genderInt = 2;
                            break;
                        default:
                            genderInt = 9;
                            break;

                    }
                    citizenData.XPathSelectElement("Services/Service[@name='CCDA']/Item[@itemId='7']")
                        .SetAttributeValue("lookupId", genderInt);

                    //Photo
                    citizenData.XPathSelectElement("Services/Service[@name='Photo Id']/Item[@itemId='16']").Value = Convert.ToBase64String(PhotographBytes);

                    //Address Details...
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='POSTCODE']").Value = PostCode;
                    if (!String.IsNullOrEmpty(BuildingName))
                    {
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='FLAT']").Value =
                            HouseOrFlatNumberOrName;
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='HOUSE NUMBER/NAME']").Value =
                            BuildingName;
                    }
                    else
                    {
                        citizenData.XPathSelectElement("Addresses/Address/Item[@name='HOUSE NUMBER/NAME']").Value =
                            HouseOrFlatNumberOrName;
                    }


                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='STREET']").Value = Street;
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='LOCALITY']").Value = VillageOrDistrict;
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='TOWN']").Value = TownCity;
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='COUNTY']").Value = County;
                    string responsibleAuthority = getNameForCPICC(CPICC);


                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='LOCAL AUTHORITY']").Value =
                        responsibleAuthority;
                    citizenData.XPathSelectElement("Addresses/Address/Item[@name='UPRN']").Value = UPRN;

                    if (AddressProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(AddressProof.ProofXml);
                    }
                    if (DisabilityProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(DisabilityProof.ProofXml);
                    }
                    if (AgeProof != null)
                    {
                        citizenData.XPathSelectElement("Proofs").Add(AgeProof.ProofXml);
                    }

                    //Type of pass
                    switch (CtPass.PassType)
                    {
                        case CTPassType.Age:
                            XElement olderPersonXElement = new XElement("Service");
                            olderPersonXElement.SetAttributeValue("name", "Older person");
                            olderPersonXElement.SetAttributeValue("serviceId", "42");
                            citizenData.XPathSelectElement("Services").Add(olderPersonXElement);
                            break;
                        case CTPassType.Disabled:
                        case CTPassType.DisabledTemporary:
                            citizenData.XPathSelectElement("Services").Add(getDisabilityXElement());
                            break;
                    }


                    return citizenData;


                }
            
        }

        public XElement UpdateImageCitizenData
        {
            get
            {
                using (var txtReader = new XmlTextReader(appDataPath + "SmartCitizenUpdateImageTemplate.xml"))
                {
                    XElement citizenData = XElement.Load(txtReader);
                    citizenData.XPathSelectElement("Services/Service[@name='Photo Id']/Item[@itemId='16']").Value = Convert.ToBase64String(PhotographBytes);
                    return citizenData;
                }
            }

        }

        private XElement getDisabilityXElement()
        {
            if (DisabilityCategory != '\0')
            {
                XElement disabilityServiceXElement =
                    XElement.Parse(
                        "<Service name=\"\" serviceId=\"\"><Item itemId=\"\" dtype=\"lookup\"></Item></Service>");

                XElement disabilityLookupFragment;

                using (var txtReader = new XmlTextReader(appDataPath + "WCC_SC_DisabilityServiceXRef.xml"))
                {
                    disabilityLookupFragment = XElement.Load(txtReader);
                }

                XElement selectedDisabilityElement =
                    disabilityLookupFragment.XPathSelectElement(
                        "/DisabilityFragment[starts-with(@disabilityCategory,'" + DisabilityCategory +
                        "')]");
                disabilityServiceXElement.SetAttributeValue("name",
                    selectedDisabilityElement.XPathSelectElement("ServiceName").Value);
                disabilityServiceXElement.SetAttributeValue("serviceId",
                    selectedDisabilityElement.XPathSelectElement("ServiceId").Value);
                disabilityServiceXElement.XPathSelectElement("Item")
                    .SetAttributeValue("itemId", selectedDisabilityElement.XPathSelectElement("ItemId").Value);
                if(CtPass.PassType == CTPassType.Disabled)
                    disabilityServiceXElement.XPathSelectElement("Item").Value = "Renew";
                else
                    disabilityServiceXElement.XPathSelectElement("Item").Value = "Refer";
                //selectedDisabilityElement.XPathSelectElement("PermanentLookupId").Value;
                return disabilityServiceXElement;
            }
            return null;
        }

        public string Email
        {
            get { return _email; }
            set { _email = value; }
        }

        public string HomePhone
        {
            get { return _homePhone; }
            set { _homePhone = value; }
        }

        public string MobilePhone
        {
            get { return _mobilePhone; }
            set { _mobilePhone = value; }
        }

        public SmartCitizenConnector.Proof AddressProof
        {
            get { return addressProof; }
            set { addressProof = value; }
        }

        public SmartCitizenConnector.Proof AgeProof
        {
            get { return ageProof; }
            set { ageProof = value; }
        }

        public SmartCitizenConnector.Proof DisabilityProof
        {
            get { return disabilityProof; }
            set { disabilityProof = value; }
        }

        public string PassImageString
        {
            set { this.PhotographBytes = Convert.FromBase64String(value); }
        }

        private string getNameForCPICC(string CPICC)
        {
            switch (CPICC)
            {
                case "29040":
                    return "Warwick";

                case "28976":
                    return "North Warwickshire";

                case "29024":
                    return "Stratford-on-Avon";
                case "29008":
                    return "Rugby";

                case "28992":
                    return "Nuneaton and Bedworth";
            }
            return "";
        }

        private string getCPICCForName(string authorityName)
        {
            switch (authorityName)
            {
                case "Warwick":
                    return "29040";

                case "North Warwickshire":
                    return "28976";

                case "Stratford-on-Avon":
                    return "29024";
                case "Rugby":
                    return "29008";

                case "Nuneaton and Bedworth":
                    return "28992";
            }
            return "";
        }
    }



}

