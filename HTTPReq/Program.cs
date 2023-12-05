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

namespace HTTPReq
{
    internal class Program
    {
        private static string Pattern = @"^--- \/ OR\/\d{4}\/\d{2}\/\d{5}$|OF\/\d{4}\/\d{2}\/\d{5} \/ OR\/\d{4}\/\d{2}\/\d{5}$";
        private static string SessionId = ExtractJSessionId(ConfigurationManager.AppSettings["Token"]);
        static async Task Main(string[] args)
        {

            await Console.Out.WriteLineAsync("The program starts working");
            List<string> resultList = await Task.Run(async () => await Generate(17286, 17515));
            await WriteToFile(resultList);
            await Console.Out.WriteLineAsync("success");

            //List<string> resultList = await Task.Run(async () => await Request(17288));
            //await WriteToFile(resultList);
            //foreach (string result in resultList) { await Console.Out.WriteLineAsync(result); }
        }

        static async Task<List<string>> Request(int id)
        {
            List<string> resultOfOneId = new List<string>();
            string numberOfOrder = "";
            string dateOfOrder = "";
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

                        //HtmlNodeCollection paragraphs = htmlDoc.DocumentNode.SelectNodes("//table//tr/td");

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
                                    dateOfOrder = paragraph.InnerText.ToString().Trim().Substring(0, 10).Trim();
                                }
                                //Going to lines
                                List<string> nameOfProducts = GetNameOfProductList(htmlDoc);
                                List<string> nettoPrices = GetNettoPriceList(htmlDoc);
                                if (nameOfProducts.Count == nettoPrices.Count)
                                {
                                    int count = nameOfProducts.Count;
                                    StringBuilder stringBuilder = new StringBuilder();
                                    for (int i = 0; i < count; i++)
                                    {
                                        stringBuilder.Append(numberOfOrder).Append(";").Append(dateOfOrder).Append(";").Append(nameOfProducts[i]).Append(";").Append(nettoPrices[i]).Append(";").Append(id);
                                        resultOfOneId.Add(stringBuilder.ToString());
                                        stringBuilder.Clear();
                                    }
                                }
                            }
                            //await Console.Out.WriteLineAsync(htmlContent);
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
                }
            }
            return resultOfOneId;
        }

        static async Task<List<string>> Generate(int startId, int endId)
        {
            await Console.Out.WriteLineAsync("The program starts generating report");
            List<string> list = new List<string>();
            for (int i = startId; i <= endId; i++)
            {
                List<string> tempList = await Request(i);
                list.AddRange(tempList);
            }
            await Console.Out.WriteLineAsync("The program ends generating report");
            return list;

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
            // Выполнение запроса XPath для выбора текста из второго и третьего тегов <TD>
            var tdNodes = htmlDoc.DocumentNode.SelectNodes("//table[@class='tablelist itchistory']//td[@valign='top'][position() = 11]");

            // Инициализация списка для хранения значений
            List<string> values = new List<string>();

            // Добавление значений из выбранных тегов <TD> в список
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
            string pathToSave = ConfigurationManager.AppSettings["PathToSaveReport"] + fileName;
            using (FileStream stream = new FileStream(pathToSave, FileMode.OpenOrCreate))
            {
                foreach (string line in lines)
                {
                    await stream.WriteAsync(Encoding.Default.GetBytes(line), 0, Encoding.Default.GetByteCount(line));
                    await stream.WriteAsync(Encoding.Default.GetBytes("\n"), 0, 1);
                }

            }

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
    }
}
