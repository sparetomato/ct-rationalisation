using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Web;
using System.Xml;
//using CTBusinessProcessManager;
//using CTBusinessProcessManager.Classes;
using CTBusinessProcessManager.Classes;
using CTSmartCitizenConnect;
using CTSmartCitizenConnect.SmartConnect;
using log4net;
using System.Text;
using warwickshire.ConcessionaryTravel.Classes;


namespace warwickshire.gov.uk.CT_WS
{

    internal class CT_WSBL
    {
        private static CT_WSBL _instance;

        private Dictionary<int, string> ctPassIssueStatus = new Dictionary<int, string>();
        private Dictionary<int, string> ctErrors = new Dictionary<int, string>();
        private Dictionary<string, string> warwickshireCPICCs = new Dictionary<string, string>();
        private int _temporaryDisabledPassStandardEligibility, _agePassStandardEligibility, _temporaryDisabledPassMaximumEligibility;
        private List<string> spuriousDates;


        private static readonly ILog log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);





        public static CT_WSBL getInstance()
        {
            if (_instance == null)
                _instance = new CT_WSBL();

            return _instance;
        }

        private CT_WSBL()
        {
            //this._agePassStandardEligibility = Convert.ToInt16(ConfigurationManager.AppSettings["AgePassStandardEligibility"].ToString());
            //this._temporaryDisabledPassStandardEligibility = Convert.ToInt16(ConfigurationManager.AppSettings["TemporaryDisabledPassStandardEligibility"].ToString());
            //this._temporaryDisabledPassMaximumEligibility = Convert.ToInt16(ConfigurationManager.AppSettings["TemporaryDisabledPassMaximumEligibility"].ToString());
            initialiseInternalCollections();
        }

        private void initialiseInternalCollections()
        {
            ctPassIssueStatus.Add(0, "Successful");
            ctErrors.Add(0, "Invalid Pass Number");
            ctErrors.Add(1, "Pass number supplied is not a number");
            ctErrors.Add(2, "Pass number must be 18 characters long");
            ctErrors.Add(3, "Pass number not found in the database");
            ctErrors.Add(4, "Evidence exipry date is not a valid date");
            ctErrors.Add(5, "Evidence expires in less than 2 months. Pass cannot be issued");
            ctErrors.Add(6, "Pass type not recognised.");
            ctErrors.Add(7, "Pass start date is not a valid Date");
            ctErrors.Add(8, "Cancel pass date is not a valid Date");
            ctErrors.Add(9, "Date of birth is not a valid Date");
            ctErrors.Add(10, "Base64 String could not be converted to an image");
            ctErrors.Add(11, "Could not convert image string to a Byte Array");
            ctErrors.Add(12, "Could not flag record");
            ctErrors.Add(13, "CPICC not supplied");
            ctErrors.Add(14, "Passholder number not supplied");
            ctErrors.Add(15, "Passholder image not supplied");
            ctErrors.Add(16, "Could not invoke Asynchronous Process");
            ctErrors.Add(17, "Pass Record does not exist");
            ctErrors.Add(18, "CPICC supplied is not a valid Warwickshire CPICC");
            ctErrors.Add(19, "Pass type not present");
            ctErrors.Add(20, "Age related pass must have a date of birth supplied");
            ctErrors.Add(21, "Permanent disabled pass must have a date of birth supplied");
            ctErrors.Add(22, "No Print Reason Specified");
            ctErrors.Add(23, "At least one of ISRN, Surname or postcode must be supplied.");
            ctErrors.Add(9999, "Demonstration of an Error");

            /*string[] cpiccList = ConfigurationManager.AppSettings["ValidCPICC"].ToString().Split(',');

            for (int i = 0; i < cpiccList.Length; i++)
            {
                warwickshireCPICCs.Add(cpiccList[i].Split('=')[0], cpiccList[i].Split('=')[1]);
            }*/



        }



        #region Synchronous Processing

        internal XmlDocument GetPassInformation(int recordID)
        {
            #region log entry
            if (log.IsInfoEnabled) log.Info("Request for pass information received");
            if (log.IsDebugEnabled) log.Debug("Pass information requested for:recordID:" + recordID);
            #endregion
            XmlDocument response = new XmlDocument();

      


            /*  DateTime requestIssuedSinceDate;
              if (!String.IsNullOrEmpty(requestIssuedSince))
              {
                  try
                  {
                      requestIssuedSinceDate = Convert.ToDateTime(requestIssuedSince);
                  }
                  catch (FormatException ex)
                  {
                      if (log.IsErrorEnabled) log.Error("Could not convert Request Issued Since to a DateTime:" + requestIssuedSince);
                      if (log.IsErrorEnabled) log.Error(ex.Message);
                      processError(ref response, 7);
                      return response;
                  }
              }
              else
              {
                  requestIssuedSinceDate = new DateTime(2011, 04, 01); // This is the first date we will have in the database.
              }
              */
            SmartCitizenConnector dataLayer = new SmartCitizenConnector();
            EntityDetailsListResponse latestPass = dataLayer.getPassStatus(recordID);
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTQueryPassStatusResponse.xml");
            response.SelectSingleNode("GetPassStatusResult/status").InnerText= latestPass.Status;
            return response;

        }









          /*  //XmlNode passHolderInformation = dataLayer.RetrieveData(CPICC, passHolderNumber, String.Empty);

            CTPassHolder passHolderInformation = dataLayer.RetrieveData(CPICC, passHolderNumber, String.Empty);

            XmlNode dataFromPrintQueue = dataLayer.GetPassFromPrintQueue(CPICC, passHolderNumber, requestIssuedSinceDate);

            XmlNode passFlags = dataLayer.GetPassRecordFlags(CPICC, passHolderNumber);

            #region Information about Print Requests
            // Process information from print queue
            if (dataFromPrintQueue.SelectNodes("PassPrintRequest").Count > 0)
            {
                if (log.IsInfoEnabled) log.Info("Processing pass printed request");
                // There is at least one pass request pending/printed.
                if (log.IsDebugEnabled) log.Debug("Getting last print request");
                //XmlNode lastPrintRequest = dataFromPrintQueue.SelectNodes("PassPrintRequest")[dataFromPrintQueue.SelectNodes("PassPrintRequest").Count - 1];
                XmlNode lastPrintRequest = dataFromPrintQueue.SelectNodes("PassPrintRequest")[0];
                if (log.IsDebugEnabled) log.Debug("Last Print Request[" + lastPrintRequest.OuterXml + "]");
                response.SelectSingleNode("//Status").InnerText = lastPrintRequest["Status"].InnerText;

                if (response.SelectSingleNode("//Status").InnerText == "Pending")
                {
                    if (log.IsDebugEnabled) log.Debug("Status is pending. Getting date sent");
                    response.SelectSingleNode("//DateSentToBureau").InnerText = lastPrintRequest["DateReceivedInPrintQueue"].InnerText;

                    if (log.IsDebugEnabled) log.Debug("Calculating when we expect the printed date");
                    DateTime expectedPrintDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DateReceivedInPrintQueue").InnerText);
                    TimeSpan elapsed = DateTime.Now.Subtract(expectedPrintDate);
                    if (elapsed.TotalDays < 0)
                    {
                        response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "Neg";
                        response.SelectSingleNode("//DaysSincePrinted3").InnerText = "Neg";
                    }
                    else
                    {
                        if (log.IsDebugEnabled) log.Debug("Recording the days since sento to bureau");
                        response.SelectSingleNode("//DaysSinceSentToBureau").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                        response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = response.SelectSingleNode("//DaysSinceSentToBureau").InnerText;
                        while (response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText.Length < 3)
                        {
                            response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "0" + response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText;
                        }
                    }
                }


                if (lastPrintRequest.SelectSingleNode("//ExportedToBureau").InnerText == "True")
                {
                    if (log.IsDebugEnabled) log.Debug("Pass has been sent to the bureau");
                    response.SelectSingleNode("//SentToBureau").InnerText = "True";
                    response.SelectSingleNode("//DateSentToBureau").InnerText = lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText;

                    // if the Date sent to bureau is a date, work out how many days since it was sent
                    try
                    {
                        if (log.IsDebugEnabled) log.Debug("Calculating days since sent to bureau");
                        DateTime lastPrintRequestDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText);
                        TimeSpan elapsed = DateTime.Now.Subtract(lastPrintRequestDate);
                        response.SelectSingleNode("//DaysSinceSentToBureau").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                        if (elapsed.TotalDays < 0)
                        {
                            response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "Neg";
                        }
                        else
                        {
                            response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = response.SelectSingleNode("//DaysSinceSentToBureau").InnerText;
                            while (response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText.Length < 3)
                            {
                                response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "0" + response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText;
                            }
                        }
                    }
                    catch (FormatException ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not convert Date sent to Bureau to a Date/Time. Value is:[" + lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText + "]");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not subtract last print request date from current date.");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled) log.Error("An Error occurred setting the Date Exported value.");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                    if (lastPrintRequest.SelectSingleNode("//Printed").InnerText == "True")
                    {
                        if (log.IsDebugEnabled) log.Debug("Pass has been printed. Populating printed details");
                        response.SelectSingleNode("//Printed").InnerText = "True";
                        response.SelectSingleNode("//DatePrinted").InnerText = lastPrintRequest.SelectSingleNode("//DatePrinted").InnerText;

                        // if the Date sent to bureau is a date, work out how many days since it was sent
                        try
                        {
                            DateTime lastPrintedDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DatePrinted").InnerText);
                            TimeSpan elapsed = DateTime.Now.Subtract(lastPrintedDate);
                            response.SelectSingleNode("//DaysSincePrinted").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                            response.SelectSingleNode("//DaysSincePrinted3").InnerText = response.SelectSingleNode("//DaysSincePrinted").InnerText;
                            while (response.SelectSingleNode("//DaysSincePrinted3").InnerText.Length < 3)
                            {
                                response.SelectSingleNode("//DaysSincePrinted3").InnerText = "0" + response.SelectSingleNode("//DaysSincePrinted3").InnerText;
                            }

                        }
                        catch (FormatException ex)
                        {
                            if (log.IsErrorEnabled) log.Error("Could not convert Date sent to Bureau to a Date/Time. Value is:[" + lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText + "]");
                            if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                        }
                        catch (ArgumentOutOfRangeException ex)
                        {
                            if (log.IsErrorEnabled) log.Error("Could not subtract last print request date from current date.");
                            if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                        }
                        catch (Exception ex)
                        {
                            if (log.IsErrorEnabled) log.Error("An Error occurred setting the Date Exported value.");
                            if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                        }
                    }
                    else
                    {
                        if (log.IsDebugEnabled) log.Debug("Pass has not yet been printed.");
                        response.SelectSingleNode("//Printed").InnerText = "False";
                    }
                }
                else
                {
                    response.SelectSingleNode("//SentToBureau").InnerText = "False";
                    response.SelectSingleNode("//Printed").InnerText = "False";
                }
                if (log.IsDebugEnabled) log.Debug("Capturing the reason the pass was sent to print.");
                response.SelectSingleNode("//PrintReason").InnerText = lastPrintRequest.SelectSingleNode("//PrintReason").InnerText;
            }

            if (log.IsDebugEnabled) log.Debug("Output of response after calculating print details[" + response.OuterXml + "]");

            #endregion

            if (log.IsDebugEnabled) log.Debug("Querying days to pass expiry");
            response.SelectSingleNode("//DaysToExpiry").InnerText = passHolderInformation.CtPass.DaysToExpiry.ToString();

            if (log.IsDebugEnabled) log.Debug("Setting days since the photo was updated");
            response.SelectSingleNode("//DaysSincePhotoUpdated").InnerText = passHolderInformation.DaysSincePhotoUpdated.ToString();

            if (log.IsDebugEnabled) log.Debug("Recording Gender");
            if (passHolderInformation.Gender != null)
                response.SelectSingleNode("//Gender").InnerText = passHolderInformation.Gender.ToString();

            if (log.IsDebugEnabled) log.Debug("Recording disability Category");
            if (passHolderInformation.DisabilityCategory != '\0')
                response.SelectSingleNode("//DisabilityCategory").InnerText = passHolderInformation.DisabilityCategory.ToString();

            /*
            if (!String.IsNullOrEmpty(passHolderInformation.SelectSingleNode("//DaysToExpiry").InnerText))
            {
                response.SelectSingleNode("//DaysToExpiry").InnerText = passHolderInformation.SelectSingleNode("//DaysToExpiry").InnerText;
            }

            if(!String.IsNullOrEmpty(passHolderInformation.SelectSingleNode("//DaysSincePhotoUpdated").InnerText))
            {
                response.SelectSingleNode("//DaysSincePhotoUpdated").InnerText = passHolderInformation.SelectSingleNode("//DaysSincePhotoUpdated").InnerText;
            }
            if (log.IsDebugEnabled) log.Debug("Checking the Whereabouts Unknown flag");
            if (passFlags.SelectNodes("//Flag[text() = \"Whereabouts Unknown\"]").Count > 0)
            {
                response.SelectSingleNode("//WhereaboutsUnknown").InnerText = "True";
            }
            else
            {
                response.SelectSingleNode("//WhereaboutsUnknown").InnerText = "False";
            }

            #region log exit
            if (log.IsInfoEnabled) log.Info("Returning Pass information");
            if (log.IsDebugEnabled) log.Debug("Pass information returned:" + response.OuterXml);
            #endregion
            return response;
        }
         */
        /*
        [Obsolete("This method has been superceded by GetPassInformation", false)]
        internal XmlDocument queryPassStatus(string CPICC, string passHolderNumber, string requestIssuedSince)
        {
            if (log.IsDebugEnabled) log.Debug("Processing request for pass status query");

            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTQueryPassStatusResponse.xml");

            try
            {
                if (log.IsDebugEnabled) log.Debug("Validating CPICC[" + CPICC + "]");
                validateCPICC(CPICC);
                if (log.IsDebugEnabled) log.Debug("Validating PassHolderNumber[" + passHolderNumber + "]");
                validateISRN(passHolderNumber);
            }
            catch (CTDataException ex)
            {
                processError(ref response, ex.ErrorCode);
                return response;
            }

            DateTime requestIssuedSinceDate;
            if (!String.IsNullOrEmpty(requestIssuedSince))
            {
                try
                {
                    requestIssuedSinceDate = Convert.ToDateTime(requestIssuedSince);
                }
                catch (FormatException ex)
                {
                    if (log.IsErrorEnabled) log.Error("Could not convert Request Issued Since to a DateTime:" + requestIssuedSince);
                    if (log.IsErrorEnabled) log.Error(ex.Message);
                    processError(ref response, 7);
                    return response;
                }
            }
            else
            {
                requestIssuedSinceDate = new DateTime(2011, 04, 01); // This is the first date we will have in the database.
            }


            CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();

            XmlNode dataLayerResponse = dataLayer.GetPassFromPrintQueue(CPICC, passHolderNumber, requestIssuedSinceDate);

            XmlNodeList printRequests = dataLayerResponse.SelectNodes("//PassPrintRequest");

            if (printRequests.Count < 1)
            {
                if (log.IsDebugEnabled) log.Debug("No print requests found for CPICC[" + CPICC + "] pass holder number[" + passHolderNumber + "] from date[" + requestIssuedSince + "]");
                return response;
            }

            XmlNode lastPrintRequest = printRequests[printRequests.Count - 1];

            response.SelectSingleNode("//status").InnerText = lastPrintRequest.SelectSingleNode("//Status").InnerText;
            if (response.SelectSingleNode("//status").InnerText == "Pending")
            {
                response.SelectSingleNode("//DateSentToBureau").InnerText = lastPrintRequest.SelectSingleNode("//DateReceivedInPrintQueue").InnerText;
                DateTime expectedPrintDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DateReceivedInPrintQueue").InnerText);
                TimeSpan elapsed = DateTime.Now.Subtract(expectedPrintDate);
                if (elapsed.TotalDays < 0)
                {
                    response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "Neg";
                    response.SelectSingleNode("//DaysSincePrinted3").InnerText = "Neg";
                }
                else
                {
                    response.SelectSingleNode("//DaysSinceSentToBureau").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                    response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = response.SelectSingleNode("//DaysSinceSentToBureau").InnerText;
                    while (response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText.Length < 3)
                    {
                        response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "0" + response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText;
                    }
                }
            }


            if (lastPrintRequest.SelectSingleNode("//ExportedToBureau").InnerText == "True")
            {
                response.SelectSingleNode("//SentToBureau").InnerText = "True";
                response.SelectSingleNode("//DateSentToBureau").InnerText = lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText;

                // if the Date sent to bureau is a date, work out how many days since it was sent
                try
                {
                    DateTime lastPrintRequestDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText);
                    TimeSpan elapsed = DateTime.Now.Subtract(lastPrintRequestDate);
                    response.SelectSingleNode("//DaysSinceSentToBureau").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                    if (elapsed.TotalDays < 0)
                    {
                        response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "Neg";
                    }
                    else
                    {
                        response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = response.SelectSingleNode("//DaysSinceSentToBureau").InnerText;
                        while (response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText.Length < 3)
                        {
                            response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText = "0" + response.SelectSingleNode("//DaysSinceSentToBureau3").InnerText;
                        }
                    }
                }
                catch (FormatException ex)
                {
                    if (log.IsErrorEnabled) log.Error("Could not convert Date sent to Bureau to a Date/Time. Value is:[" + lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText + "]");
                    if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    if (log.IsErrorEnabled) log.Error("Could not subtract last print request date from current date.");
                    if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                }
                catch (Exception ex)
                {
                    if (log.IsErrorEnabled) log.Error("An Error occurred setting the Date Exported value.");
                    if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                }
                if (lastPrintRequest.SelectSingleNode("//Printed").InnerText == "True")
                {
                    response.SelectSingleNode("//Printed").InnerText = "True";
                    response.SelectSingleNode("//DatePrinted").InnerText = lastPrintRequest.SelectSingleNode("//DatePrinted").InnerText;

                    // if the Date sent to bureau is a date, work out how many days since it was sent
                    try
                    {
                        DateTime lastPrintedDate = Convert.ToDateTime(lastPrintRequest.SelectSingleNode("//DatePrinted").InnerText);
                        TimeSpan elapsed = DateTime.Now.Subtract(lastPrintedDate);
                        response.SelectSingleNode("//DaysSincePrinted").InnerText = Math.Round(elapsed.TotalDays, 0).ToString();
                        response.SelectSingleNode("//DaysSincePrinted3").InnerText = response.SelectSingleNode("//DaysSincePrinted").InnerText;
                        while (response.SelectSingleNode("//DaysSincePrinted3").InnerText.Length < 3)
                        {
                            response.SelectSingleNode("//DaysSincePrinted3").InnerText = "0" + response.SelectSingleNode("//DaysSincePrinted3").InnerText;
                        }

                    }
                    catch (FormatException ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not convert Date sent to Bureau to a Date/Time. Value is:[" + lastPrintRequest.SelectSingleNode("//DateExportedToBureau").InnerText + "]");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        if (log.IsErrorEnabled) log.Error("Could not subtract last print request date from current date.");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                    catch (Exception ex)
                    {
                        if (log.IsErrorEnabled) log.Error("An Error occurred setting the Date Exported value.");
                        if (log.IsErrorEnabled) log.Error("Exception:" + ex.Message);
                    }
                }
                else
                {
                    response.SelectSingleNode("//Printed").InnerText = "False";
                }
            }
            else
            {
                response.SelectSingleNode("//SentToBureau").InnerText = "False";
                response.SelectSingleNode("//Printed").InnerText = "False";
            }

            response.SelectSingleNode("//PrintReason").InnerText = lastPrintRequest.SelectSingleNode("//PrintReason").InnerText;



            return response;

        }*/

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



        #endregion


        #region Asynchronous properties & methods
        /*
        // Create a delegate to run in its own thread.
        private delegate void CTBPMWorkerDelegate(CTData jobData, CTJob.CTJobType type, DateTime? jobStart);
        private void CTBPMWorker(CTData jobData, CTJob.CTJobType type, DateTime? jobStart)
        {
            if (log.IsDebugEnabled) log.Debug("Invoking Engine on thread:" + Thread.CurrentThread.ManagedThreadId);

            CTBPMEngine.getInstance().startJob(jobData, type, jobStart);
            if (log.IsDebugEnabled) log.Debug("Engine Invoked.");

        }


        internal XmlDocument AddRecord(string CPICC, string NGCaseID, string passHolderNumber, string firstNameOrInitial, string surname, string houseOrFlatNumberOrName,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode, string title,
            string dateOfBirth, string typeOfConcession, string disabilityPermanent, string evidenceExpiryDate, string passStartDate, string UPRN)//, byte[]imageFile)
        {
            CTBPMWorkerDelegate worker = new CTBPMWorkerDelegate(CTBPMWorker);

            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTAddRecordResponse.xml");

            CTData jobData = new CTData();

            try
            {
                jobData = buildJobData(CPICC, NGCaseID, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNumberOrName, buildingName, street, villageOrDistrict, townCity, county,
                    postcode, title, dateOfBirth, typeOfConcession, disabilityPermanent, evidenceExpiryDate, passStartDate, null, null, true, null, UPRN);
            }
            catch (CTDataException ex)
            {
                processError(ref response, ex.ErrorCode);
                return response;
            }

            if (log.IsDebugEnabled) log.Debug("Starting Asynchronous Operation");
            worker.BeginInvoke(jobData, CTJob.CTJobType.AddRecord, null, null, null);
            if (log.IsDebugEnabled) log.Debug("Asynchronous Process Started, returning from initial call.");


            response.SelectSingleNode("addRecordResponse/status").InnerText = "success";


            return response;
        }*/

        internal XmlDocument IssuePass(string CPICC, string NGCaseID, string firstNameOrInitial, string surname, string houseOrFlatNumberOrName,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode, string title,
            string dateOfBirth, string typeOfConcession, string disabilityPermanent, string evidenceExpiryDate, string passStartDate, string imageAsBase64String, string passPrintReason, string gender, string disabilityCategory, string UPRN, SmartCitizenConnector.Proof addressProof, SmartCitizenConnector.Proof ageProof, SmartCitizenConnector.Proof disabillityProof)
        {
            if (log.IsInfoEnabled) log.Info("Request to issue pass received from Firmstep Case ID:" + NGCaseID);
            logParams(CPICC, NGCaseID, firstNameOrInitial, surname, houseOrFlatNumberOrName, buildingName, street, villageOrDistrict, townCity, county, postcode, title,
                dateOfBirth, typeOfConcession, disabilityPermanent, evidenceExpiryDate, passStartDate, imageAsBase64String, passPrintReason, gender, disabilityCategory, UPRN);

            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTIssuePassResponse.xml");
           SmartCitizenConnector dataLayer = new SmartCitizenConnector();

           CTPassType typeOfPass = CTPassType.NotSet;
           if (!String.IsNullOrEmpty(typeOfConcession))
           {
               if (typeOfConcession.ToUpper() == "AGE")
               {
                   typeOfPass = CTPassType.Age;
               }

               else if (typeOfConcession.ToUpper() == "ELIGIBLE DISABLED")
               {
                   if (disabilityPermanent.ToUpper() == "YES")
                       typeOfPass = CTPassType.Disabled;
                   else
                       typeOfPass = CTPassType.DisabledTemporary;
               }
               else
               {
                   throw new CTDataException(6);
               }
           }
           else
           {
               throw new CTDataException(19);
           }

            CTPass newPass = dataLayer.IssuePass(title, firstNameOrInitial, surname, dateOfBirth, gender, String.Empty, String.Empty, String.Empty, buildingName, houseOrFlatNumberOrName, street, villageOrDistrict, townCity, county, UPRN, CPICC, postcode, imageAsBase64String, NGCaseID, typeOfPass,disabilityCategory, addressProof, ageProof, disabillityProof);
            /*
            if (String.IsNullOrEmpty(passPrintReason))
                passPrintReason = "New Pass Issue";
            CTData jobData = new CTData();
            try
            {
                jobData = buildJobData(CPICC, NGCaseID, null, firstNameOrInitial, surname, houseOrFlatNumberOrName, buildingName,
                 street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession, disabilityPermanent,
                 evidenceExpiryDate, passStartDate, null, convertImage(imageAsBase64String), true, passPrintReason, UPRN);
            }
            catch (CTDataException ex)
            {
                processError(ref response, ex.ErrorCode);
                return response;
            }

            if (!String.IsNullOrEmpty(gender))
                jobData.Gender = gender;

            if (jobData.ShortPassType == 'D' && !String.IsNullOrEmpty(disabilityCategory))
                jobData.DisabilityCategory = disabilityCategory[0];


            //DateTime? jobStart = null;
            //if (!String.IsNullOrEmpty(passStartDate))
            //{
            //    if (DateTime.Compare(DateTime.Parse(passStartDate), DateTime.Now) > 1)
            //    {
            //        try
            //        {
            //            jobStart = DateTime.Parse(passStartDate);
            //            // add in the lead time
            //            DateTime leadDate = (DateTime)jobStart;
            //            TimeSpan leadTimeSpan = new TimeSpan(Convert.ToInt32(ConfigurationManager.AppSettings["DelayedIssueLeadTime"].ToString()), 0, 0, 0);
            //            jobStart = leadDate.Subtract(leadTimeSpan);
            //        }
            //        catch (FormatException ex)
            //        {
            //            if (log.IsErrorEnabled) log.Error("Could not convert Pass Start Date to a valid Date/Time. Value is:[" + passStartDate + "]");
            //            if (log.IsDebugEnabled) log.Debug("Inner Exception:" + ex.Message);
            //            processError(ref response, 7);
            //        }
            //    }
            //    else jobStart = DateTime.Now;
            //}

            try
            {
                startAsynchronousPassRequest(jobData, CTJob.CTJobType.IssuePass);
            }
            catch (CTDataException ex)
            {
                processError(ref response, ex.ErrorCode);
                return response;
            }
            */
            response.SelectSingleNode("issuePassResponse/status").InnerText = "success";

            response.SelectSingleNode("issuePassResponse/passExpiryDate").InnerText =
                newPass.ExpiryDate.ToShortDateString();
            if (log.IsDebugEnabled) log.Debug("Returned XML:" + response.OuterXml);
            if (log.IsDebugEnabled) log.Debug("Exiting Method.");
            return response;
        }



        #endregion

        #region Private Methods

        private void initSpuriousDates()
        {
            if(spuriousDates == null)
            {
                spuriousDates = new List<string>();
                string[] spuriousDateArray = ConfigurationManager.AppSettings["spuriousDates"].Split(',');
                foreach(string date in spuriousDateArray)
                {
                    spuriousDates.Add(date);
                }    
            }
        }

        private bool isSpuriousDate(string dateToCheck)
        {
        if(spuriousDates == null || spuriousDates.Count == 0)
            initSpuriousDates();

        return spuriousDates.Contains(dateToCheck);
        }

        #endregion


        private string formatPostcode(string postcode)
        {
            postcode = postcode.ToUpper().Trim().Replace(" ", "").Replace("  ", "");
            if (postcode.Length == 6)
                postcode = postcode.Substring(0, 3) + " " + postcode.Substring(3);
            else if (postcode.Length == 7)
                postcode = postcode.Substring(0, 4) + " " + postcode.Substring(4);

            return postcode;
        }

        private void buildJobData(ref SmartCitizenCTPassholder existingPassHolder, string CPICC, string NGCaseID, string passHolderNumber, string firstNameOrInitial, string surname, string houseOrFlatNumberOrName,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode, string title,
            string dateOfBirth, string typeOfConcession, string disabilityPermanent, string evidenceExpiryDate, byte[] imageAsBytes, string UPRN)
        {
            if (log.IsDebugEnabled) log.Debug("Updating Existing Passholder Details");


            existingPassHolder.FirstNameOrInitial = firstNameOrInitial.ToTitleCase();
            existingPassHolder.Surname = surname.ToTitleCase();
            existingPassHolder.HouseOrFlatNumberOrName = houseOrFlatNumberOrName.ToTitleCase();
            existingPassHolder.BuildingName = buildingName.ToTitleCase();
            existingPassHolder.Street = street.ToTitleCase();
            existingPassHolder.VillageOrDistrict = villageOrDistrict.ToTitleCase();
            existingPassHolder.TownCity = townCity.ToTitleCase();
            existingPassHolder.County = county.ToTitleCase();
            existingPassHolder.PostCode = formatPostcode(postcode);
            existingPassHolder.Title = title.ToTitleCase();
            existingPassHolder.CPICC = CPICC;
            existingPassHolder.RecordID = Convert.ToInt32(passHolderNumber);
            existingPassHolder.UPRN = UPRN;

            
            CTPassType typeOfPass = CTPassType.NotSet;
            if (!String.IsNullOrEmpty(typeOfConcession))
            {
                if (typeOfConcession.ToUpper() == "AGE")
                {
                    typeOfPass = CTPassType.Age;
                }

                else if (typeOfConcession.ToUpper() == "ELIGIBLE DISABLED")
                {
                    if (disabilityPermanent.ToUpper() == "YES")
                        typeOfPass = CTPassType.Disabled;
                    else
                        typeOfPass = CTPassType.DisabledTemporary;
                }
                else
                {
                    throw new CTDataException(6);
                }
            }
            else
            {
                throw new CTDataException(19);
            }

            if (typeOfPass != CTPassType.NotSet)
            {
                existingPassHolder.CtPass.PassType = typeOfPass;  
            }

            if (!String.IsNullOrEmpty(NGCaseID))
                existingPassHolder.CtPass.NorthgateCaseNumber = NGCaseID;

            if (!String.IsNullOrEmpty(dateOfBirth))
                existingPassHolder.DateOfBirth = Convert.ToDateTime(dateOfBirth);



            if (imageAsBytes != null && imageAsBytes.Length > 0)
                existingPassHolder.PhotographBytes = imageAsBytes;



            if (log.IsDebugEnabled) log.Debug("Existing Pass Data updated with new values");


        }

        internal XmlDocument updatePassImage(string CPICC, string passHolderNumber, string passImageString)
        {
            //if (log.IsDebugEnabled) log.Debug("Updating Pass Image for CPICC:" + CPICC + " - PassholderNumber " + passHolderNumber);
            //validateCPICC(CPICC);
            if (log.IsDebugEnabled) log.Debug("Updating pass image for passholder:" + passHolderNumber);

            if (log.IsDebugEnabled) log.Debug("Pass Image String:" + passImageString);
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTUpdateImageResponse.xml");

            // verify that the pass holder exists in the database
            SmartCitizenConnector dataLayer = new SmartCitizenConnector();
            CTPassHolder existingPassHolderDetails = dataLayer.GetCtPassHolder(passHolderNumber);

            if(existingPassHolderDetails == null)
            {
                if (log.IsErrorEnabled) log.Error("Could not locate Pass Holder Number:[" + passHolderNumber + "] with CPICC:[" + CPICC + "]");
                processError(ref response, 17);
                return response;
            }

            byte[] passImageAsBytes;
            try
            {
                if (log.IsDebugEnabled) log.Debug("Attempting to convert to an image from a byte array");
                passImageAsBytes = Convert.FromBase64String(passImageString);
                ImageConverter ic = new ImageConverter();
                Image passImage = (Image)ic.ConvertFrom(passImageAsBytes);
                if (log.IsDebugEnabled) log.Debug("Converted OK.");
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not update pass image:" + ex.Message + "for CPICC:" + CPICC + " - PassHolderNumber: " + passHolderNumber);
                processError(ref response, 10);
                return response;
            }

            try
            {
                if (log.IsDebugEnabled) log.Debug("Calling Method on Data Layer");
                if (log.IsDebugEnabled) log.Debug("Pass Image as Bytes:" + Convert.ToBase64String(passImageAsBytes));
                dataLayer.UpdatePassImage(passHolderNumber, passImageString);
                //datalayer.UpdateImage(CPICC, passHolderNumber, passImageAsBytes);

            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Debug("Could not update image in database:" + ex.Message);
                if (log.IsErrorEnabled) log.Error("CPICC:" + CPICC + " Passholdernumber:" + passHolderNumber + "Pass Image:" + Convert.ToBase64String(passImageAsBytes));
                processError(ref response, 10);
                return response;
            }

            response.SelectSingleNode("updateImageResponse/status").InnerText = "success";
            return response;
        }

        internal XmlDocument flagPass(string CPICC, string passHolderNumber, string FlagDescription)
        {

            logParams(CPICC, passHolderNumber, FlagDescription);
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTFlagRecordResponse.xml");

            //CT_DataLayer dataLayer = new CT_DataLayer();

            SmartCitizenConnector dataLayer = new SmartCitizenConnector();



            if (log.IsDebugEnabled) log.Debug("Flagging pass in Data Layer");
            try
            {
                dataLayer.FlagPassHolder(passHolderNumber, FlagDescription);
            }
            catch(Exception ex)
            
            {
                processError(ref response, 12);
                return response;
            }

                response.SelectSingleNode("//status").InnerText = "success";
                return response;
            
        }


        /*
        internal XmlDocument ReissuePass(string CPICC, string passHolderNumber, string oldPassNumber)
        {
            if (log.IsDebugEnabled) log.Debug("Reissue Pass request received");
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTIssuePassResponse.xml");
            // trim any spaces
            if (!String.IsNullOrEmpty(oldPassNumber))
            {
                oldPassNumber = oldPassNumber.Replace(" ", "");
                try
                {
                    validateISRN(oldPassNumber);
                }
                catch (CTDataException ex)
                {
                    processError(ref response, ex.ErrorCode);
                }
            }
            CTData jobData = new CTData();
            jobData.ISRN = oldPassNumber;
            jobData.PassHolderNumber = passHolderNumber;
            jobData.CPICC = CPICC;

            jobData.PrintReason = "Replacement";

            //get the previous print reason.
            //CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();
            //dataLayer.GetPassFromPrintQueue(CPICC, passHolderNumber, new DateTime(2011,4,1));



            try
            {
                startAsynchronousPassRequest(jobData, CTJob.CTJobType.ReissuePass);
                response.SelectSingleNode("issuePassResponse/status").InnerText = "success";
            }
            catch (CTDataException ex)
            {
                processError(ref response, ex.ErrorCode);
            }
            return response;
        }*/


        //02/06/2014 - added spurious date check. If any date is in our list of spurious dates (in web.config) we return an empty node. JC Requirement.
        //04/06/2014 - added UPRN and RecordID into return values.
        //04/06/2014 - Refactored code to re-use buildCustomerResponse to provide consistent responses from queries.
        internal XmlDocument queryPass(string forename, string surname, string postcode, string dateOfBirth, string passNo)
        {
            
            if (log.IsInfoEnabled) log.Info("Query Pass request received");
            logParams(forename, surname, postcode, passNo);
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTQueryPassResponse.xml");

            if ((surname + postcode + passNo).Length == 0)
            {
                processError(ref response, 23);
                return response;
            }

            XmlNode customerTemplate = response.SelectSingleNode("//customer").CloneNode(true);
            response.DocumentElement.RemoveAll();

            //CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();
            SmartCitizenConnector dataLayer = new SmartCitizenConnector();
            CTPassHolder[] searchResults = dataLayer.SearchPassHolders(surname, forename, dateOfBirth, postcode, passNo);
            //CTPassHolder[] searchResults = dataLayer.SearchPassHolders(surname, forename, dateOfBirth, postcode, passNo);

            if (log.IsInfoEnabled) log.Info("Processing Search Result(s)");
            foreach (CTPassHolder searchResult in searchResults)
            {

                XmlNode customer = customerTemplate.CloneNode(true);
                buildCustomerSearchResponse(ref customer, searchResult);
                response.DocumentElement.AppendChild(customer);
            }

            // Firmstep 19-03-2014. Added an Empty Customer node if searching by surname and postcode CS Request
            if (String.IsNullOrEmpty(passNo))
            {
                XmlNode blankCustomerNode = customerTemplate.CloneNode(true);
                blankCustomerNode.SelectSingleNode("//ISRN").InnerText = "No Match Found";
                response.DocumentElement.AppendChild(blankCustomerNode);
            }


            if (log.IsDebugEnabled) log.Debug("Response:" + response.OuterXml);
            if (log.IsInfoEnabled) log.Info("Returning Search Response.");
            //response.LoadXml(dataLayer.SearchData(surname, postcode, passNo, false).OuterXml);
            return response;
        }

        internal XmlDocument queryPassFromPassholderNumber(string CPICC, string passHolderNumber)
        {
            logParams(CPICC, passHolderNumber);
            XmlDocument response = new XmlDocument();
            if (log.IsDebugEnabled) log.Debug("Loading response XML");
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTQueryPassResponse.xml");
            if(log.IsDebugEnabled) log.Debug("Response XML Loaded.");

            if(log.IsDebugEnabled) log.Debug("Cloning the Customer node.");
            XmlNode customer = response.SelectSingleNode("//customer").CloneNode(true);
            response.DocumentElement.RemoveAll();
            if(log.IsDebugEnabled) log.Debug("Customer node cloned and document cleared.");

            //CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();
            SmartCitizenConnector dataLayer = new SmartCitizenConnector();
            if(log.IsDebugEnabled) log.Debug("Calling Data Layer.");
            CTPassHolder searchResult = dataLayer.GetCtPassHolder(passHolderNumber);
            if (searchResult == null)
            {

                processError(ref response, 3);
                return response;
            }
            if(log.IsDebugEnabled)log.Debug("Data Layer successfully called and a result returned.");

            buildCustomerSearchResponse(ref customer, searchResult);
            response.DocumentElement.AppendChild(customer);

            return response;
        }


        /*internal XmlDocument queryPassWithCurrentOrPreviousData(string PassholderId, string Surname, string DateOfBirth, int? CurrentUPRN, string CurrentAddress, int? PriorUPRN, string PriorAddress, string PriorSurname)
        {
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTQueryPassCurrentAndHistoricalResponse.xml");
            if (log.IsDebugEnabled) log.Debug("Entering");
            logParams(PassholderId, Surname, DateOfBirth, CurrentUPRN, CurrentAddress, PriorUPRN, PriorAddress, PriorSurname);
            // Load response XML
            // Search for passholder data
            CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();
            CTPassHolder[] searchResults = dataLayer.SearchCurrentAndPreviousData(PassholderId, Surname, DateOfBirth, CurrentUPRN, CurrentAddress, PriorUPRN, PriorAddress, PriorSurname);

            XmlNode customerTemplate = response.SelectSingleNode("//customer").CloneNode(true);
            response.DocumentElement.RemoveAll();
            
            foreach(CTPassHolder result in searchResults)
            {

                #region Debug logging of results from DataLayer...
                if (log.IsDebugEnabled)
                {
                    foreach (var prop in result.GetType().GetProperties())
                    {
                        if (prop.PropertyType == typeof(CTPass))
                        {
                            log.Debug("CTPass Values:");
                            foreach (var subProp in result.CtPass.GetType().GetProperties())
                            {
                                if (subProp.PropertyType == typeof(DateTime))
                                {
                                    string dateString = ((DateTime)subProp.GetValue(result.CtPass, null)).ToShortDateString();
                                    log.Debug(subProp.Name + "(" + subProp.PropertyType + ")" + ": " + dateString);
                                }
                                else
                                {
                                    log.Debug(subProp.Name + "(" + subProp.PropertyType + ")" + ": " + subProp.GetValue(result.CtPass, null).ToString());
                                }
                            }

                        }
                        else
                        {
                            if (prop.GetValue(result, null) != null)
                            {
                                if (prop.PropertyType == typeof(byte[]))
                                {
                                    log.Debug(prop.Name + "(" + prop.PropertyType + ")" + ": Image");
                                }
                                else if (prop.PropertyType == typeof(DateTime))
                                {
                                    string dateString = ((DateTime)prop.GetValue(result, null)).ToShortDateString();
                                    log.Debug(prop.Name + "(" + prop.PropertyType + ")" + ": " + dateString);
                                }
                                else if (prop.PropertyType == typeof(System.Nullable<DateTime>))
                                {
                                    //if (log.IsDebugEnabled) log.Debug("Property is Nullable DateTime");
                                    if (prop.GetValue(result, null) != null)
                                    {
                                        string dateString = ((DateTime)prop.GetValue(result, null)).ToShortDateString();
                                        log.Debug(prop.Name + "(" + prop.PropertyType + ")" + ": " + dateString);
                                    }
                                }
                                else if (prop.PropertyType == typeof(char))
                                {
                                    if (Convert.ToChar(prop.GetValue(result, null)) != '\0')
                                        log.Debug(prop.Name + "(" + prop.PropertyType + ")" + ": " + prop.GetValue(result, null).ToString());
                                }
                                else
                                {
                                    log.Debug(prop.Name + "(" + prop.PropertyType + ")" + ": " + prop.GetValue(result, null).ToString());
                                }
                            }

                        }



                    }
                }
                #endregion
                XmlNode customerNode = customerTemplate.CloneNode(true);
                customerNode.SelectSingleNode("//RecordId").InnerText = result.RecordID.ToString();

                switch (result.CtPass.PassType)
                {
                    case CTPassType.Age:
                        customerNode.SelectSingleNode("//PassType").InnerText = "age";
                        break;
                    case CTPassType.Disabled:
                        customerNode.SelectSingleNode("//PassType").InnerText = "perm disabled";
                        break;
                    case CTPassType.DisabledTemporary:
                        customerNode.SelectSingleNode("//PassType").InnerText = "temp disabled";
                        break;
                    case CTPassType.NotSet:
                        customerNode.SelectSingleNode("//PassType").InnerText = "not set";    // Should never happen.
                        break;
                }

                customerNode.SelectSingleNode("//Title").InnerText = result.Title;
                customerNode.SelectSingleNode("//FirstNameOrInitial").InnerText = result.FirstNameOrInitial;
                customerNode.SelectSingleNode("//Surname").InnerText = result.Surname;
                customerNode.SelectSingleNode("//Gender").InnerText = result.Gender;
                if(result.DateOfBirth.HasValue)
                    customerNode.SelectSingleNode("//DateOfBirth").InnerText = result.DateOfBirth.Value.ToShortDateString();

                StringBuilder sbAddresss = new StringBuilder();
                if (!String.IsNullOrEmpty(result.HouseOrFlatNumberOrName))
                    sbAddresss.Append(result.HouseOrFlatNumberOrName + ", ");
                if (!String.IsNullOrEmpty(result.BuildingName))
                    sbAddresss.Append(result.BuildingName + ", ");
                if (!String.IsNullOrEmpty(result.Street))
                    sbAddresss.Append(result.Street + ", ");
                if (!String.IsNullOrEmpty(result.VillageOrDistrict))
                    sbAddresss.Append(result.VillageOrDistrict + ", ");
                if (!String.IsNullOrEmpty(result.TownCity))
                    sbAddresss.Append(result.TownCity + ", ");
                if (!String.IsNullOrEmpty(result.County))
                    sbAddresss.Append(result.County + ", ");
                if (!String.IsNullOrEmpty(result.PostCode))
                    sbAddresss.Append(result.PostCode);
                else
                    sbAddresss.Remove(sbAddresss.Length - 1, 1);    // Trim the last comma if there is no postcode (unlikely)

                customerNode.SelectSingleNode("//Address").InnerText = sbAddresss.ToString();

                // Get most recent pass print event
                CTPassPrintRecord[] printRecords = dataLayer.GetPassFromPrintQueueByID(result.RecordID, new DateTime(2011, 4, 1));
                if (printRecords.Length > 0)
                {
                    DateTime latestDate = new DateTime(2011,4,1);
                    foreach (CTPassPrintRecord record in printRecords)
                    {
                        if(record.ActualSentToPrintDate.HasValue)
                        {
                            if (record.ActualSentToPrintDate.Value > latestDate)
                                customerNode.SelectSingleNode("//DatePrinted").InnerText = record.ActualSentToPrintDate.Value.ToShortDateString();
                            if (record.PrintedDate.HasValue)
                                customerNode.SelectSingleNode("//DatePosted").InnerText = record.PrintedDate.Value.ToShortDateString();
                            else
                                customerNode.SelectSingleNode("//DatePosted").InnerText = "";
                        }

                    }

                }

                if(result.PhotographBytes.Length > 3)
                {
                customerNode.SelectSingleNode("PhotoURL").InnerText = "data:image/jpeg;base64,"+ Convert.ToBase64String(result.PhotographBytes);
                }
                response.DocumentElement.AppendChild(customerNode);
            }

            // Firmstep 19-03-2014. Added an Empty Customer node to allow for no matches
                XmlNode blankCustomerNode = customerTemplate.CloneNode(true);
                blankCustomerNode.SelectSingleNode("//RecordId").InnerText = "No Match Found";
                response.DocumentElement.AppendChild(blankCustomerNode);
            


            return response;
            if (log.IsDebugEnabled) log.Debug("Exiting");
        }*/

        private void buildCustomerSearchResponse(ref XmlNode customerNode, CTPassHolder passHolder)
        {
                // match up the values we can automatically.
            foreach (var prop in passHolder.GetType().GetProperties())
            {
                try
                {



                    //if (log.IsDebugEnabled) log.Debug("Property Name:" + prop.Name);
                    //if (log.IsDebugEnabled) log.Debug("Property Type:" + prop.PropertyType);
                    //XmlElement element; 
                    if (prop.PropertyType == typeof (CTPass))
                    {

                        foreach (var subprop in passHolder.CtPass.GetType().GetProperties())
                        {
                            //if (log.IsDebugEnabled) log.Debug("Sub Property Name:" + prop.Name);
                            //if (log.IsDebugEnabled) log.Debug("Sub Property Type:" + subprop.PropertyType);
                            //if (log.IsDebugEnabled) log.Debug("Sub Property Value:" + subprop.GetValue(passHolder.CtPass, null).ToString());
                            if (customerNode.SelectSingleNode("//" + subprop.Name) != null)
                            {
                                if (subprop.GetValue(passHolder.CtPass, null) != null)
                                {
                                    if (subprop.PropertyType == typeof (DateTime))
                                    {
                                        string dateString =
                                            ((DateTime) subprop.GetValue(passHolder.CtPass, null)).ToShortDateString();
                                        if (!isSpuriousDate(dateString))
                                            customerNode.SelectSingleNode("//" + subprop.Name).InnerText = dateString;
                                    }
                                    else
                                    {
                                        customerNode.SelectSingleNode("//" + subprop.Name).InnerText =
                                            subprop.GetValue(passHolder.CtPass, null).ToString();
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        //if (log.IsDebugEnabled) log.Debug("Property Value:" + prop.GetValue(passHolder, null).ToString());
                        if (customerNode.SelectSingleNode("//" + prop.Name) != null)
                        {
                            if (prop.GetValue(passHolder, null) != null)
                            {
                                if (prop.PropertyType == typeof (byte[]))
                                {
                                    customerNode.SelectSingleNode("//" + prop.Name).InnerText =
                                        convertImage((byte[]) prop.GetValue(passHolder, null));
                                }
                                else if (prop.PropertyType == typeof (DateTime))
                                {
                                    string dateString = ((DateTime) prop.GetValue(passHolder, null)).ToShortDateString();
                                    if (!isSpuriousDate(dateString))
                                        customerNode.SelectSingleNode("//" + prop.Name).InnerText =
                                            ((DateTime) prop.GetValue(passHolder, null)).ToShortDateString();
                                }
                                else if (prop.PropertyType == typeof (System.Nullable<DateTime>))
                                {
                                    //if (log.IsDebugEnabled) log.Debug("Property is Nullable DateTime");
                                    if (prop.GetValue(passHolder, null) != null)
                                    {
                                        string dateString =
                                            ((DateTime) prop.GetValue(passHolder, null)).ToShortDateString();
                                        if (!isSpuriousDate(dateString))
                                        {
                                            customerNode.SelectSingleNode("//" + prop.Name).InnerText = dateString;
                                        }
                                    }
                                }
                                else if (prop.PropertyType == typeof (char))
                                {
                                    if (Convert.ToChar(prop.GetValue(passHolder, null)) != '\0')
                                        customerNode.SelectSingleNode("//" + prop.Name).InnerText =
                                            Convert.ToString(prop.GetValue(passHolder, null));
                                }
                                else
                                {
                                    customerNode.SelectSingleNode("//" + prop.Name).InnerText =
                                        prop.GetValue(passHolder, null).ToString();
                                }
                            }
                        }
                    }
                }

                catch (Exception e)
                {
                    if (log.IsErrorEnabled) log.Error(e.Message);

                }

                // modify the nodes we need for the customer.
                if (customerNode.SelectSingleNode("//Deleted").InnerText.ToLower() == "true")
                    customerNode.SelectSingleNode("//Deleted").InnerText = "Y";
                else
                    customerNode.SelectSingleNode("//Deleted").InnerText = "N";

                if (customerNode.SelectSingleNode("//Photograph").InnerText.Length > 10)
                    customerNode.SelectSingleNode("//PhotoAssociated").InnerText = "Y";

                customerNode.SelectSingleNode("//DaysToExpiry").InnerText.PadLeft(4, '0');
                //customer.SelectSingleNode("//DaysSincePhotoUpdated").InnerText.PadLeft(5, '0');
                customerNode.SelectSingleNode("//RemainingTime").InnerText =
                    calculateTimeLeftString(passHolder.CtPass.DaysToExpiry);


                switch (passHolder.CtPass.PassType)
                {
                    case CTPassType.Age:
                        customerNode.SelectSingleNode("//TypeOfConcession").InnerText = "A";
                        customerNode.SelectSingleNode("//TypeOfConcessionLong").InnerText = "Age";
                        break;
                    case CTPassType.Disabled:
                        customerNode.SelectSingleNode("//TypeOfConcession").InnerText = "D";
                        customerNode.SelectSingleNode("//TypeOfConcessionLong").InnerText = "Eligible Disabled";
                        customerNode.SelectSingleNode("//DisabilityPermanent").InnerText = "Yes";
                        customerNode.SelectSingleNode("//DisabilityType").InnerText = "Permanent";
                        break;
                    case CTPassType.DisabledTemporary:
                        customerNode.SelectSingleNode("//TypeOfConcession").InnerText = "D";
                        customerNode.SelectSingleNode("//TypeOfConcessionLong").InnerText = "Eligible Disabled";
                        customerNode.SelectSingleNode("//DisabilityPermanent").InnerText = "No";
                        customerNode.SelectSingleNode("//DisabilityType").InnerText = "Temporary";
                        break;
                }

                
            }

            if (passHolder.PhotographBytes != null)
            {

                if (log.IsDebugEnabled) log.Debug("Photo associated. Attaching.");
                if (log.IsDebugEnabled) log.Debug("Photo Length:" + passHolder.PhotographBytes.Length);
                customerNode.SelectSingleNode("//Photograph").InnerText =
                    Convert.ToBase64String(passHolder.PhotographBytes);
            }
            else
            {
                if (log.IsDebugEnabled) log.Debug("No Photograph associated with this record.");
            }



            customerNode.SelectSingleNode("//FormattedExpiryDate").InnerText =
                passHolder.CtPass.ExpiryDate.ToString("s");

            if (passHolder.DateOfBirth.HasValue && !isSpuriousDate(passHolder.DateOfBirth.Value.ToShortDateString()))
            {
                customerNode.SelectSingleNode("//FormattedDateOfBirth").InnerText =
                    passHolder.DateOfBirth.Value.ToString("s");
            }


        }

        //TODO - Need to build data layer for this method...
        internal XmlDocument cancelPass(string ISRN, string delay)
        {
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTCancelPassResponse.xml");

            // validate that the ISRN is a valid ISRN
            if (ISRN.Replace(" ", "").Length < 18)
            {
                processError(ref response, 2);
                return response;
            }

            DateTime cancelPassDate = DateTime.Now.AddDays(Convert.ToInt32(delay));
            /*
            CTDataV2_WS.CT_DataLayer dataLayer = new CTDataV2_WS.CT_DataLayer();
            CTData jobData = new CTData(dataLayer.RetrieveData("", "", ISRN.ToString()));

            if (log.IsDebugEnabled) log.Debug("Starting Asynchronous Operation to cancel pass");

            CTBPMWorkerDelegate worker = new CTBPMWorkerDelegate(CTBPMWorker);
            worker.BeginInvoke(jobData, CTJob.CTJobType.CancelPass, cancelPassDate, null, null);
            */
            if (log.IsDebugEnabled) log.Debug("Asynchronous Process Started, returning from initial call.");

            response.SelectSingleNode("cancelPassResponse/status").InnerText = "success";

            return response;
        }
         

        internal XmlDocument updatePassDetails(string ISRN, string CPICC, string passHolderNumber, string firstNameOrInitial, string surname, string houseOrFlatNumberOrName,
            string buildingName, string street, string villageOrDistrict, string townCity, string county, string postcode, string title,
            string dateOfBirth, string typeOfConcession, string disabilityPermanent, string evidenceExpiryDate, string passStartDate, bool reissuePass, string oldCPICC, bool recalculateExpiryDate, string achieveServiceCaseNumber,
            string printReason, string gender, string disabilityCategory, string UPRN)
        {
            if (log.IsDebugEnabled) log.Debug("Update Pass Request Received");

            logParams(ISRN, CPICC, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNumberOrName,
             buildingName, street, villageOrDistrict, townCity, county, postcode, title,
             dateOfBirth, typeOfConcession, disabilityPermanent, evidenceExpiryDate, passStartDate, reissuePass.ToString(), oldCPICC, recalculateExpiryDate.ToString(), achieveServiceCaseNumber, printReason, gender, disabilityCategory);

            if (log.IsDebugEnabled) log.Debug("Loading Response XML Document");
            XmlDocument response = new XmlDocument();
            response.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTUpdatePassResponse.xml");
            if (log.IsDebugEnabled) log.Debug("Response XML loaded.");

            if (ISRN != String.Empty)
            {
                if (!validateISRN(ISRN))
                {
                    processError(ref response, 0);
                    return response;
                }
            }

            if (log.IsDebugEnabled) log.Debug("Getting Existing Passholder data from SmartCitizen.");
            SmartCitizenConnector dataLayer = new SmartCitizenConnector();
            SmartCitizenCTPassholder existingPassHolder = dataLayer.GetCtPassHolder(passHolderNumber);

            //CTData jobData = null;
            try
            {
                if (log.IsDebugEnabled) log.Debug("Building Job Data.");
                //CTData existingData = new CTData(queryPassFromPassholderNumber(oldCPICC, passHolderNumber));
                if (reissuePass)
                {
                    if (log.IsDebugEnabled) log.Debug("Pass will be reissued.");
                    if (String.IsNullOrEmpty(passStartDate)) passStartDate = DateTime.Now.ToShortDateString();
                    buildJobData(ref existingPassHolder, CPICC, null, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNumberOrName,
                        buildingName, street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession,
                        disabilityPermanent, evidenceExpiryDate, null, UPRN);
                    

                    if (!String.IsNullOrEmpty(gender))
                        existingPassHolder.Gender = gender;

                    if (existingPassHolder.CtPass.PassType == CTPassType.Disabled || existingPassHolder.CtPass.PassType == CTPassType.DisabledTemporary
                        && !String.IsNullOrEmpty(disabilityCategory))
                        existingPassHolder.DisabilityCategory = disabilityCategory[0];

                    /*
                    if (printReason.ToUpper().Contains("RENEW"))
                    {
                        //If the Print type is 'Renewal' and the current expiry date <= 2months print immediately.
                        DateTime twoMonthsTime = DateTime.Now.AddMonths(2);
                        if (existingData.ExpiryDate.CompareTo(twoMonthsTime) <= 0)
                            jobData.PassPrintDate = DateTime.Now;
                        else
                            jobData.PassPrintDate = existingData.ExpiryDate.AddMonths(-2);
                        //If the Print type is 'Renewal' and the current expiry date > 2months delay printing until 2 months before.

                    }
                    else
                    {
                        jobData.PassPrintDate = DateTime.Now;
                    }*/

                    try
                    {
                        existingPassHolder.CtPass.NorthgateCaseNumber = achieveServiceCaseNumber;
                    }
                    catch (System.FormatException)
                    {
                        if (log.IsDebugEnabled) log.Debug("Could not set AchieveService Case Number to:[" + achieveServiceCaseNumber + "]. Setting to -1");
                        existingPassHolder.CtPass.NorthgateCaseNumber = "-1";
                    }
                    // if we don't want to refresh the expiry date, replace the newly calculated one with the existing one.
                    /*if (!recalculateExpiryDate || existingData.CompanionAllowedLocally == 'Y') // Companion passes should not recalculate expiry date... yet.
                    {
                        jobData.ExpiryDate = existingData.ExpiryDate;
                    }*/
                }
                else
                {
                    if (log.IsDebugEnabled) log.Debug("Pass will not be reissued.");
                    //CTData existingData = new CTData(queryPassFromPassholderNumber(oldCPICC, passHolderNumber));
                    buildJobData(ref existingPassHolder, CPICC, null, passHolderNumber, firstNameOrInitial, surname, houseOrFlatNumberOrName,
                        buildingName, street, villageOrDistrict, townCity, county, postcode, title, dateOfBirth, typeOfConcession,
                        disabilityPermanent, evidenceExpiryDate, null, UPRN);

                    if (!String.IsNullOrEmpty(gender))
                        existingPassHolder.Gender = gender;

                    if (existingPassHolder.CtPass.PassType == CTPassType.Disabled || existingPassHolder.CtPass.PassType == CTPassType.DisabledTemporary
                        && !String.IsNullOrEmpty(disabilityCategory))
                        existingPassHolder.DisabilityCategory = disabilityCategory[0];

                    /*if (!recalculateExpiryDate)
                    {
                        jobData.ExpiryDate = existingData.ExpiryDate;
                    }*/
                    try
                    {
                        existingPassHolder.CtPass.NorthgateCaseNumber = achieveServiceCaseNumber;
                    }
                    catch (System.FormatException)
                    {
                        if (log.IsDebugEnabled) log.Debug("Could not set Northgate Case Number to:[" + achieveServiceCaseNumber + "]. Setting to -1");
                        existingPassHolder.CtPass.NorthgateCaseNumber = "-1";
                    }

                    /*if (existingPassHolder.CtPass.PassType == CTPassType.Disabled)
                    {
                        //jobData.DisabilityType = existingData.DisabilityType;
                        existingPassHolder.CtPass.DisabilityType = disabilityPermanent;
                    }*/
                }
                
            }

            catch (CTDataException ex)
            {


                // if we are reissuing and not recalculating the expiry date, and the error code relates to the expirydate, don't throw the error.
/*
                if (reissuePass && !recalculateExpiryDate && ex.ErrorCode == 5)
                {
                    // Do nothing
                }
                else
                {
                    processError(ref response, ex.ErrorCode);
                    return response;
                }*/
            }
            //CTBPMWorkerDelegate worker = new CTBPMWorkerDelegate(CTBPMWorker);
            /*if (log.IsDebugEnabled) log.Debug("Delegating job to worker. Reissue:" + reissuePass);

            // JobData is null if error 5 is caught. need to do the check inside the buildJobData to not throw the error...
            if (log.IsDebugEnabled)
            {
                log.Debug("String Values of JobData:");
                PropertyInfo[] properties = jobData.GetType().GetProperties();
                for (int i = 0; i < properties.Length; i++)
                {
                    if (properties[i].PropertyType == typeof(System.String))
                    {
                        log.Debug("Property:" + properties[i].Name + " value: [" + properties[i].GetValue(jobData, null) + "]");
                    }
                }
            }

            try
            {
                if (reissuePass)
                    worker.BeginInvoke(jobData, CTJob.CTJobType.UpdateDetailsAndIssuePass, null, null, null);
                else
                    worker.BeginInvoke(jobData, CTJob.CTJobType.UpdateDetails, null, null, null);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error invoking the asynchronous process. " + ex.Message);
                processError(ref response, 16);
            }*/

            SmartCitizenCTPassholder updatedPassHolder = dataLayer.UpdatePassHolderDetails(existingPassHolder);
            if (reissuePass)
                dataLayer.ReplacePass(updatedPassHolder.RecordID, updatedPassHolder.CtPass.ISRN, 17,
                    achieveServiceCaseNumber);

            if (log.IsDebugEnabled) log.Debug("Asynchronous process started, returning successful response.");
            response.SelectSingleNode("updatePassResponse/status").InnerText = "success";

            response.SelectSingleNode("updatePassResponse/passExpiryDate").InnerText = updatedPassHolder.CtPass.ExpiryDate.ToShortDateString();
            response.SelectSingleNode("updatePassResponse/reissue").InnerText = reissuePass.ToString();
            if (log.IsDebugEnabled) log.Debug("Update Pass Request Complete");
            return response;
        }

        private bool validateISRN(string ISRN)
        {
            try
            {
                Convert.ToInt64(ISRN);
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not convert ISRN to a number:[" + ISRN + "]");
                if (log.IsDebugEnabled) log.Debug("Inner Exception:" + ex.Message);
                return false;
            }

            if (ISRN.Length != 18)
                return false;



            return true;
        }

        /// <summary>
        /// Checks the supplied CPICC code against the list of valid Warwickshire CPICCs
        /// </summary>
        /// <param name="CPICC">5 digit CPICC code</param>
        /// <returns>true if CPICC is  </returns>
        /// <exception>CTDataException</exception>
        private bool validateCPICC(string CPICC)
        {
            if (log.IsDebugEnabled) log.Debug("Validating CPICC:" + CPICC);
            if (String.IsNullOrEmpty(CPICC))
                throw new CTDataException(13);
            if (warwickshireCPICCs.ContainsKey(CPICC))
                return true;
            else
                throw new CTDataException(18);
        }



        private void processError(ref XmlDocument response, int errorCode)
        {
            if (log.IsErrorEnabled) log.Error("Error received.");
            if (log.IsErrorEnabled) log.Error("Error code:" + errorCode);
            response.SelectSingleNode("//status").InnerText = "error";
            XmlDocument errorDoc = new XmlDocument();
            errorDoc.Load(HttpContext.Current.ApplicationInstance.Server.MapPath("~/App_Data") + "/CTError.xml");

            errorDoc.SelectSingleNode("error/code").InnerText = errorCode.ToString();
            errorDoc.SelectSingleNode("error/description").InnerText = ctErrors[errorCode];

            XmlNode errorNode = response.ImportNode(errorDoc.DocumentElement, true);

            response.DocumentElement.AppendChild(errorNode);

        }

        /// <summary>
        /// Logs all the supplied parameter names and values out to the debug log
        /// </summary>
        /// <param name="parms">List of parameter names to log. Values are automatically passed.</param>
        private void logParams(params object[] parms)
        {
            System.Diagnostics.StackTrace stackTrace = new System.Diagnostics.StackTrace();
            if (log.IsDebugEnabled) log.Debug("Output of parameters supplied for:" + stackTrace.GetFrame(1).GetMethod().Name);
            for (int i = 0; i < parms.Length; i++)
            {
                if (log.IsDebugEnabled) log.Debug("Parameter [" + i + "]: Name:" + stackTrace.GetFrame(1).GetMethod().GetParameters()[i].Name + " Value:[" + parms[i] + "]");
            }
        }

        /*internal bool getPassesForPrint()
        {
            try
            {
                CTBPMEngine.getInstance().buildZipFile();
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error getting passes for print:" + ex.Message);
                return false;
            }
            return true;
        }

        internal bool getPrintedPasses()
        {
            try
            {
                CTBPMEngine.getInstance().downloadSFTP();
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Error downloading passes: " + ex.Message);
                return false;
            }
            return true;
        }

        internal bool processESPReports()
        {
            try
            {
                CTBPMEngine.getInstance().processESPReports();
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not process reports:" + ex.Message);
                return false;
            }
            return true;
        }*/

        private byte[] convertImage(string imageAsBase64String)
        {
            if (log.IsDebugEnabled) log.Debug("Attempting to convert the image string.");
            try
            {
                byte[] imageAsBytes = Convert.FromBase64String(imageAsBase64String);
                if (log.IsDebugEnabled) log.Debug("Image String converted to Byte array.");
                return imageAsBytes;
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not convert image string:" + ex.Message);
                throw new CTDataException(11);
            }
        }

        private string convertImage(byte[] imageAsBytes)
        {
            string imageAsBase64String = String.Empty;
            try
            {
                imageAsBase64String = Convert.ToBase64String(imageAsBytes);
            }
            catch (ArgumentNullException)
            {
                if (log.IsErrorEnabled) log.Error("No value supplied to convert to Base64 string.");
            }
            return imageAsBase64String;
        }

        /*private void startAsynchronousPassRequest(CTData jobData, CTJob.CTJobType jobType)
        {
            if (log.IsDebugEnabled) log.Debug("Starting Asynchronous Operation");
            try
            {
                CTBPMWorkerDelegate worker = new CTBPMWorkerDelegate(CTBPMWorker);
                worker.BeginInvoke(jobData, jobType, null, null, null);
                if (log.IsDebugEnabled) log.Debug("Asynchronous Process Started, returning from initial call.");
            }
            catch (Exception ex)
            {
                if (log.IsErrorEnabled) log.Error("Could not invoke asynchronous job request: " + ex.Message);
                throw new CTDataException(16);
            }
        }*/

        internal string calculateTimeLeftString(int days)
        {
            //int time = int.Parse(_timeLeft);
            StringBuilder sb = new StringBuilder();
            int years = 0;
            int months = 0;


            if (days < 0)
            {
                return "Expired";
            }

            //Find out how many years
            if (days >= 365)
            {
                //Need to divide time by 365 to find the number of years
                years = days / 365;
                int yearsToRemove = years * 365;
                days = days - yearsToRemove;
                if (years > 1)
                {
                    sb.Append(years + " years");
                }
                else
                {
                    sb.Append(years + " year");
                }

            }
            if (days >= 30)
            {
                if (years >= 1)
                {
                    sb.Append(", ");
                }

                //Need to divide time by 30 to get the number of months
                months = days / 30;
                int monthsToRemove = months * 30;
                days = days - monthsToRemove;
                if (months > 1)
                {
                    sb.Append(months + " months");
                }
                else
                {
                    sb.Append(months + " month");
                }
            }

            if (days > 0)
            {
                if (years > 0 || months > 0)
                {
                    sb.Append(" and ");
                }
                if (days > 1)
                {
                    sb.Append(days + " days");
                }
                else
                {
                    sb.Append(days + " day");
                }
            }
            return sb.ToString();
        }




        #region Obsolete Methods
        [Obsolete("Please use the ValidateISRN method", true)]
        private bool validatePassNumber(string passNumberToValidate)
        {
            return false;
        }
        #endregion


    }

    #region Exception classes
    internal class DateException : Exception
    {
        internal DateException(string message)
            : base(message)
        {
        }
    };
    #endregion

}
