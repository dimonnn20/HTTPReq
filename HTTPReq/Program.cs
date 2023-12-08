using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Configuration;
using System.IO;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net;

namespace HTTPReq
{
    internal class Program
    {
        private static string Pattern = @"^--- \/ OR\/\d{4}\/\d{2}\/\d{5}$|OF\/\d{4}\/\d{2}\/\d{5} \/ OR\/\d{4}\/\d{2}\/\d{5}$";
        private static string SessionId;
        private static int StartPoint;
        private static int EndPoint;
        static async Task Main(string[] args)
        {
            var sw = new Stopwatch();
            await Console.Out.WriteLineAsync("The program starts creating report with following parameters:");
            //SessionId = ExtractJSessionId(ConfigurationManager.AppSettings["Token"]);
            
            StartPoint = Convert.ToInt32(ConfigurationManager.AppSettings["StartPoint"]);
            EndPoint = Convert.ToInt32(ConfigurationManager.AppSettings["EndPoint"]);
            await Console.Out.WriteLineAsync($"Year of report: {ConfigurationManager.AppSettings["Year"]}");
            await Console.Out.WriteLineAsync($"Month of report: {ConfigurationManager.AppSettings["Month"]}");
            //await Console.Out.WriteLineAsync($"Place to save report : {ConfigurationManager.AppSettings["PathToSaveReport"]}");
            try
            {
                SessionId = await getToken();
                sw.Start();
                List<string> resultList = await Task.Run(async () => await Generate(StartPoint, EndPoint));
                if (resultList.Count != 0)
                {
                    await WriteToFile(resultList);
                    await Console.Out.WriteLineAsync($"Success!\nTime spent : {sw.Elapsed} ");
                }
                else
                {
                    await Console.Out.WriteLineAsync("There were no data that meet the parameters");
                }
                sw.Stop();
            }
            catch (Exception ex)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                await Console.Out.WriteLineAsync(ex.Message);
                Console.ForegroundColor = originalColor;
            }
        }

        static async Task<List<string>> Request(int id)
        {
            List<string> resultOfOneId = new List<string>();
            string numberOfOrder = "";
            DateTime dateOfOrder;

            string url = $"http://crmlog.ewabis.com.pl:8080/crm/Jsp/viewOrder.jsp;jsessionid={SessionId}?command=viewOrder&nextPage=viewOrder.jsp&mode=view&OrderId={id}";
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string htmlContent = await response.Content.ReadAsStringAsync();
                        HtmlDocument htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(htmlContent);
                        HtmlNode bodyNode = htmlDoc.DocumentNode.SelectSingleNode("//body");
                        string text = bodyNode.InnerText.Trim();
                        if (text.Length != 0)
                        {
                            // Find the number of order
                            HtmlNodeCollection paragraphs = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Numer oferty/zam.')]/following-sibling::td");
                            if (paragraphs != null)
                            {
                                foreach (HtmlNode paragraph in paragraphs)
                                {
                                    numberOfOrder = paragraph.InnerText.ToString().Trim();
                                }
                                // Find date of the order
                                HtmlNodeCollection paragraphs2 = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Data zł./sp.: ')]/following-sibling::td");
                                if (Regex.IsMatch(numberOfOrder, Pattern) && paragraphs2 != null)
                                {
                                    foreach (HtmlNode paragraph in paragraphs2)
                                    {
                                        string dateTimeString = paragraph.InnerText.ToString().Trim().Substring(0, 10).Trim();
                                        if (DateTime.TryParseExact(dateTimeString.ToString(), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateOfOrder))
                                        {
                                            if (dateOfOrder.Year == Convert.ToInt32(ConfigurationManager.AppSettings["Year"]) && dateOfOrder.Month == Convert.ToInt32(ConfigurationManager.AppSettings["Month"]))
                                            {
                                                //Going to lines
                                                List<string> nameOfProducts = GetNameOfProductList(htmlDoc);
                                                List<string> nettoPrices = GetNettoPriceList(htmlDoc);
                                                List<string> currencyList = GetCurrencyList(htmlDoc);
                                                List<string> category = GetCategoryList(nameOfProducts);

                                                if (nameOfProducts.Count == nettoPrices.Count && nettoPrices.Count == currencyList.Count)
                                                {
                                                    int count = nameOfProducts.Count;
                                                    StringBuilder stringBuilder = new StringBuilder();
                                                    for (int i = 0; i < count; i++)
                                                    {
                                                        stringBuilder.Append(numberOfOrder).Append(";").Append(dateOfOrder.ToString("yyyy-MM-dd")).Append(";").Append(nameOfProducts[i]).Append(";").Append(nettoPrices[i]).Append(";").Append(currencyList[i]).Append(";").Append(category[i]).Append(";").Append(id);
                                                        resultOfOneId.Add(stringBuilder.ToString());
                                                        stringBuilder.Clear();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            throw new Exception("Access token is not correct");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"ERROR: {response.StatusCode} - {response.ReasonPhrase}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }
            }
            return resultOfOneId;
        }

        private static List<string> GetCategoryList(List<string> nameOfProducts)
        {
            List<string> result = new List<string>();
            foreach (var item in nameOfProducts)
            {
                if (item.StartsWith("Etykiet") || item.StartsWith("Taśma") || item.StartsWith("Winietka") || item.StartsWith("Kalka") || item.StartsWith("Tasma") || item.StartsWith("TTR"))
                {
                    result.Add("Consumables");
                }
                else if (item.StartsWith("Midmeki") || item.StartsWith("Logopak") || item.StartsWith("Drukarka") || item.StartsWith("Meki") || item.Contains("Logomatic") || item.Contains("Maszyna") || item.StartsWith("Detektor"))
                {
                    result.Add("Machine");
                }
                else if (item.StartsWith("Print") || item.StartsWith("Moduł") || item.StartsWith("Usługa") || item.StartsWith("Roboczogodzina") || item.StartsWith("Dojazd") || item.StartsWith("Płyt")
                     || item.StartsWith("dojazd") || item.StartsWith("Wizyta") || item.StartsWith("wizyta") || item.StartsWith("Płyn") || item.StartsWith("Nocleg") || item.Contains("Filter") || item.Contains("Belt") || StartsWithDigit(item)
                     || item.StartsWith("dostawa") || item.StartsWith("Dostawa") || item.StartsWith("Diet") || item.StartsWith("Hotel") || item.StartsWith("Kamera") || item.StartsWith("opakowanie") || item.StartsWith("Transport")
                     || item.StartsWith("Travel") || item.StartsWith("usługa") || item.StartsWith("Work") || item.StartsWith("Zestaw") || item.StartsWith("Tooth") || item.StartsWith("Tester") || item.StartsWith("S") || item.StartsWith("s") || item.Contains("motor")
                     || item.StartsWith("instalacja") || item.StartsWith("Bulb") || item.StartsWith("Bateria") || item.Contains("Rubber") || item.Contains("Rolka") || item.StartsWith("Cost") || item.Contains("switch") || item.Contains("gumowa") || item.Contains("Belt") || item.Contains("traveling") || item.Contains("Pasek")
                     || item.Contains("socket") || item.Contains("roller") || item.Contains("Bearing") || item.Contains("Łożysko") || item.Contains("Fotokomórka") || item.StartsWith("Delivery") || item.Contains("Głowica") || item.Contains("EPROM")
                     || item.Contains("Patyczki") || item.Contains("Roundbelt") || item.Contains("LogoCare") || item.Contains("LogoClean") || item.Contains("electronic") || item.StartsWith("Mod")
                     || item.Contains("Rurka") || item.Contains("Terminal") || item.Contains("Instalacja") || item.Contains("travel") || item.Contains("Sensor") || item.Contains("BATTERY") || item.Contains("mocujący") || item.Contains("Battery") || item.Contains("adapter") || item.Contains("adapter") || item.Contains("Supply") || item.Contains("CPU") || item.Contains("Motor"))
                {
                    result.Add("Service");
                }
                else
                {
                    result.Add("");
                }
            }
            return result;
        }

        private static bool StartsWithDigit(string item)
        {
            // Проверяем, не пустая ли строка и начинается ли она с цифры
            return !string.IsNullOrEmpty(item) && char.IsDigit(item.FirstOrDefault());
        }

        static async Task<List<string>> Generate(int startId, int endId)
        {
            try
            {
                Random random = new Random();
                Stopwatch sw = new Stopwatch();
                await Console.Out.WriteLineAsync("The program started generating report");
                List<string> list = new List<string>();
                for (int i = startId; i <= endId; i++)
                {
                    sw.Start();
                    if (i % 100 == 0)
                    {
                        sw.Stop();
                        int timeLeft = ((int)sw.Elapsed.TotalSeconds * ((endId - i) / 100));
                        Console.SetCursorPosition(0, Console.CursorTop - 1);
                        Console.WriteLine($"Completed {i} iterations from {endId}, time left = " + (timeLeft == 0 ? ".." : timeLeft.ToString()) + " seconds ");
                        sw.Restart();
                    }
                    List<string> tempList = await Request(i);
                    list.AddRange(tempList);
                }
                await Console.Out.WriteLineAsync("The program ended generating report");
                return list;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in method Generate: {ex.Message}");
                throw;
            }
        }

        static List<string> GetNameOfProductList(HtmlDocument htmlDoc)
        {
            //var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@valign='top']//a");
            var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@class='maindata']//a");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    values.Add(RemoveParagraphs(tdNode.InnerText.Trim()).Replace(';', ','));
                }
            }
            return values;
        }

        static List<string> GetNettoPriceList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//table[@class='tablelist itchistory']//td[@valign='top'][position() = 11]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str.Replace(',', ' ').Replace('.', ','));
                }
            }
            return values;
        }

        static List<string> GetCurrencyList(HtmlDocument htmlDoc)
        {
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//table[@class='tablelist itchistory']//td[@valign='top'][position() = 14]");
            List<string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    string str = tdNode.InnerText.Trim();
                    values.Add(str);
                }
            }
            return values;
        }

        static async Task WriteToFile(List<string> lines)
        {
            //int length = str.Length;
            //string pathToSave = @"C:\folderToDel\strContent.txt";
            //using (FileStream stream = new FileStream(pathToSave, FileMode.OpenOrCreate))
            //{
            //    await stream.WriteAsync(Encoding.Default.GetBytes(str), 0, Encoding.Default.GetByteCount(str));
            //    await stream.FlushAsync();
            //    await Console.Out.WriteLineAsync("Successfuly");
            //}
            //await Console.Out.WriteLineAsync($"Length of saved string is = {File.ReadAllBytes(pathToSave).Length} ");
            string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}.txt";
            //string pathToSave = ConfigurationManager.AppSettings["PathToSaveReport"] + fileName;
            string pathToSave = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            using (FileStream stream = new FileStream(pathToSave, FileMode.OpenOrCreate))
            {
                foreach (string line in lines)
                {
                    await stream.WriteAsync(Encoding.Default.GetBytes(line), 0, Encoding.Default.GetByteCount(line));
                    await stream.WriteAsync(Encoding.Default.GetBytes("\n"), 0, 1);
                }

            }
            await Console.Out.WriteLineAsync($"Report is successfuly saved to {pathToSave}");

        }
        static string RemoveParagraphs(string input)
        {
            // Замена символов новой строки на пустую строку

            string result = input.Replace("\n", "");
            result = result.Replace("\r", "");
            result = result.Replace("\t", "");
            return result;
        }

        static string ExtractJSessionId(string input)
        {

            string jsessionId;
            // Ищем индекс начала подстроки "jsessionid=".
            int startIndex = input.IndexOf("jsessionid=") + "jsessionid=".Length;
            int endIndex = input.IndexOf("?");
            if (startIndex > 0)
            {
                // Вырезаем подстроку, начиная с "jsessionid=".
                int length = endIndex - startIndex;
                jsessionId = input.Substring(startIndex, length);
                Console.WriteLine($"jsessionId = {jsessionId}");
                // Ищем конец значения jsessionid (первый символ, не являющийся символом шестнадцатеричной цифры).
                return jsessionId;
            }

            return string.Empty;
        }

        static async Task<string> getToken()
        {
            string token = "";
            // Замените URL на тот, который вы хотите использовать
            string url = "http://crmlog.ewabis.com.pl:8080/crm/Jsp/commandCenterAction.jsp";

            // Замените значения параметров на свои
            string login = ConfigurationManager.AppSettings["Login"]; ;
            string password = ConfigurationManager.AppSettings["Password"]; ;

            // Создайте HttpClient
            using (HttpClient client = new HttpClient())
            {
                // Создайте данные формы
                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("command", "login"),
                new KeyValuePair<string, string>("nextPage", "welcomeFrame.jsp"),
                new KeyValuePair<string, string>("login", login),
                new KeyValuePair<string, string>("password", password),
                new KeyValuePair<string, string>("isExplorer", "0"),
                new KeyValuePair<string, string>("isExplorer10up", "0"),
                new KeyValuePair<string, string>("isFirefox", "0"),
                new KeyValuePair<string, string>("isSafari", "1"),
                new KeyValuePair<string, string>("isMobile", "0"),
                new KeyValuePair<string, string>("isiPad", "0"),
                new KeyValuePair<string, string>("submited.x", "40"),
                new KeyValuePair<string, string>("submited.y", "8"),
                new KeyValuePair<string, string>("LoginInternalConnection", "1"),
                new KeyValuePair<string, string>("LoginReload", "1"),
            });

                // Отправьте POST-запрос с данными формы
                HttpResponseMessage response = await client.PostAsync(url, formContent);

                // Печать статуса ответа
                //Console.WriteLine($"Статус код: {response.StatusCode}");

                // Печать тела ответа
                string responseBody = await response.Content.ReadAsStringAsync();
                //Console.WriteLine($"Тело ответа: {responseBody}");
                int startIndex = responseBody.IndexOf("jsessionid=") + "jsessionid=".Length;
                int endIndex = responseBody.IndexOf("?Param=True", startIndex);

                if (startIndex >= 0 && endIndex >= 0)
                {
                    token = responseBody.Substring(startIndex, endIndex - startIndex);
                    await Console.Out.WriteLineAsync("Log in successfuly");
                }
                else
                {
                    throw new Exception("Session id is not found. Login or password are not correct !!! ");
                }
            }


            return token;
        }
    }
}
