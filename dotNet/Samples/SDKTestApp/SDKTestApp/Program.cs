using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PCSC;
using RPL.SmartcardSDK.Public;
using RPL.SmartcardSDK.Public.Models;

namespace SDKTestApp
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            // --- TODO SET THESE VALUES TO SUIT YOUR OWN ENVIRONMENT AND SDK CONFIGURATION

            string TEMP_ROOT = "c:\\temp\\SDK";
            string TEMP_FOLDER = $"{TEMP_ROOT}\\TestData";

            string APIKEY = "YOUR APIKEY GOES HERE";
            string APPID = "YOUR APPID GOES HERE";
            string APPVER = "1.0.0";
            string APPFRIENDLY = "My card reader";
            
            // --- TODO SET THESE VALUES TO SUIT YOUR OWN ENVIRONMENT AND SDK CONFIGURATION

            if (!Directory.Exists(TEMP_FOLDER))
            {
                Directory.CreateDirectory(TEMP_FOLDER);
            }

            //
            //Initialise the SDK
            //
            Context sdkContext = new Context(TEMP_ROOT);

            InitParams initParams = new InitParams
            {
                ApiKey = APIKEY,
                AppIdentifierName = APPID,
                AppVersion = APPVER,
                AppFriendlyName = APPFRIENDLY
            };

            SmartcardClientResult<InitResult> initResult = await SmartcardClient.Init(sdkContext, initParams, (x) => { Debug.WriteLine(x.Status); });

            if (!initResult.Success)
            {
                Debug.WriteLine($"*** Initialising API FAILED *** : {initResult.Error?.ErrorType}");
                return;
            }

            Debug.WriteLine("*** Initialising API COMPLETE ***");


            Debug.WriteLine("*** Reading card data ***");

            // Create PC/SC context
            var ctx = new SCardContext();
            ctx.Establish(SCardScope.System);

            // Create reader object and connect to the Smart Card
            SCardReader reader = new SCardReader(ctx);

            string[] readers = ctx.GetReaders();

            bool connected = false;

            foreach (string r in readers)
            {
                Debug.WriteLine($"Checking card reader {r}  ");

                var rc = reader.Connect(r, SCardShareMode.Shared, SCardProtocol.Any);
                SCardError cardError = (SCardError)rc;

                if (rc == SCardError.Success)
                {
                    //Just use the first reader which we can connect to
                    Debug.WriteLine($"CONNECTED using protocol {reader.ActiveProtocol.ToString()}");
                    connected = true;
                    break;
                }

                Debug.WriteLine($"FAILED TO CONNECT: {cardError.ToString()}");
            }

            if (connected)
            {
                SmartcardClientResult<ReadCardResult> scReadResult = await SmartcardClient.ReadCard(reader, SmartcardClient.SyncOptions.SYNC, (x) => { Debug.WriteLine(x.Status); });

                if (scReadResult.Success)
                {
                    Debug.WriteLine($"Read successful : SchemeId = {scReadResult.Result.CardData.Scheme.Identifier}");

                    Debug.WriteLine($"Parsing card data...");
                    Dictionary<string, string> cardDataVals = ParseCSCSCardData(scReadResult.Result.CardData);

                    foreach (KeyValuePair<string, string> keyVal in cardDataVals)
                    {
                        Debug.WriteLine($"{keyVal.Key} : {keyVal.Value}");
                    }

                    RenderCardParams renderParams = new RenderCardParams()
                    {
                        RenderCardDataMap = cardDataVals,
                        SchemeId = scReadResult.Result.CardData.Scheme.Identifier
                    };

                    string fileName = scReadResult.Result.CardData.Scheme.Identifier + "_" + cardDataVals["fullname"].Replace(" ", "_");

                    SmartcardClientResult<RenderCardResult> renderResult = SmartcardClient.RenderCard(renderParams, (x) => { Debug.WriteLine(x.Status); });

                    if (renderResult.Success)
                    {
                        Debug.WriteLine($"Render successful");

                        if (renderResult?.Result?.CardFront != null)
                        {
                            File.WriteAllBytes($"{TEMP_FOLDER}\\{fileName}_front.png", renderResult.Result.CardFront);
                        }

                        if (renderResult?.Result?.CardBack != null)
                        {
                            File.WriteAllBytes($"{TEMP_FOLDER}\\{fileName}_back.png", renderResult.Result.CardBack);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"Render failed: {renderResult.Error?.ErrorType.ToString()}");
                    }
                }
                else
                {
                    Debug.WriteLine($"Card read failed: {scReadResult.Error?.ErrorType.ToString()}");
                }
            }
            else
            {
                Debug.WriteLine($"*** No cards found ***");
            }
        }


        /// <summary>
        /// Build up the dictionary of data elements required for a card render
        /// </summary>
        /// <param name="cardData"></param>
        /// <returns></returns>
        private static Dictionary<string, string> ParseCSCSCardData(CardData cardData)
        {
            Dictionary<string, string> renderData = new Dictionary<string, string>();

            //
            //Extract card photo data block 
            //
            renderData.Add("photo", cardData.Scheme.DataBlocks.First(d => d.Type.Equals("image/jpeg")).Data);

            //
            //Extract CSCS card datablock. This has a pseudo-XML structure. 
            //
            byte[] cardDataBytes = Convert.FromBase64String(cardData.Scheme.DataBlocks.First(d => d.Type.Equals("text/sml")).Data);
            string cardXML = System.Text.Encoding.ASCII.GetString(cardDataBytes);

            //string regNo = GetXMLTagValue(cardXML, "RegNo");
            string title = GetXMLTagValue(cardXML, "Title");
            string initial = GetXMLTagValue(cardXML, "Initial");
            string surname = GetXMLTagValue(cardXML, "Surname");
            string firstname = GetXMLTagValue(cardXML, "Firstname");
            string logoID = GetXMLTagValue(cardXML, "LogoID");
            string expiryDateStr = GetXMLTagValue(cardXML, "ExpiryDate");

            // try two possible date formats for expiry date
            DateTime expiryDate = DateTime.MinValue;
            bool expiryDateParsed = DateTime.TryParseExact(expiryDateStr, "MMM yyyy", null, DateTimeStyles.None, out expiryDate);
            DateTime? displayExpiryDate = null;

            if (!expiryDateParsed)
            {
                expiryDateParsed = DateTime.TryParseExact(expiryDateStr, "MMMM yyyy", null, DateTimeStyles.None,
                    out expiryDate);
            }

            if (expiryDateParsed)
            {
                displayExpiryDate = new DateTime(expiryDate.Year, expiryDate.Month, 1).AddMonths(1).AddDays(-1);
            }
            
            string printDateStr = GetXMLTagValue(cardXML, "PrintDate");

            renderData.Add("printdate", printDateStr);
            renderData.Add("top", GetXMLTagValue(cardXML, "Top"));
            renderData.Add("fullname",
                ((!string.IsNullOrEmpty(title) ? title.ToUpper() + " " : "") +
                 (!string.IsNullOrEmpty(initial) ? initial.ToUpper() + " " : "") +
                 (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("initialsurname",
                ((!string.IsNullOrEmpty(initial) ? initial.ToUpper() + " " : "") +
                 (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("firstnamesurname",
                ((!string.IsNullOrEmpty(firstname) ? firstname.ToUpper() + " " : "") +
                 (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("regno", GetXMLTagValue(cardXML, "RegNo"));
            renderData.Add("colour", GetXMLTagValue(cardXML, "Colour"));
            renderData.Add("bottom", GetXMLTagValue(cardXML, "Bottom"));
            renderData.Add("expirydatemonthyear", displayExpiryDate?.ToString("MMM yyyy") ?? expiryDateStr);
            renderData.Add("expirydatemonthyearfull", displayExpiryDate?.ToString("MMMM yyyy") ?? expiryDateStr);

            renderData.Add("scheme", GetXMLTagValue(cardXML, "Scheme"));

            return renderData;
        }

        private static string GetXMLTagValue(string xml, string tag, bool genericCloseTag = true)
        {
            try
            {
                var openingTag = "<" + tag + ">";
                var closingTag = genericCloseTag ? "</>" : "</" + tag + ">";

                if (xml.IndexOf(openingTag) == -1)
                {
                    return "";
                }

                return xml.Substring(
                    xml.IndexOf(openingTag) + openingTag.Length,
                    xml.IndexOf(closingTag, xml.IndexOf(openingTag) + openingTag.Length) -
                    (xml.IndexOf(openingTag) + openingTag.Length
                    ));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
