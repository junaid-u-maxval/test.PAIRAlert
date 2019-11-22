using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using CefSharp.WinForms;
using CefSharp;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Management;
using System.Configuration;
using System.Web.Script.Serialization;

namespace NewPAIRAlertCrawler
{
    public partial class InitializeUSCrawler : Form, IDownloadHandler
    {

        HtmlAgilityPack.HtmlDocument htmlAgilityDocument;
        public event EventHandler<DownloadItem> OnBeforeDownloadFired;
        public event EventHandler<DownloadItem> OnDownloadUpdatedFired;
        ChromiumWebBrowser webBrowser;
        string documentSource;
        bool loginProcessInitiated;
        public InitializeUSCrawler()
        {
            InitializeComponent();
        }

        private void InitializeUSCrawler_Load(object sender, EventArgs e)
        {
            #region "InitializeCrawlerEvents"
            RemoveCefSharpSubProcess("CefSharp.BrowserSubprocess");

            string cefCachePath = System.IO.Directory.GetCurrentDirectory() + "//CefSharpCache";
            if (!Directory.Exists(cefCachePath))
            {
                Directory.CreateDirectory(cefCachePath);
                var di = new DirectoryInfo(cefCachePath);
                di.Attributes &= ~FileAttributes.ReadOnly;
            }

            if (Cef.IsInitialized)
            {
                webBrowser.Dispose();
            }

            CefSettings settings = new CefSettings();
            settings.MultiThreadedMessageLoop = true;
            settings.CachePath = cefCachePath;
            settings.WindowlessRenderingEnabled = true;
            //settings. = true;
           // CefSharpSettings = true;
            Cef.Initialize(settings);
            LoadPublicPairURL();
            #endregion "InitializeCrawlerEvents"
        }

        private async void LoadPublicPairURL()
        {
            string publicPairUrl = ConfigurationManager.AppSettings["PublicPairURL"].ToString();
            webBrowser = new ChromiumWebBrowser(publicPairUrl);
            this.Controls.Add(webBrowser);
            webBrowser.DownloadHandler = this;
            webBrowser.Load(publicPairUrl);
            await AsyncDelay(10000);
            webBrowser.LoadingStateChanged += CefSharp_DocumentComplete;
        }

        private async void CefSharp_DocumentComplete(object sender, LoadingStateChangedEventArgs e)
        {
            bool isSetCaptchaResponse = false; bool isSubmitButtonClicked = false;
            string UrlText = webBrowser.Address == null ? string.Empty : webBrowser.Address.ToString();
            documentSource = await webBrowser.GetBrowser().MainFrame.GetSourceAsync();
            htmlAgilityDocument = new HtmlAgilityPack.HtmlDocument();
            htmlAgilityDocument.LoadHtml(documentSource);
            if (await checkIDElementExists("recaptcha-demo"))
            {
                string googleKey = GetGoogleRecaptchaKey(documentSource);
                string captchaKey = "84e20a36e9857a0b7bc129ab69f1b043";
                if (!string.IsNullOrEmpty(googleKey))
                {
                    CaptchaResponse capResponse = start2captchaSolver(googleKey, captchaKey);
                    if (capResponse.status == 1 && !string.IsNullOrEmpty(capResponse.request))
                    {
                        await setIDElementInnerHTML("g-recaptcha-response", capResponse.request);
                        await AsyncDelay(3000);
                        await clickIDElement("recaptcha-demo-submit");
                        await AsyncDelay(3000);
                        documentSource = await webBrowser.GetBrowser().MainFrame.GetSourceAsync();
                        System.Threading.Thread.Sleep(3000);
                        htmlAgilityDocument.LoadHtml(documentSource);
                        if (await checkIDElementExists("application_number_radiobutton"))
                        {
                            await clickIDElement("application_number_radiobutton");
                            await AsyncDelay(1000);
                            if (await checkIDElementExists("number_id"))
                            {

                            }
                        }
                    }

                }
                //var loadingNodes = htmlAgilityDocument.DocumentNode.SelectNodes("//div[contains(@id, 'recaptcha-demo')]");
            }
            //var loadingNodes = htmlAgilityDocument.DocumentNode.SelectNodes("//div[contains(@id, 'recaptcha-demo')]");
            //HtmlAgilityPack.HtmlNodeCollection tagCollection = htmlAgilityDocument.DocumentNode.SelectNodes("//div");
            if (!e.IsLoading && !loginProcessInitiated)
            {
                try
                {
                }
                catch (Exception ex)
                {

                }
            }
        }

        public static void RemoveCefSharpSubProcess(string processname)
        {
            string cefPath = System.IO.Directory.GetCurrentDirectory() + "\\CefSharp.BrowserSubprocess.exe";

            Process[] process = Process.GetProcessesByName(processname);
            foreach (Process p in process)
            {
                string path = ProcessExecutablePath(p);

                if (path == cefPath)
                {
                    // LogManagerBL.Write(Enums.CrawlerLogProcessType.Login, "", "Removing CefSharp BrowserSubprocess." + cefPath);
                    p.Kill();
                }
            }

        }

        static private string ProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule.FileName;
            }
            catch
            {
                string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process";
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject item in searcher.Get())
                {
                    object id = item["ProcessID"];
                    object path = item["ExecutablePath"];

                    if (path != null && id.ToString() == process.Id.ToString())
                    {
                        return path.ToString();
                    }
                }
            }

            return "";
        }

        private async Task<string> GeDocumentSourceFromBrowser()
        {
            string documentSource = string.Empty;
            try
            {
                documentSource = await webBrowser.GetBrowser().MainFrame.GetSourceAsync();
                System.Threading.Thread.Sleep(5000);
            }
            catch
            {
                return string.Empty;
            }
            return documentSource;
        }

        private async Task AsyncDelay(int milliseconds)
        {
            await Task.Delay(milliseconds);
        }


        #region "Captcha Recognition - 2Captcha"
        private CaptchaResponse start2captchaSolver(string googlekey, string captchakey)
        {
            try
            {
                string rt;
                int counter = 0;
                CaptchaResponse finresponse = null;
                WebRequest request = WebRequest.Create("http://2captcha.com/in.php?key=" + captchakey + "&method=userrecaptcha&googlekey=" + googlekey + "&pageurl=https://portal.uspto.gov/pair/PublicPair/&here=now&json=1");
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
            //htmlAgilityDocument.LoadHtml(documentSource);
            HtmlAgilityPack.HtmlNodeCollection alertNodes = htmlAgilityDocument.DocumentNode.SelectNodes("//div[contains(@id, 'recaptcha-demo')]//iframe");
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

        #endregion "Captcha Recognition - 2Captcha"

        #region "Click button or Set Values to the inputs"

        private async Task<bool> checkIDElementExists(string id)
        {
            try
            {
                string script = "(function() { var element = document.getElementById('" + id + "'); if (typeof(element) != 'undefined' && element != null) { return true; } else { return false; } })()";
                bool elementExists = false;
                await webBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
                {
                    var response = x.Result;
                    if (response.Success && bool.Parse(response.Result.ToString()))
                    {
                        elementExists = true;
                    }
                });
                return elementExists;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> checkNameElementExists(string name, int index = 0)
        {
            string script = string.Format("document.getElementsByName('" + name + "').length;");
            bool elementExists = false;
            await webBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success && response.Result != null)
                {
                    elementExists = int.Parse(response.Result.ToString()) > index;
                }
            });
            return elementExists;
        }

        private async Task<bool> clickClassElement(string className, int index)
        {
            string script = "$('." + className + "')[" + index + "].click()";
            bool isSuccess = false;
            await webBrowser.GetBrowser().MainFrame.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    isSuccess = true;
                }
            });
            return isSuccess;
        }
        private async Task<bool> clickTagElement(string tagName, int index)
        {
            string script = "document.getElementsByTagName('" + tagName + "')[" + index + "].click()";
            bool isSuccess = false;
            await webBrowser.GetBrowser().MainFrame.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    isSuccess = true;
                }
            });
            return isSuccess;
        }

        private async Task<bool> clickIDElement(string id, string type = "button")
        {
            string script = "document.getElementById('" + id + "').click()";
            if (type == "checkbox")
            {
                script = "document.getElementById('" + id + "').checked=true;";
            }
            bool isSuccess = false;
            await webBrowser.GetBrowser().MainFrame.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    isSuccess = true;
                }
            });
            return isSuccess;
        }

        private async Task<bool> setIDElementValue(string id, string value)
        {
            string script = string.Format("document.getElementById('" + id + "').value='" + value + "';");
            bool elementExists = false;
            await webBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    elementExists = true;
                }
            });
            return elementExists;
        }

        private async Task<bool> setIDElementInnerHTML(string id, string value)
        {
            string script = string.Format("document.getElementById('" + id + "').innerHTML='" + value + "';");
            bool elementExists = false;
            await webBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    elementExists = true;
                }
            });
            return elementExists;
        }

        private async Task<bool> clickNameElement(string name, int index)
        {
            string script = "document.getElementsByName('" + name + "')[" + index + "].click()";
            bool isSuccess = false;
            await webBrowser.GetBrowser().MainFrame.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success)
                {
                    isSuccess = true;
                }
            });
            return isSuccess;
        }
        private async Task<bool> checkNameElementExists(string name)
        {
            string script = string.Format("document.getElementsByName('" + name + "').length;");
            bool elementExists = false;
            await webBrowser.EvaluateScriptAsync(script).ContinueWith(x =>
            {
                var response = x.Result;
                if (response.Success && response.Result != null)
                {
                    elementExists = int.Parse(response.Result.ToString()) > 0;
                }
            });
            return elementExists;
        }

        #endregion "Click button or Set Values to the inputs"

        public void OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IBeforeDownloadCallback callback)
        {
            throw new NotImplementedException();
        }

        public void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser, DownloadItem downloadItem, IDownloadItemCallback callback)
        {
            throw new NotImplementedException();
        }
    }

    public class CaptchaResponse
    {
        public int status { get; set; }
        public string request { get; set; }

    }
}
