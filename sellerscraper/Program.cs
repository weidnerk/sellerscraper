/*
 * another YT reference
 * 
 * https://www.youtube.com/watch?v=SKz4VYj5AzQ
 *
 * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 * 02.13.2020
 * Got error about selenium not supporting my version of chrome.  Not sure what happened - possibly chrome updated overnight.
 * Opened up installed packages and first of all removed outdated phantom drivers.
 * 
 * as far as i know, to do chrome automation with selenium, looks like you need 2 things: chromedriver and selenium webdriver.
 * 
 * selenium:
 * https://www.nuget.org/packages/Selenium.WebDriver
 * just one file, chromedriver.exe which goes in bin folder.
 * 
 * chromedriver:
 * https://chromedriver.chromium.org/home
 * When you download this, just one file, chromedriver.exe which goes in your bin folder.
 * 
 * At this time, i installed v3.141 of webdriver and
 * v80.0.3987.16 of chromedriver.
 * 
 * In project references, you see a reference to WebDriver but not chromedriver.
 * 
 * and another things, I opened chrome on desktop and checked version and it started updating to latest version.
 * not sure why this doesn't happen automatically.  was on 79, upgraded to 80.
 * ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
 * 
 */
using dsmodels;
using eBayUtility.WebReference;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

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
            try
            {
                if (args.Length > 0)
                {
                    seller = args[0];
                    var exists = db.SellerExists(seller);
                    if (!exists)
                    {
                        Console.WriteLine("ERROR: Seller does not exist.");
                        return;
                    }
                }
                if (args.Length > 1)
                {
                    numDaysBack = Convert.ToInt32(args[1]);
                }
              
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettingsView(connStr, HOME_DECOR_USER_ID);

                dsutil.DSUtil.WriteFile(_logfile, "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", "admin");
                dsutil.DSUtil.WriteFile(_logfile, "Start scan: " + seller, "admin");

                int numSellerItems = 0;
                Task.Run(async () =>
                {
                    if (args.Length > 0)
                    {
                        numSellerItems = await FetchSeller(settings, seller, Int32.MaxValue, numDaysBack);
                    }
                    else
                    {
                        var sellers = db.GetSellers();
                        bool runScan = false;
                        foreach (var item in sellers)
                        {
                            runScan = false;
                            seller = item.Seller;
                            var sellerProfile = await db.SellerProfileGet(item.Seller);
                            if (sellerProfile == null)
                            {
                                runScan = true;
                            }
                            else
                            {
                                if (sellerProfile.Active)
                                {
                                    runScan = true;
                                }
                            }
                            if (runScan)
                            {
                                numSellerItems = await FetchSeller(settings, item.Seller, Int32.MaxValue, numDaysBack);
                            }
                        }
                    }
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
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("Main, seller: " + seller, exc);
                dsutil.DSUtil.WriteFile(_logfile, msg, "");
            }
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
            int numItems = 0;
            int completedItems = 0;
            bool retRemoveDetail = false;

            var sh = new SearchHistory();
            sh.UserId = settings.UserID;
            sh.Seller = seller;
            sh.DaysBack = daysToScan;
            //sh.MinSoldFilter = 4;

            int? rptNumber = null;
            DateTime? fromDate;
            rptNumber = db.LatestRptNumber(seller);
            if (rptNumber.HasValue)
            {
                sh.ID = rptNumber.Value;
            }
            if (daysToScan > 0)
            {
                // passed an override value for date to start scan
                fromDate = DateTime.Now.AddDays(-daysToScan);
            }
            else
            {
                // else calculate start scanning from seller's last sale
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
                sh.Updated = DateTime.Now;
                var sh_updated = await db.SearchHistoryAdd(sh);
                rptNumber = sh_updated.ID;
            }
            else
            {
                sh.Updated = DateTime.Now;
                db.SearchHistoryUpdate(sh, "Updated");
                retRemoveDetail = await db.HistoryDetailRemove(rptNumber.Value, fromDate.Value);
            }
            try
            {
                // Use eBay API findCompletedItems() to get seller's sold listings but then need to use Selenium to get actual sales by day.
                var modelview = eBayUtility.FetchSeller.ScanSeller(settings, seller, fromDate.Value);
                if (modelview != null)
                {
                    int listingCount = modelview.Listings.Count;
                    dsutil.DSUtil.WriteFile(_logfile, "scan seller count: " + listingCount, "admin");
                    string lastItemID = null;
                    foreach (var listing in modelview.Listings.OrderBy(o => o.ItemID))
                    {
                        if (lastItemID != listing.ItemID)
                        {
                            //output += listing.Title + "\n";      if (listing.ItemID == "392535456736")
                            if (true)
                            {
                                var si = await eBayUtility.ebayAPIs.GetSingleItem(settings, listing.ItemID);
                                var listingStatus = si.ListingStatus;

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
                                        if (true)   // listing.ItemID == "312839444438"
                                        {
                                            Console.WriteLine(numItems + "/" + listingCount);
                                            var transactions = NavigateToTransHistory(listing.SellerListing.EbayURL, listing.ItemID);

                                            if (transactions != null)
                                            {
                                                var orderHistory = new OrderHistory();
                                                orderHistory.ItemID = listing.ItemID;
                                                orderHistory.Title = listing.SellerListing.Title;
                                                orderHistory.EbayURL = listing.SellerListing.EbayURL;
                                                orderHistory.PrimaryCategoryID = listing.PrimaryCategoryID;
                                                orderHistory.PrimaryCategoryName = listing.PrimaryCategoryName;
                                                orderHistory.EbaySellerPrice = listing.SellerListing.SellerPrice;
                                                orderHistory.Description = si.Description;
                                                orderHistory.ListingStatus = listingStatus;
                                                orderHistory.IsSellerVariation = listing.SellerListing.Variation;

                                                orderHistory.RptNumber = rptNumber.Value;
                                                orderHistory.OrderHistoryDetails = transactions;
                                                string orderHistoryOutput = db.OrderHistorySave(orderHistory, fromDate.Value);
                                                string specificOutput = await db.OrderHistoryItemSpecificSave(dsmodels.DataModelsDB.CopyFromSellerListing(si.ItemSpecifics));
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    ++completedItems;
                                }
                            }
                        }
                        lastItemID = listing.ItemID;
                    }
                    dsutil.DSUtil.WriteFile(_logfile, "completed items count: " + completedItems, "admin");
                }
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
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(60);
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
                driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(60);
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
            driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(60);

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

    }
}
