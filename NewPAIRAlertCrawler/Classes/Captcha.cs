using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using CefSharp.WinForms;
using CefSharp;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.Web.Script.Serialization;


namespace NewPAIRAlertCrawler.Classes
{
    public class Captcha
    {
        private HtmlAgilityPack.HtmlDocument htmlAgilityDocument;
        public ChromiumWebBrowser objPdfDownloadBrowser;

        private CaptchaResponse start2captchaSolver(string googlekey, string captchakey)
        {
            try
            {
                string rt;
                int counter = 0;
                CaptchaResponse finresponse = null;
                WebRequest request = WebRequest.Create("http://2captcha.com/in.php?key=" + captchakey + "&method=userrecaptcha&googlekey=" + googlekey + "&pageurl=https://my.uspto.gov/&here=now&json=1");
                WebResponse response = request.GetResponse();
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                rt = reader.ReadToEnd();
                CaptchaResponse cresponse = new JavaScriptSerializer().Deserialize<CaptchaResponse>(rt);

                if (cresponse != null && cresponse.status == 1)
                {
                    while (true)
                    {
                        if (counter == 15)
                        {
                            CaptchaResponse error = new CaptchaResponse();
                            error.status = -1;
                            error.request = "Error";
                            return error;
                        }
                        WebRequest reponse = WebRequest.Create("http://2captcha.com/res.php?key=" + captchakey + "&action=get&id=" + cresponse.request + "&json=1");
                        WebResponse result = reponse.GetResponse();
                        Stream dataStream1 = result.GetResponseStream();
                        StreamReader reader1 = new StreamReader(dataStream1);
                        rt = reader1.ReadToEnd();
                        finresponse = new JavaScriptSerializer().Deserialize<CaptchaResponse>(rt);

                        if (finresponse != null && finresponse.status == 1)
                        {
                            return finresponse;
                        }
                        else
                        {
                            System.Threading.Thread.Sleep(3000);
                            counter++;
                        }
                    }
                }

                return finresponse;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private string GetGoogleRecaptchaKey(string documentSource)
        {
            htmlAgilityDocument.LoadHtml(documentSource);
            HtmlAgilityPack.HtmlNodeCollection alertNodes = htmlAgilityDocument.DocumentNode.SelectNodes("//div[contains(@id, 'siw-recaptcha')]//iframe");
            if (alertNodes != null && alertNodes.Count > 0 && alertNodes[0].Attributes["src"] != null)
            {
                Uri iframeURL = new Uri(alertNodes[0].Attributes["src"].Value);
                if (iframeURL != null)
                {
                    string newGoogleKey = getBetween(iframeURL.ToString(), "k=", "&");
                    return newGoogleKey;
                }
            }
            return string.Empty;
        }

        public string getBetween(string strSource, string strStart, string strEnd)
        {
            int Start, End;
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                Start = strSource.IndexOf(strStart, 0) + strStart.Length;
                End = strSource.IndexOf(strEnd, Start);
                return strSource.Substring(Start, End - Start);
            }
            else
            {
                return "";
            }
        }

        private async Task AsyncDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }
    }

    public class CaptchaResponse
    {
        public int status { get; set; }
        public string request { get; set; }

    }
}
