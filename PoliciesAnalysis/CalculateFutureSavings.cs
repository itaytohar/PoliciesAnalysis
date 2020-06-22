using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;
using System.Data.SqlClient;
using System.Collections.Generic;

namespace PoliciesAnalysis
{
    public class YearlySavings
    {
        public int RetirementYear { get; set; }
        public double RetirementTotalSavings { get; set; }
    }
    public static class CalculateFutureSavings
    {
        [FunctionName("CalculateFutureSavings")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            string responseMessage = "";
            string CustomerID = req.Query["ID"];
            string BirthDate = req.Query["BirthDate"].ToString().Replace('.', '/');
            DateTime dBirthDate;
            string EMAIL="";
            int Age = 0;
            int YearsForPension = 0;
            int ID = 0;
            double YearlyInterest = 1.04;
            string text = "";
            Random rnd = new Random();
            int NumOfPolicies = rnd.Next(1, 3);
           // dBirthDate = DateTime.ParseExact(BirthDate, "dd/mm/yyyy", CultureInfo.InvariantCulture);
            if (DateTime.TryParseExact(BirthDate, "d/M/yyyy",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out dBirthDate))
            {
                Age =  DateTime.Today.Year - dBirthDate.Year;
                YearsForPension = 67 - Age;
            }
            else
            {
                log.LogError($"Parse BirthDate for Customer Failed");
                responseMessage = "Error";
                return new BadRequestObjectResult(responseMessage);
            }

            var str = Environment.GetEnvironmentVariable("sqldb_connection");
            if (int.TryParse(CustomerID, out ID))
            {
                // Update somw rows of policies for the new customer
                text = $"UPDATE dbo.Policies SET CustomerID = {ID}, IsVisible = 1 WHERE PolicyNum IN (SELECT TOP {NumOfPolicies} PolicyNum FROM  dbo.Policies WHERE CustomerID = 99)";
                using (SqlConnection connLocal = new SqlConnection(str))
                {
                    connLocal.Open();

                    using (SqlCommand cmd = new SqlCommand(text, connLocal))
                    {
                        // Execute the command and log the # rows affected.
                        var rows = await cmd.ExecuteNonQueryAsync();
                        log.LogInformation($"{rows} rows were updated");
                    }
                }

                string text2 = $"Select * FROM dbo.Policies  Where CustomerID = {ID}";
                List<YearlySavings> YealySavingsLst = new List<YearlySavings>();
                using (SqlConnection conn2 = new SqlConnection(str))
                {
                    conn2.Open();

                    using (SqlCommand cmd = new SqlCommand(text2, conn2))
                    {
                        // Execute the command and log the # rows affected.
                       
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                double SavingsAmount = (double)reader["SavingsAmount"];
                                var PromisedPromote = reader["PromisedPromote"];
                                var PolicyNum = reader["PolicyNum"];
                                double YearAllowence = (double)reader["YearAllowence"];
                                //Calculate Pension foreach year till Age 67
                                double CurrentTotalSavings = SavingsAmount;
                                List<YearlySavings> yearlySavingsList;
                                yearlySavingsList = CalcPensionSavings(YearsForPension, YearlyInterest, YearAllowence, CurrentTotalSavings);
                                // Insert all list rows to PoliciesAnalysis table
                                text = await InsertPolicySavingsToDB(log, text, str, conn2, PolicyNum, yearlySavingsList);
                            }
                        }
                    }
                }
            }
            else
            {
                log.LogError($"Parse Customer ID Error");
                responseMessage = "Error";
                return new BadRequestObjectResult(responseMessage);
            }


            string text3 = $"Select EMAIL FROM dbo.CustomerDetails  Where Id = '{CustomerID}'";
            using (SqlConnection conn3 = new SqlConnection(str))
            {
                conn3.Open();

                using (SqlCommand cmd = new SqlCommand(text3, conn3))
                {
                    // Execute the command and log the # rows affected.

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            EMAIL = (string)reader["EMAIL"];
                        }
                    }
                }
            }
                            //            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                            //            dynamic data = JsonConvert.DeserializeObject(requestBody);

            responseMessage = EMAIL;
            return new OkObjectResult(responseMessage);
        }

        private static async Task<string> InsertPolicySavingsToDB(ILogger log, string text, string str, SqlConnection conn, object PolicyNum, List<YearlySavings> yearlySavingsList)
        {
            foreach (var item in yearlySavingsList)
            {
                text = $"INSERT INTO dbo.PoliciesAnalysis(PolicyNum,RetirementYear,RetirementTotalSavings) VALUES ({PolicyNum},{item.RetirementYear},{item.RetirementTotalSavings})";
                using (SqlConnection conn2 = new SqlConnection(str))
                {
                    conn2.Open();

                    using (SqlCommand cmd2 = new SqlCommand(text, conn2))
                    {
                        // Execute the command and log the # rows affected.
                        var rows = await cmd2.ExecuteNonQueryAsync();
                        log.LogInformation($"{rows} rows were inserted");
                    }
                }

            }

            return text;
        }

        private static List<YearlySavings> CalcPensionSavings(int YearsForPension, double YearlyInterest, double YearAllowence, double CurrentTotalSavings)
        {
            List<YearlySavings> YealySavingsLst = new List<YearlySavings>();
            for (int i = 1; i < YearsForPension + 1; i++)
            {
                int Year = DateTime.Today.Year + i;
                CurrentTotalSavings = (CurrentTotalSavings + YearAllowence) * YearlyInterest;
                double dRetirementTotalSavings = CurrentTotalSavings;
                for (int j = i + 1; j < YearsForPension + 1; j++)
                {
                    dRetirementTotalSavings = dRetirementTotalSavings * YearlyInterest;
                }
                YearlySavings YearlySavingsItem = new YearlySavings()
                {
                    RetirementYear = DateTime.Today.Year + i,
                    RetirementTotalSavings = dRetirementTotalSavings
                };

                YealySavingsLst.Add(YearlySavingsItem);
            }

            return YealySavingsLst;
        }
    }
}
