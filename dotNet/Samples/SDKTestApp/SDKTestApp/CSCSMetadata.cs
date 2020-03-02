using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using RPL.SmartcardSDK.Public.Models;

namespace SDKTestApp
{
    class CSCSMetadata
    {
        internal static Dictionary<string, string> ParseCardData(CardData cardData)
        {
            //
            //This only extracts a subset of the available card data, but includes the elements needed for rendering
            //
            Dictionary<string, string> renderData = new Dictionary<string, string>();

            if (cardData == null)
            {
                return renderData;
            }

            renderData.Add("photo", cardData.Scheme.DataBlocks.First(d => d.Type.Equals("image/jpeg")).Data);

            byte[] cardDataBytes = Convert.FromBase64String(cardData.Scheme.DataBlocks.First(d => d.Type.Equals("text/sml")).Data);

            string cardXML = System.Text.Encoding.ASCII.GetString(cardDataBytes);

            string title = GetXMLTagValue(cardXML, "Title");
            string initial = GetXMLTagValue(cardXML, "Initial");
            string surname = GetXMLTagValue(cardXML, "Surname");
            string firstname = GetXMLTagValue(cardXML, "Firstname");
            string logoID = GetXMLTagValue(cardXML, "LogoID");
            string expiryDateStr = GetXMLTagValue(cardXML, "ExpiryDate");
            string cancelledDateStr = GetXMLTagValue(cardXML, "CancelledDate");
            string suspendedDateStr = GetXMLTagValue(cardXML, "SuspendedDate");
            string printDateStr = GetXMLTagValue(cardXML, "PrintDate");


            // try two possible date formats for expiry date
            DateTime expiryDate = DateTime.MinValue;
            bool expiryDateParsed = DateTime.TryParseExact(expiryDateStr, "MMM yyyy", null, DateTimeStyles.None, out expiryDate);
            DateTime? displayExpiryDate = null;

            if (!expiryDateParsed)
            {
                expiryDateParsed = DateTime.TryParseExact(expiryDateStr, "MMMM yyyy", null, DateTimeStyles.None, out expiryDate);
            }

            if (expiryDateParsed)
            {
                displayExpiryDate = new DateTime(expiryDate.Year, expiryDate.Month, 1).AddMonths(1).AddDays(-1);
            }

            renderData.Add("printdate", printDateStr);
            renderData.Add("top", GetXMLTagValue(cardXML, "Top"));
            renderData.Add("fullname", ((!string.IsNullOrEmpty(title) ? title.ToUpper() + " " : "") + (!string.IsNullOrEmpty(initial) ? initial.ToUpper() + " " : "") + (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("initialsurname", ((!string.IsNullOrEmpty(initial) ? initial.ToUpper() + " " : "") + (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("firstnamesurname", ((!string.IsNullOrEmpty(firstname) ? firstname.ToUpper() + " " : "") + (!string.IsNullOrEmpty(surname) ? surname.ToUpper() + " " : "")).Trim());
            renderData.Add("regno", GetXMLTagValue(cardXML, "RegNo"));
            renderData.Add("colour", GetXMLTagValue(cardXML, "Colour"));
            renderData.Add("bottom", GetXMLTagValue(cardXML, "Bottom"));
            renderData.Add("expirydatemonthyear", displayExpiryDate?.ToString("MMM yyyy") ?? expiryDateStr);
            renderData.Add("expirydatemonthyearfull", displayExpiryDate?.ToString("MMMM yyyy") ?? expiryDateStr);
            renderData.Add("scheme", GetXMLTagValue(cardXML, "Scheme"));
            renderData.Add("type", GetXMLTagValue(cardXML, "Type"));
            renderData.Add("issuedby", GetXMLTagValue(cardXML, "IssuedBy"));
            renderData.Add("foil", GetXMLTagValue(cardXML, "Foil"));
            renderData.Add("logo", String.IsNullOrEmpty(logoID) ? "" : logoID);

            //
            //Get competences list
            //
            List<Competence> compList = new List<Competence>();

            var backElementContents = GetXMLTagValue(cardXML, "Back", false);
            if (backElementContents != null)
            {
                var competences = backElementContents.Split(new[] { "</>" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var competence in competences)
                {
                    // Lop off the <Line> tag
                    string strComp = competence.Trim().Substring(6);
                    List<string> listComp = strComp.Split(new[] { "|" }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    string compText = listComp.Count > 0 ? HttpUtility.HtmlDecode(listComp[0]) : "";
                    DateTime compDate;
                    if (listComp.Count > 1)
                    {
                        if (DateTime.TryParseExact(listComp[1], new string[] { "dd-MM-yyyy" }, null, DateTimeStyles.None, out compDate))
                        {
                            compList.Add(new Competence { Text = compText, ExpiryDate = compDate });
                        }
                        else
                        {
                            compList.Add(new Competence { Text = compText, ExpiryDate = null });
                        }
                    }
                    else
                    {
                        compList.Add(new Competence { Text = compText, ExpiryDate = null });
                    }
                }
            }


            if (compList != null && compList.Count > 0)
            {
                StringBuilder sbQuals = new StringBuilder();
                StringBuilder sbExpDates = new StringBuilder();

                sbQuals.Append("[");
                sbExpDates.Append("[");

                foreach (var qual in compList)
                {
                    // Update to Affiliate spec for B&ES introduces expiry dates per competence
                    sbQuals.Append("{" + qual.Text + "}");
                    sbExpDates.Append("{" + (qual.ExpiryDate.HasValue ? qual.ExpiryDate.Value.ToString("dd-MM-yyyy") : "") + "}");
                }

                sbQuals.Append("]");
                sbExpDates.Append("]");

                renderData.Add("qualificationslist", sbQuals.ToString());
                renderData.Add("qualificationsexpirylist", sbExpDates.ToString());
            }


            // add the Gencarda custom fields
            var customDataFields = new Dictionary<string, string>();

            var customTag = GetXMLTagValue(cardXML, "Custom", false);
            var customFields = customTag.Split(new[] { "</>" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var customField in customFields)
            {
                var customFieldData = customField.Replace("<Field>", "");

                var customFieldArr = customFieldData.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

                var customFieldName = RemoveNonAlphanumeric(customFieldArr[0]).ToLower();

                customDataFields.Add("customlabel_" + customFieldName, customFieldArr[0]);
                customDataFields.Add("customvalue_" + customFieldName, customFieldArr[1]);
            }

            if (!string.IsNullOrEmpty(cancelledDateStr))
            {
                renderData.Add("cancelled", "1");
                renderData.Add("suspended", "0");
                renderData.Add("expired", "0");
            }
            else if (!string.IsNullOrEmpty(suspendedDateStr))
            {
                renderData.Add("cancelled", "0");
                renderData.Add("suspended", "1");
                renderData.Add("expired", "0");
            }
            else
            {
                renderData.Add("cancelled", "0");
                renderData.Add("suspended", "0");

                if (expiryDate <= DateTime.Now)
                {
                    renderData.Add("expired", "1");
                }
                else
                {
                    renderData.Add("expired", "0");
                }
            }

            renderData.Add("inexpirygraceperiod", "0");

            // Environment markers list (B&ES)
            renderData.Add("env", GetXMLTagValue(cardXML, "Env"));

            //add gencarda custom fields
            if (customDataFields != null)
            {
                foreach (var customField in customDataFields)
                {
                    renderData.Add(customField.Key, customField.Value);
                }
            }

            return renderData;
        }

        private static string RemoveNonAlphanumeric(string text)
        {
            Regex rgx = new Regex("[^a-zA-Z0-9]");
            return rgx.Replace(text, "");
        }

        /// <summary>
        /// Gets the value of the specified tag from the specified fake-xml string, containing illegal formatting, such as generic tag closers: "</>"
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="tag"></param>
        /// <param name="genericCloseTag"></param>
        /// <returns></returns>
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
                    xml.IndexOf(closingTag, xml.IndexOf(openingTag) + openingTag.Length) - (xml.IndexOf(openingTag) + openingTag.Length
                ));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    internal class Competence
    {
        public string Text { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
