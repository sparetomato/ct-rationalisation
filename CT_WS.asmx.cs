using System;
using System.ComponentModel;
using System.Web.Services;
using System.Xml;
using warwickshire.gov.uk.CT_WS;
using log4net;

namespace CTSmartCitizenConnect
{    
    
    /// <summary>
    /// Summary description for CT_WS
    /// </summary>
    [WebService(Namespace = "http://warwickshire.gov.uk/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [ToolboxItem(false)]


    public class CT_WS : System.Web.Services.WebService
    {
        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        /// <summary>
        /// Issues a new Concessionary Travel Pass.
        /// </summary>
        /// <param name="CPICC">CPICC code for the pass</param>
        /// <param name="northgateCaseID">Northgate Case Reference Number</param>
        /// <param name="title">Applicant Title</param>
        /// <param name="firstNameOrInitial">Applicant forename</param>
        /// <param name="surname">Applicant surname</param>
        /// <param name="houseOrFlatNameOrNumber">House Number</param>
        /// <param name="buildingName">Building Name</param>
        /// <param name="street">Street name</param>
        /// <param name="villageOrDistrict">Village Name</param>
        /// <param name="townCity">Town Name</param>
        /// <param name="county">County</param>
        /// <param name="postcode">Postcode</param>
        /// <param name="dateOfBirth">Date of Birth dd/mm/yyyy as a string</param>
        /// <param name="typeOfConcession">A - Age, D - Disabled</param>
        /// <param name="disabilityPermanant">YES or NO (only applicable for Disabled Passes)</param>
        /// <param name="evidenceExpiryDate">dd/mm/yyyy (only applicable for temporary disabled passes</param>
        /// <param name="passStartDate">dd/mm/yyyy - for passes to be issued in the future</param>
        /// <param name="passImageString">Photograph encoded as Base64.</param>
        /// <returns></returns>
        [WebMethod]
        public XmlDocument issuePass(string CPICC, string northgateCaseID, string title, string firstNameOrInitial, string surname, string houseOrFlatNameOrNumber,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode,
            string dateOfBirth, string typeOfConcession, string disabilityPermanant, string evidenceExpiryDate, string passStartDate, string passImageString, string passPrintReason, string gender, string disabilityCategory, string UPRN, string addressProofId, string addressProofDate, string addressProofReference, string ageProofId, string ageProofDate, string ageProofReference, string disabilityProofId, string disabilityProofDate, string disabilityProofReference)
        {
            SmartCitizenConnector.Proof addressProof = new SmartCitizenConnector.Proof(Convert.ToInt32(addressProofId), addressProofReference, null, DateTime.Parse(addressProofDate));
            SmartCitizenConnector.Proof ageProof = null;
             if(!String.IsNullOrEmpty(ageProofReference))
               ageProof = new SmartCitizenConnector.Proof(Convert.ToInt32(ageProofId),ageProofReference, DateTime.Parse(ageProofDate), DateTime.Now);
            SmartCitizenConnector.Proof disabilityProof = null;
            if(!String.IsNullOrEmpty(disabilityProofReference))
                disabilityProof = new SmartCitizenConnector.Proof(Convert.ToInt32(disabilityProofId), disabilityProofReference, DateTime.Parse(evidenceExpiryDate), DateTime.Parse(disabilityProofDate));
            return CT_WSBL.getInstance().IssuePass(CPICC, northgateCaseID, firstNameOrInitial, surname, houseOrFlatNameOrNumber,
                buildingName, street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession, disabilityPermanant, evidenceExpiryDate, passStartDate, passImageString, passPrintReason, gender, disabilityCategory, UPRN, addressProof, ageProof, disabilityProof);
        }

 
        [WebMethod]
        public XmlDocument QueryPass(string forename, string Surname, string postcode, string passNo)
        {
            return CT_WSBL.getInstance().queryPass(forename, Surname, postcode,String.Empty, passNo);
        }

        [WebMethod]
        public bool GetProofs()
        {
            SmartCitizenConnector conn = new SmartCitizenConnector();
            conn.GetProofList();
            return true;
        }

        [WebMethod]
        public XmlDocument QueryPassFromPassHolderNumber(string CPICC, string passHolderNumber)
        {
            return CT_WSBL.getInstance().queryPassFromPassholderNumber(CPICC, passHolderNumber);
        }

        [WebMethod]
        public XmlDocument CancelPass(string ISRN, string delay)
        {
            return CT_WSBL.getInstance().cancelPass(ISRN, delay);
        }

        [WebMethod]
        public XmlDocument UpdatePassDetails(string ISRN, string passHolderNumber, string CPICC, string title, string firstNameOrInitial, string surname, string houseOrFlatNameOrNumber,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode,
            string dateOfBirth, string typeOfConcession, string disabilityPermanant, string evidenceExpiryDate, string passStartDate, bool reissuePass, string oldCPICC, bool recalculateExpiryDate, string northgateCaseNumber, string printReason,
            string gender, string disabilityCategory, string UPRN)//, byte[] imageFile)
        {
            return CT_WSBL.getInstance().updatePassDetails(ISRN, CPICC, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNameOrNumber,
                buildingName, street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession, disabilityPermanant,
                evidenceExpiryDate, passStartDate, reissuePass, oldCPICC, recalculateExpiryDate, northgateCaseNumber, printReason, gender, disabilityCategory, UPRN);
        }


        [WebMethod]
        public XmlDocument UpdateImage(string passHolderNumber, string CPICC, string passImageString)
        {
            return CT_WSBL.getInstance().updatePassImage(CPICC, passHolderNumber, passImageString);
            
        }

        [WebMethod]
        public XmlDocument FlagPass(string CPICC, string PassHolderNumber, string FlagDescription)
        {
            return CT_WSBL.getInstance().flagPass(CPICC, PassHolderNumber, FlagDescription);
        }





    
        #region Obsolete Methods

        //[WebMethod]
        //public XmlDocument addRecord(string CPICC, string northgateCaseID, string passHolderNumber, string title, string firstNameOrInitial, string surname, string houseOrFlatNameOrNumber,
        //    string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode,
        //    string dateOfBirth, string typeOfConcession, string disabilityPermanant, string evidenceExpiryDate, string passStartDate)//, byte[] imageFile)
        //{
        //    throw new System.NotImplementedException("Add Record method is obsolete. Please do not use.");
        //    //return CT_WSBL.getInstance().AddRecord(CPICC, northgateCaseID, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNameOrNumber,
        //    //    buildingName, street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession, disabilityPermanant, evidenceExpiryDate, passStartDate);//, imageFile);
        //}
        [WebMethod]
        public bool GetPassesForPrint()
        {
            throw new NotImplementedException();
            //return CT_WSBL.getInstance().getPassesForPrint();
        }

        [WebMethod]
        public bool GetPrintedPasses()
        {
            throw new NotImplementedException();
            //return CT_WSBL.getInstance().getPrintedPasses();
        }

        [WebMethod]
        public bool ProcessESPReports()
        {
            throw new NotImplementedException();
            //return CT_WSBL.getInstance().processESPReports();
        }

        /// <summary>
        /// Searches passholder data for current and previous values
        /// </summary>
        /// <param name="PassholderId">Record ID (Unique)</param>
        /// <param name="Surname">Current Surname</param>
        /// <param name="DateOfBirth">Date Of Birth</param>
        /// <param name="CurrentUPRN">Current Unique Property Reference</param>
        /// <param name="CurrentAddress">Current Address</param>
        /// <param name="PriorUPRN">Prevuious UPRN</param>
        /// <param name="PriorAddress">Previous Address</param>
        /// <param name="PriorSurname">Previous Surname</param>
        /// <returns></returns>
        [WebMethod]
        public XmlDocument SearchCurrentAndPreviousPassholderData(string PassholderId, string Surname, string DateOfBirth, string CurrentUPRN, string CurrentAddress, string PriorUPRN, string PriorAddress, string PriorSurname)
        {
            if (log.IsErrorEnabled) log.Error("Attempt to call SearchCurrentAndPreviousPassholderData");
            throw new NotImplementedException();
            /*
            int? iCurrentUPRN, iPriorUPRN;
            if(!string.IsNullOrEmpty(CurrentUPRN))s
                iCurrentUPRN = Convert.ToInt32(CurrentUPRN);
            else
                iCurrentUPRN = null;

            if(!string.IsNullOrEmpty(PriorUPRN))
                iPriorUPRN = Convert.ToInt32(PriorUPRN);
            else
                iPriorUPRN = null;


            return CT_WSBL.getInstance().queryPassWithCurrentOrPreviousData(PassholderId, Surname, DateOfBirth, iCurrentUPRN, CurrentAddress, iPriorUPRN, PriorAddress, PriorSurname);
        
             */
        }


        [WebMethod]
        public XmlDocument GetPassInformation(string CPICC, string PassHolderNumber, string RequestIssedSince)
        {
            if (log.IsErrorEnabled) log.Error("Attempt to call GetPassInformation.");
            throw new NotImplementedException();
            //return CT_WSBL.getInstance().GetPassInformation(CPICC, PassHolderNumber, RequestIssedSince);
        }



        [WebMethod]
        public XmlDocument ReissuePass(string oldPassNumber, string CPICC, string passHolderNumber)
        {
            if (log.IsErrorEnabled) log.Error("Attempt to call ReissuePass method.");
            throw new NotImplementedException();
            //return CT_WSBL.getInstance().ReissuePass(CPICC, passHolderNumber, oldPassNumber);
        }


        [WebMethod]
        public XmlDocument GetPassStatus(int recordID)//record ID needed instead of cpicc and passholder number
        {
            if (log.IsErrorEnabled) log.Error("Attempt to call GetPassStatus");
           // throw new NotImplementedException();
            //return CT_WSBL.getInstance().queryPassStatus(CPICC, passHolderNumber, requestIssuedSince);
            return CT_WSBL.getInstance().GetPassInformation(recordID);
        }

        #endregion

    }
}
