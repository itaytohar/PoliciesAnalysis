using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace ExtractIDDetailsFromJson
{
    public static class GetDetails
    {
        [FunctionName("GetDetails")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic json = JsonConvert.DeserializeObject(requestBody);
            string rex = @"^\d{1,2}.\d{1,2}.\d{4}$";
            var list = new System.Collections.Generic.List<string>();
            string ID = "";
            foreach (var p in json.pages)
            {
                foreach (var kv in p.keyValuePairs)
                {
                    foreach (var val in kv.value)
                    {

                        string valText = ((string)val.text).Trim();
                        valText = Regex.Replace(valText, @" ", string.Empty);
                        int res = 0;
                        if (valText.Length == 9 && int.TryParse(valText, out res))
                        {
                            ID = valText;
                        }
                        if (valText.Length >= 10)
                        {
                            string item = (valText).Substring(valText.Length - 10);
                            if (Regex.Match(item, rex).Success && !(int.TryParse(item, out res)))
                            {
                                list.Add(item);
                            }
                        }
                    }
                }
            }
            string IssueDate = list[1];
            string burthDate = list[0];

            string responseMessage = "{ \"IdDetails\" : {	\"Id\" : \"" + ID + "\", \"IdBirthDate\" : \"" + burthDate + "\", \"IdIssueDate\" : \"" + IssueDate + "\"}";
            return new OkObjectResult(responseMessage);
        }
    }
}
