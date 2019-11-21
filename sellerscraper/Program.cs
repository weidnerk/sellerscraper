﻿/*
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

namespace webscraper
{
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

        static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                string seller = args[0];
                string connStr = ConfigurationManager.ConnectionStrings["OPWContext"].ConnectionString;
                var settings = db.GetUserSettings(connStr, HOME_DECOR_USER_ID);

                //string seller = "dealocrat";
                //string seller = "fountain-of-deals";
                //string seller = "valleysupershop";
                //string seller = "tuckeronlinesupply";
                //string seller = "optimuze";

                dsutil.DSUtil.WriteFile(_logfile, "~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~", "admin");
                dsutil.DSUtil.WriteFile(_logfile, "Start scan: " + seller, "admin");

                int numSellerItems = 0;
                Task.Run(async () =>
                {
                    //numSellerItems = await FetchSeller(settings, seller, 2);
                    numSellerItems = await FetchSeller(settings, seller, Int32.MaxValue);
                }).Wait();
                dsutil.DSUtil.WriteFile(_logfile, "# seller items: " + numSellerItems, "admin");
                dsutil.DSUtil.WriteFile(_logfile, "Complete scan: " + seller, "admin");
                Process.Start("notepad.exe", "log.txt");
            }
        }

        static async Task<int> FetchSeller(UserSettingsView settings, string seller, int numItemsToFetch)
        {
            //string output = null;
            int numItems = 0;
            
            var sh = new SearchHistory();
            sh.UserId = settings.UserID;
            sh.Seller = seller;
            sh.DaysBack = 30;
            sh.MinSoldFilter = 4;
            sh.StoreID = settings.StoreID;
            var sh_updated = await db.SearchHistoryAdd(sh);

            try
            {
                var modelview = eBayUtility.FetchSeller.ScanSeller(settings, seller, 30);
                int listingCount = modelview.Listings.Count;
                foreach (var listing in modelview.Listings)
                {
                    //output += listing.ItemID + "\n";
                    //output += listing.Title + "\n";

                    var si = await eBayUtility.ebayAPIs.GetSingleItem(listing.ItemID, settings.AppID);
                    var listingStatus = si.SellerListing.ListingStatus;

                    if (listingStatus != "Completed")
                    {
                        if (numItems++ < numItemsToFetch)
                        {
                            Console.WriteLine(numItems + "/" + listingCount);
                            var transactions = NavigateToTransHistory(listing.SellerListing.EbayUrl);

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

                                orderHistory.RptNumber = sh.ID;
                                orderHistory.OrderHistoryDetails = transactions;
                                db.OrderHistorySave(orderHistory);
                                await db.ItemSpecificSave(si.SellerListing.ItemSpecifics);

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
                        break;
                    }
                }
            }
            catch (Exception exc)
            {
                string msg = dsutil.DSUtil.ErrMsg("FetchSeller", exc);
            }
            return numItems;
        }

        /// <summary>
        /// Try not using HTML Agility pack
        /// </summary>
        /// <param name="sellerListingUrl"></param>
        static List<OrderHistoryDetail> NavigateToTransHistory(string sellerListingUrl)
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
                var html = driver.FindElement(By.TagName("html")).GetAttribute("innerHTML");

                var transactions = eBayUtility.FetchSeller.GetTransactionsFromPage(html);

                driver.Quit();
                return transactions;
            }
            catch (Exception exc)
            {
                string str = exc.Message;
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

            var ps =_driver.PageSource.ToString();

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