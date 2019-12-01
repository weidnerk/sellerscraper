/*
 * another YT reference
 * 
 * https://www.youtube.com/watch?v=SKz4VYj5AzQ
 *
 */

using dsmodels;
using eBayUtility;
using eBayUtility.WebReference;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.PhantomJS;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Entity;

namespace webscraper
{
    public class ListingStatus
    {
        public int Active { get; set; }
        public int Completed { get; set; }
        public int Custom { get; set; }
        public int CustomCode { get; set; }
        public int Ended { get; set; }
    }
    public class SellingState
    {
        public int Active { get; set; }
        public int Canceled { get; set; }
        public int Ended { get; set; }
        public int EndedWithSales { get; set; }
        public int EndedWithoutSales { get; set; }
    }
    class Program
    {
        // static string url = "https://www.walmart.com/ip/Sauder-Beginnings-Dresser-Highland-Oak-Finish/260081375";
        //static string url = "https://www.walmart.com/ip/5-Drawers-Chest-Dresser-White/430586811?athcpid=430586811&athpgid=athenaItemPage&athcgid=null&athznid=PWVUB&athieid=v0&athstid=CS054&athguid=752143b2-4dd-16df492e578199&athancid=null&athena=true";
        static string url = "https://www.ebay.com/itm/Coleman-Camp-Chef-Oven-Stove-Aluminum-Cookware-Bakeware-Portable-Baking-Device-/163163384942";
        //static string url = "https://www.ebay.com/itm/45-Piece-White-Dinnerware-Set-Square-Banquet-Plates-Dishes-Bowls-Kitchen-Dinner/181899005040";
        static string api_key = "ak-mc65k-44235-ae31p-4bsng-ygmrn";
        private static IWebDriver _driver;
        readonly static string _logfile = "log.txt";
        readonly static string HOME_DECOR_USER_ID = "65e09eec-a014-4526-a569-9f2d3600aa89";

        static DataModelsDB db = new DataModelsDB();

        static ListingStatus ListingStatusObject = new ListingStatus();
        static SellingState SellingStateObject = new SellingState();

        static void ListingStatusCount(string listingStatus)
        {
            switch (listingStatus)
            {
                case "Ended":
                    ++ListingStatusObject.Ended;
                    break;

                case "Active":
                    ++ListingStatusObject.Active;
                    break;

                case "Completed":
                    ++ListingStatusObject.Completed;
                    break;
            }
        }
        static void SellingStateCount(string sellingState)
        {
            switch (sellingState)
            {
                case "Active":
                    ++SellingStateObject.Active;
                    break;

                case "Canceled":
                    ++SellingStateObject.Canceled;
                    break;

                case "Ended":
                    ++SellingStateObject.Ended;
                    break;

                case "EndedWithSales":
                    ++SellingStateObject.EndedWithSales;
                    break;

                case "EndedWithoutSales":
                    ++SellingStateObject.EndedWithoutSales;
                    break;
            }
        }
        static void Main(string[] args)
        {
            //Console.WriteLine(string.Join("\n", TimeZoneInfo.GetSystemTimeZones().OrderBy(o => o.Id).Select(x => x.Id)));

            string seller = null;
            int numDaysBack = 0;
            if (args.Length > 0)
            {
                seller = args[0];
            }
            if (args.Length > 1)
            {
                numDaysBack = Convert.ToInt32(args[1]);
            }
            if (args.Length == 0)
            {
                Console.WriteLine("invalid usage.");
                return;
            }
            string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
            var settings = db.GetUserSettings(connStr, HOME_DECOR_USER_ID);

            dsutil.DSUtil.WriteFile(_logfile, "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Start scan: " + seller, "admin");

            int numSellerItems = 0;
            Task.Run(async () =>
            {
                //numSellerItems = await FetchSeller(settings, seller, 2);
                numSellerItems = await FetchSeller(settings, seller, Int32.MaxValue, numDaysBack);
            }).Wait();
            dsutil.DSUtil.WriteFile(_logfile, "# seller items: " + numSellerItems, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Complete scan: " + seller, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "LISTING STATUS ", "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Completed: " + ListingStatusObject.Completed, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Active: " + ListingStatusObject.Active, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Ended: " + ListingStatusObject.Ended, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "SELLING STATE ", "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Canceled: " + SellingStateObject.Canceled, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Active: " + SellingStateObject.Active, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "Ended: " + SellingStateObject.Ended, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "EndedWithSales: " + SellingStateObject.EndedWithSales, "admin");
            dsutil.DSUtil.WriteFile(_logfile, "EndedWithoutSales: " + SellingStateObject.EndedWithoutSales, "admin");
            Process.Start("notepad.exe", "log.txt");
        }

        static string SellingState(string itemID, List<SearchResult> searchResult)
        {
            foreach (var x in searchResult)
            {
                foreach (var y in x.item)
                {
                    if (y.itemId == itemID)
                    {
                        return y.sellingStatus.sellingState;
                    }
                }
            }
            return null;
        }

        static async Task<int> FetchSeller(UserSettingsView settings, string seller, int numItemsToFetch, int daysToScan)
        {
            //string output = null;
            int numItems = 0;
            int completedItems = 0;

            var sh = new SearchHistory();
            sh.UserId = settings.UserID;
            sh.Seller = seller;
            sh.DaysBack = 30;
            sh.MinSoldFilter = 4;
            sh.StoreID = settings.StoreID;

            int? rptNumber = null;
            DateTime? fromDate;
            if (daysToScan > 0) 
            {
                // passed an override value for date to start scan
                fromDate = DateTime.Now.AddDays(-daysToScan);
            }
            else
            {
                // else calculate start scanning from seller's last sale
                rptNumber = db.LatestRptNumber(seller);
                if (rptNumber.HasValue)
                {
                    fromDate = db.fromDateToScan(rptNumber.Value);
                }
                else
                {
                    fromDate = DateTime.Now.AddDays(-30);
                }
            }
            if (!rptNumber.HasValue || rptNumber.Value == 0) 
            {
                // first time running seller
                var sh_updated = await db.SearchHistoryAdd(sh);
                rptNumber = sh_updated.ID;
            }
            else
            {
                //fromDate = new DateTime(2019, 11, 29);
                await db.HistoryDetailRemove(rptNumber.Value, fromDate.Value);
            }
            try
            {
                var modelview = eBayUtility.FetchSeller.ScanSeller(settings, seller, fromDate.Value);
                int listingCount = modelview.Listings.Count;
                dsutil.DSUtil.WriteFile(_logfile, "scan seller count: " + listingCount, "admin");
                foreach (var listing in modelview.Listings)
                {
                    //output += listing.Title + "\n";      if (listing.ItemID == "352726925834")
                    if (true)
                    {
                        var si = await eBayUtility.ebayAPIs.GetSingleItem(listing.ItemID, settings.AppID);
                        var listingStatus = si.SellerListing.ListingStatus;

                        // when navigating to a listing from the website, ebay adds a hash
                        // you don't seem to need it unless navigating to a completed item in which case it doesn't work
                        // i'm not sure how to generate the hash

                        ListingStatusCount(listingStatus);
                        var sellingState = SellingState(listing.ItemID, modelview.SearchResult);
                        SellingStateCount(sellingState);

                        if (listingStatus != "Completed")
                        {
                            if (numItems++ < numItemsToFetch)
                            {
                                Console.WriteLine(numItems + "/" + listingCount);
                                var transactions = NavigateToTransHistory(listing.SellerListing.EbayUrl, listing.ItemID);

                                if (transactions != null)
                                {
                                    var orderHistory = new OrderHistory();
                                    orderHistory.ItemID = listing.ItemID;
                                    orderHistory.Title = listing.SellerListing.Title;
                                    orderHistory.EbayUrl = listing.SellerListing.EbayUrl;
                                    orderHistory.PrimaryCategoryID = listing.PrimaryCategoryID;
                                    orderHistory.PrimaryCategoryName = listing.PrimaryCategoryName;
                                    orderHistory.EbaySellerPrice = listing.SellerListing.SellerPrice;
                                    orderHistory.Description = si.Description;
                                    orderHistory.ListingStatus = listingStatus;
                                    orderHistory.IsMultiVariationListing = listing.SellerListing.Variation;

                                    orderHistory.RptNumber = rptNumber.Value;
                                    orderHistory.OrderHistoryDetails = transactions;
                                    string orderHistoryOutput = db.OrderHistorySave(orderHistory, fromDate.Value);
                                    string specificOutput = await db.ItemSpecificSave(si.SellerListing.ItemSpecifics);

                                    //string output = eBayUtility.FetchSeller.DumpItemSpecifics(si.SellerListing.ItemSpecifics);

                                    // write to log
                                    //foreach (var order in transactions)
                                    //{
                                    //    output += order.Qty + "\n";
                                    //    output += order.Price + "\n";
                                    //    output += order.DateOfPurchase + "\n";
                                    //}
                                }
                                else
                                {
                                    //output += "No transactions\n";
                                }
                                //output += "\n";
                            }
                        }
                        else
                        {
                            ++completedItems;
                        }
                    }
                }
                dsutil.DSUtil.WriteFile(_logfile, "completed items count: " + completedItems, "admin");
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("FetchSeller", exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
            }
            return numItems;
        }

        /// <summary>
        /// Try not using HTML Agility pack
        /// </summary>
        /// <param name="sellerListingUrl"></param>
        static List<OrderHistoryDetail> NavigateToTransHistory(string sellerListingUrl, string itemID)
        {
            List<OrderHistoryDetail> transactions = null;
            IWebDriver driver = new ChromeDriver();
            try
            {
                Thread.Sleep(2000);
                driver.Navigate().GoToUrl(sellerListingUrl);
                driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(60));
                Thread.Sleep(2000);

                var element = driver.FindElement(By.XPath("//a[contains(@href, 'https://offer.ebay.com/ws/eBayISAPI.dll?ViewBidsLogin')]"));
                if (element == null)
                {
                    dsutil.DSUtil.WriteFile(_logfile, "Could not navigate to purchase history: " + sellerListingUrl, "admin");
                }
                else
                {
                    element.Click();

                    Thread.Sleep(3000);
                    var html = driver.FindElement(By.TagName("html")).GetAttribute("innerHTML");

                    transactions = eBayUtility.FetchSeller.GetTransactionsFromPage(html, itemID);
                }
                driver.Quit();
                return transactions;
            }
            catch (Exception exc)
            {
                driver.Quit();
                string msg = dsutil.DSUtil.ErrMsg("NavigateToTransHistory", exc);
                dsutil.DSUtil.WriteFile(_logfile, itemID + ": " + msg, "");
                return null;
            }
        }

        static void NavigateToTransHistory_pristine(string sellerListingUrl)
        {
            try
            {
                IWebDriver driver = new ChromeDriver();
                Thread.Sleep(2000);
                driver.Navigate().GoToUrl(sellerListingUrl);
                driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(60));
                Thread.Sleep(2000);

                var element = driver.FindElement(By.XPath("//a[contains(@href, 'https://offer.ebay.com/ws/eBayISAPI.dll?ViewBidsLogin')]"));
                element.Click();
                Thread.Sleep(3000);
                driver.Quit();
            }
            catch (Exception exc)
            {
                string str = exc.Message;
            }
        }

        /// <summary>
        /// Go to seller's listing transaction history page (using Selenium) and parse the transactions
        /// Uses HTML Agility Pack
        /// </summary>
        static void NavigateToTransHistory_HTMLAgilityPack(string sellerListingUrl)
        {
            IWebDriver driver = new ChromeDriver();
            Thread.Sleep(2000);
            driver.Navigate().GoToUrl(sellerListingUrl);
            driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(60));

            var html = driver.FindElement(By.TagName("html")).GetAttribute("innerHTML");
            var h = driver.PageSource;
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var n = doc.DocumentNode.SelectSingleNode("//span[@class='soldwithfeedback']");
            if (n != null)
            {
                var links = n.Descendants("a")
                               .Select(a => a.GetAttributeValue("href", string.Empty))
                               .ToList();

                dsutil.DSUtil.WriteFile(_logfile, links[0], "");

                string value2 = WebUtility.HtmlDecode(links[0]);
                driver.Navigate().GoToUrl(value2);
                Thread.Sleep(2000);
                // driver.Manage().Timeouts().ImplicitlyWait(TimeSpan.FromSeconds(7));
            }

            File.WriteAllText(@"C:\temp\htmlloadertext.html", h);
            driver.Quit();
        }
        static void Wikipedia()
        {
            IWebDriver driver = new ChromeDriver();
            driver.Navigate().GoToUrl("https://www.wikipedia.org/");
            ReadOnlyCollection<IWebElement> anchorList = driver.FindElements(By.TagName("a"));
            foreach (IWebElement anchor in anchorList)
            {
                if (anchor.Text.Length > 0)
                {
                    if (anchor.Text.Contains("English"))
                    {
                        anchor.Click();
                    }
                }
            }
            driver.Quit();
        }
        /// <summary>
        /// Cool seeing Chrome opened
        /// https://www.youtube.com/watch?v=wtH4i7CPg1M
        /// </summary>
        static void YTVideo()
        {
            IWebDriver driver = new ChromeDriver();
            driver.Navigate().GoToUrl(url);
            var html = driver.FindElement(By.TagName("html")).GetAttribute("innerHTML");
            var h = driver.PageSource;
            File.WriteAllText(@"C:\temp\htmlloadertext.html", h);
            driver.Quit();
        }

        static void getpage2()
        {
            _driver = new PhantomJSDriver();
            _driver.Navigate().GoToUrl(url);

            //IWebElement element = _driver.FindElement(By.Name("q"));
            //string stringToSearchFor = "BDDfy";
            //element.SendKeys(stringToSearchFor);
            //element.Submit();

            var ps = _driver.PageSource.ToString();

            // Assert.That(_driver.Title, Is.StringContaining(stringToSearchFor));
            ((ITakesScreenshot)_driver).GetScreenshot().SaveAsFile("c:\\temp\\wm.png", ImageFormat.Png);
            _driver.Quit();
        }

        /// <summary>
        /// Remember, PhantomJS is no longer a supported project.
        /// </summary>
        static void getpage()
        {
            var service = PhantomJSDriverService.CreateDefaultService();
            service.SslProtocol = "any"; //"any" also works

            var driverService = PhantomJSDriverService.CreateDefaultService();
            driverService.LocalToRemoteUrlAccess = true;

            var driver = new PhantomJSDriver(service);
            driver.Url = url;
            driver.Navigate();
            //the driver can now provide you with what you need (it will execute the script)
            //get the source of the page
            var source = driver.PageSource;
            //fully navigate the dom
            // var pathElement = driver.FindElementById("some-id");
            var html = driver.FindElementByTagName("html").GetAttribute("innerHTML");
            File.WriteAllText(@"C:\temp\htmlloadertext.html", html);
            driver.Quit();
        }

        //public static string GetPagePhantomJs(string url)
        //{
        //    try
        //    {
        //        using (var client = new System.Net.Http.HttpClient())
        //        {
        //            client.DefaultRequestHeaders.ExpectContinue = false;
        //            var pageRequestJson = new System.Net.Http.StringContent(@"{'url':'" + url + "','renderType':'html','outputAsJson':false }");
        //            var response = client.PostAsync("https://PhantomJsCloud.com/api/browser/v2/ak-mc65k-44235-ae31p-4bsng-ygmrn/", pageRequestJson).Result;
        //            return response.Content.ReadAsStringAsync().Result;
        //        }
        //    }
        //    catch (Exception exc)
        //    {
        //        string msg = exc.Message;
        //        return null;
        //    }
        //}

        public static async Task<string> GetPagePhantomJs_v2(string url)
        {
            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.ExpectContinue = false; //REQUIRED! or you will get 502 Bad Gateway errors
                                                                         //you should look at the HTTP Endpoint docs, section about "userRequest" and "pageRequest" 
                                                                         //for a listing of all the parameters you can pass via the "pageRequestJson" variable.
                    var pageRequestJson = new System.Net.Http.StringContent(@"{'url':'" + url + "','renderType':'html','outputAsJson':false }");
                    var response = await client.PostAsync(string.Format("https://PhantomJScloud.com/api/browser/v2/{0}/", api_key), pageRequestJson);
                    var responseString = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("*** HTTP Request Finish ***");
                    Console.WriteLine(responseString);
                    return null;
                }
            }
            catch (Exception exc)
            {
                string msg = exc.Message;
                return null;
            }
        }
    }
}
