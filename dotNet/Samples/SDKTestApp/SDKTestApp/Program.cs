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

            //string APIKEY = "nfVKkOigXUs+2cP/I9MIhXPqxva85zLAlGoXKVcozZI=";
            //string APPID = "assettagz-pc";
            //string APPVER = "1.0.0";
            //string APPFRIENDLY = "My card reader";
            string APIKEY = "bw9tbZbJz4oT+lOOVus2kOEAHhOsvLn5v2pEw3e3X8s=";
            string APPID = "assettagz-android";
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

                    //Parse the card data - CSCS fromat is assumed here. Other card schemes may required methods of parsing 
                    Dictionary<string, string> cardDataVals = CSCSMetadata.ParseCardData(scReadResult.Result.CardData);

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
    }
}
