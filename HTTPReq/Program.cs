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

namespace HTTPReq
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            List <string> resultList = await Task.Run(async () => await Generate(17530,17532));
            await WriteToFile(resultList);
            await Console.Out.WriteLineAsync("success");
            //List<string> resultList = await Task.Run(async () => await Request(17530));
            //List<string> resultList2 = await Task.Run(async () => await Request(17531));
            //List<string> resultList3 = await Task.Run(async () => await Request(17532));

            //            List<string> resultList4 = await Task.Run(async () => await Request(17533));
            //List<string> resultList5 = await Task.Run(async () => await Request(17534));
            //List<string> resultList6 = await Task.Run(async () => await Request(17535));
            //List<string> resultList7= await Task.Run(async () => await Request(17536));
            //List<string> resultList8 = await Task.Run(async () => await Request(17537));
            //List<string> resultList9 = await Task.Run(async () => await Request(17538));

            //foreach (string result in resultList) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList2) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList3) { await Console.Out.WriteLineAsync(result); }
            //           foreach (string result in resultList4) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList5) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList6) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList7) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList8) { await Console.Out.WriteLineAsync(result); }
            //foreach (string result in resultList9) { await Console.Out.WriteLineAsync(result); }
        }

        static async Task <List<string>> Request(int id)
        {
            List<string> resultOfOneId = new List<string>();
            string numberOfOrder = "";
            string dateOfOrder = "";
            string url = $"http://crmlog.ewabis.com.pl:8080/crm/Jsp/viewOrder.jsp;jsessionid={ConfigurationManager.AppSettings["Token"]}?command=viewOrder&nextPage=viewOrder.jsp&mode=view&OrderId={id}";
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
                        }
                        else
                        {
                            throw new Exception("There is no records or access denied");
                        }

                        // Find date of the order
                        HtmlNodeCollection paragraphs2 = htmlDoc.DocumentNode.SelectNodes("//td[contains(b, 'Data zł./sp.: ')]/following-sibling::td");
                        if (paragraphs2 != null)
                        {
                            foreach (HtmlNode paragraph in paragraphs2)
                            {
                                dateOfOrder = paragraph.InnerText.ToString().Trim().Substring(0, 10).Trim();
                            }
                        }
                        else
                        {
                            throw new Exception("There is no records or access denied");
                        }
                        //Going to lines
                        List <string> nameOfProducts = GetNameOfProductList(htmlDoc);
                        List<string> nettoPrices = GetNettoPriceList(htmlDoc);
                        if (nameOfProducts.Count == nettoPrices.Count)
                        {
                            int count = nameOfProducts.Count;
                            StringBuilder stringBuilder = new StringBuilder();
                            for (int i = 0; i < count; i++)
                            {
                                stringBuilder.Append(numberOfOrder).Append(";").Append(dateOfOrder).Append(";").Append(nameOfProducts[i]).Append(";").Append(nettoPrices[i]);
                                resultOfOneId.Add(stringBuilder.ToString());
                                stringBuilder.Clear();
                            }
                        }
                        else
                        {
                            throw new Exception("The list of items is not equal to list of prices");
                        }
                        
                        //await Console.Out.WriteLineAsync(htmlContent);
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

        static async Task <List<string>> Generate(int startId, int endId)
        { 
            List<string> list = new List<string>();
            for (int i = startId; i <= endId; i++) 
            {
                List <string> tempList = await Request(i);
                list.AddRange(tempList);     
            }
            return list;
        }

        static string GetCellValue(HtmlDocument htmlDoc, int cellNumber)
        {
            // Выполнение запроса XPath для выбора текста из конкретной ячейки
            HtmlNode cellNode = htmlDoc.DocumentNode.SelectSingleNode($"//tr[@class='tablelistitem'][{cellNumber}]/td[@valign='top']/a");
            //HtmlNode cellNode = htmlDoc.DocumentNode.SelectSingleNode($"//tr[@class='tablelistitem'][{cellNumber}]/td[@class='maindata']/a");

            // Возврат текста из выбранной ячейки
            return cellNode?.InnerText.Trim() ?? "";
        }

        static List<string> GetNameOfProductList(HtmlDocument htmlDoc)
        {
            //var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@valign='top']//a");
            var tdNodes = htmlDoc.DocumentNode.SelectNodes($"//tr[@class='tablelistitem']//td[@class='maindata']//a");
            List <string> values = new List<string>();
            if (tdNodes != null)
            {
                foreach (var tdNode in tdNodes)
                {
                    values.Add(tdNode.InnerText.Trim());
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
                    values.Add(tdNode.InnerText.Trim());
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
            using (FileStream stream = new FileStream(pathToSave, FileMode.Append))
            {
                foreach (string line in lines)
                {
                    await stream.WriteAsync(Encoding.Default.GetBytes(line), 0, line.Length);
                }

            }

        }
    }
}
