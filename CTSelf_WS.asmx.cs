using System;
using System.Text;
using System.Web;
using System.Web.Services;
using System.Xml;
using CTSmartCitizenConnect.CTDataLayer;
using log4net;

namespace CTSmartCitizenConnect
{
    /// <summary>
    /// Suite of Web Services specifically for Self Service implementation of Concessionary Travel
    /// </summary>
    [WebService(Namespace = "http://warwickshire.gov.uk/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]

    public class CTSelf_WS : System.Web.Services.WebService
    {

        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private string appDataPath = HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/";

        [WebMethod(Description = "Verifies that a passholder exists on CT for a renewal")]
        public XmlDocument CheckPassHolderData(string surname, string forename, string dateOfBirth, string postcode, string ISRN)
        {


            if (log.IsInfoEnabled) log.Info("Request to search CT Data received.");
            logParams(ISRN, surname, forename, postcode, dateOfBirth);
            if (log.IsDebugEnabled) log.Debug("Initialising Response document.");
            XmlDocument response = new XmlDocument();
            response.Load(appDataPath + "CTSelfPassSearchResponse.xml");
            if (log.IsDebugEnabled) log.Debug("Response document loaded.");
            if (String.IsNullOrEmpty(ISRN) || String.IsNullOrEmpty(surname) || String.IsNullOrEmpty(postcode))
                return response;
            if (ISRN.Length != 18)
                return response;
            if (log.IsDebugEnabled) log.Debug("Initialising connection to CT Data Layer.");
            
            //dataLayer = new CT_DataLayer();
            //if (log.IsDebugEnabled) log.Debug("Data Layer initialised.");
            CT_DataLayerSoapClient client = new CT_DataLayerSoapClient();

            CTPassHolder[] results = client.SearchData(surname, postcode, ISRN, false);
              
            if (log.IsDebugEnabled) log.Debug("Adding original search criteria to response");
            response.SelectSingleNode("/result/searchISRN").InnerText = ISRN;
            response.SelectSingleNode("/result/searchSurname").InnerText = surname;
            response.SelectSingleNode("/result/searchPostcode").InnerText = postcode;

            if (results.Length > 1)
            {
                if (log.IsErrorEnabled)
                { 
                    log.Error("Multiple results found for criteria.");
                    log.Error("ISRN [" + ISRN + "]");
                    log.Error("Surname [" + surname + "]");
                    log.Error("PostCode [" + postcode + "]");
                    log.Error("Returning empty result.");
                }
                
                return response;
            }

            if (results.Length < 1)
            {
                if(log.IsInfoEnabled) log.Info("No Results found for search criteria.");
                if (log.IsDebugEnabled)
                {
                
                    log.Debug("ISRN:[" + ISRN + "]");
                    log.Debug("Surname:[" + surname + "]");
                    log.Debug("PostCode:[" + postcode + "]");
             
                }
                if(log.IsInfoEnabled) log.Info("Returning empty result.");

                return response;
            }

            response.SelectSingleNode("/result/recordId").InnerText = results[0].RecordID.ToString();
            response.SelectSingleNode("/result/foreName").InnerText = results[0].FirstNameOrInitial;
            response.SelectSingleNode("/result/title").InnerText = results[0].Title;
                                                                      
            if(results[0].DateOfBirth.HasValue)
                response.SelectSingleNode("/result/dob").InnerText = results[0].DateOfBirth.Value.ToShortDateString();
            
            switch(results[0].CtPass.PassType)
            {
                case CTPassType.Disabled:
                    response.SelectSingleNode("/result/passType").InnerText = "Disabled";
                    response.SelectSingleNode("/result/disabledPassType").InnerText = "Permanent";
                    break;
                case CTPassType.DisabledTemporary:
                    response.SelectSingleNode("/result/passType").InnerText = "Disabled";
                    response.SelectSingleNode("/result/disabledPassType").InnerText = "Temporary";
                    break;
                default:
                    response.SelectSingleNode("/result/passType").InnerText = "Age";
                    break;
            }

            if (results[0].DisabilityCategory != '\0')
                response.SelectSingleNode("/result/disabilityCategory").InnerText = results[0].DisabilityCategory.ToString();

            response.SelectSingleNode("/result/expiryDate").InnerText = results[0].CtPass.ExpiryDate.ToShortDateString();
            response.SelectSingleNode("/result/gender").InnerText = results[0].Gender;
            if (results[0].PhotographBytes.Length > 3)
                response.SelectSingleNode("/result/hasPhoto").InnerText = "true";

            response.SelectSingleNode("/result/resultsFound").InnerText = results.Length.ToString();

            /*
             * <result>
  <recordId />
              <fullName />
  <dob />
  <passType />
  <disabledPassType />
  <disabilityCategory />
  <expiryDate />
 <searchISRN />
  <searchSurname />
  <searchPostcode />
</result>
             */



            if (log.IsInfoEnabled) log.Info("Returning response.");
            return response;
        }

        

        [WebMethod]
        public XmlDocument UpdateAndRenewPass(int RecordId, string Title, string ForeName, string Gender, string DateOfBirth, string DisabilityCategory, int caseNumber)
        {
            if (log.IsInfoEnabled) log.Info("Request to renew pass received for RecordID [" + RecordId + "]*");
            logParams(RecordId, Title, ForeName, DateOfBirth, DisabilityCategory, caseNumber);
            if(log.IsDebugEnabled) log.Debug("Gender:" + Gender);
            if (log.IsDebugEnabled) log.Debug("Initialising response document.");
            XmlDocument response = new XmlDocument();
            if (log.IsDebugEnabled) log.Debug("Response document initialised.");
            if (log.IsDebugEnabled) log.Debug("Initialising connection to the data layer");
            CT_DataLayerSoapClient dataLayer = new CT_DataLayerSoapClient();
            if (log.IsDebugEnabled) log.Debug("Data Layer initialised.");

            if (log.IsDebugEnabled) log.Debug("Retrieving existing record By ID.");
            CTPassHolder existingPassHolderRecord = dataLayer.RetrieveDataByID(RecordId);
            if (log.IsDebugEnabled) log.Debug("Passholder retrieved. Updating record.");
            try
            {
                if(existingPassHolderRecord.Title != Title)
                    existingPassHolderRecord.Title = Title;
                if(existingPassHolderRecord.FirstNameOrInitial.Length <= 1)
                    existingPassHolderRecord.FirstNameOrInitial = ForeName;
                
                if(log.IsDebugEnabled) log.Debug("Passholder Gender before tampering:" + existingPassHolderRecord.Gender);
                if(existingPassHolderRecord.Gender != Gender)
                    existingPassHolderRecord.Gender = Gender;

                if(existingPassHolderRecord.DateOfBirth != DateTime.Parse(DateOfBirth))
                    existingPassHolderRecord.DateOfBirth = DateTime.Parse(DateOfBirth);


                if (DisabilityCategory != String.Empty)
                    existingPassHolderRecord.DisabilityCategory = DisabilityCategory[0];
                existingPassHolderRecord.CtPass.ExpiryDate = calculateNewExpiryDate(existingPassHolderRecord.CtPass.PassType, DateOfBirth, String.Empty);
                if (log.IsDebugEnabled) log.Debug("Pass Holder Record updated. Saving to Data Layer.");
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error updating existing pass record for recordID [" + RecordId + "]");
                if (log.IsErrorEnabled) log.Error(ex.Message);
                response.LoadXml("<result>Fail</result>");
                return response;
            }

            if(log.IsDebugEnabled) log.Debug("Existing Gender:" + existingPassHolderRecord.Gender);
            dataLayer.UpdateAndRenewPass(existingPassHolderRecord, caseNumber);

            if (log.IsInfoEnabled) log.Info("Pass renew request processed. Returning response.");
            response.LoadXml("<result>Success</result>");
            return response;
        }
        
        //TODO: this is a duplicate of what is in the main CT Web Service. Extract and make generic.

        /// <summary>
        /// Gets the last birthday for a person. If their birthday has happened this year than their last birthday is this year. 
        /// If their birthday has not happened this year, their last birthday will be last year
        /// </summary>
        /// <param name="dateOfBirth">Date of Birth</param>
        /// <returns>The date that they celebrated their last birthday</returns>
        private DateTime getLastBirthday(DateTime dateOfBirth)
        {
            // Get their birth date for this year
            // Firstly, if their birthday is 29th February, set their dob to 28th February
            DateTime thisYearBirthday;
            if (dateOfBirth.Day == 29 && dateOfBirth.Month == 2)
                thisYearBirthday = new DateTime(DateTime.Now.Year, dateOfBirth.Month, 28);
            else
                thisYearBirthday = new DateTime(DateTime.Now.Year, dateOfBirth.Month, dateOfBirth.Day);

            if (thisYearBirthday.CompareTo(DateTime.Now) <= 0)
                return thisYearBirthday;
            else
                return new DateTime(DateTime.Now.Year - 1, thisYearBirthday.Month, thisYearBirthday.Day); // Need to use 'ThisYearBirthday' as we have already accounted for the 29th Feb

        }
        
        private DateTime calculateNewExpiryDate(CTPassType typeOfPass, string dateOfBirth, string evidenceExpiryDate)
        {
            DateTime expiryDate = DateTime.Now;
            DateTime evidenceExpiry = DateTime.Now;
            TimeSpan dateDiff;

            if (typeOfPass == CTPassType.Age)
            {
                if (dateOfBirth == null)
                    throw new CTDataException(20);
                expiryDate = getLastBirthday(Convert.ToDateTime(dateOfBirth)).AddYears(5);
            }

            if (typeOfPass == CTPassType.Disabled)
            {
                if (dateOfBirth == null)
                    throw new CTDataException(21);
                expiryDate = getLastBirthday(Convert.ToDateTime(dateOfBirth)).AddYears(3);
            }

            if (typeOfPass == CTPassType.DisabledTemporary)
            {
                // For temporary disabled passes we must have an evidence expiry date.
                if (!String.IsNullOrEmpty(evidenceExpiryDate))
                {
                    try
                    {
                        evidenceExpiry = Convert.ToDateTime(evidenceExpiryDate);
                    }
                    catch (FormatException ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not convert:[" + evidenceExpiryDate + "] to a DateTime object");
                        if (log.IsDebugEnabled) log.Debug("Inner Exception:" + ex.Message);
                        throw new CTDataException(4);
                    }
                    // if the evidence expires in less than 4 months, we set the pass for
                    // 6 months. If it is more than 4 months, we set the pass for 2 months after the date
                    // the evidence expires.
                    dateDiff = evidenceExpiry.Subtract(DateTime.Now);

                    if (dateDiff.TotalDays < 60)
                    {
                        throw new CTDataException(5);
                    }

                    if (dateDiff.TotalDays < 120)
                        expiryDate = DateTime.Now.AddMonths(6);

                    if (dateDiff.TotalDays >= 120 && dateDiff.TotalDays <= 1035)
                        expiryDate = evidenceExpiry.AddMonths(2);


                    if (dateDiff.TotalDays > 1035)
                    {
                        expiryDate = DateTime.Now.AddYears(3);
                    }

                }
                else
                {
                    expiryDate = getLastBirthday(Convert.ToDateTime(dateOfBirth)).AddYears(3);
                }
            }
            return expiryDate;
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
    }

    public class CTDataException : Exception
    {
        private int _errorCode;

        public int ErrorCode
        {
            get { return _errorCode; }
            set { _errorCode = value; }
        }
        public CTDataException(int errorCode)
        {
            this._errorCode = errorCode;
        }
    }
}
