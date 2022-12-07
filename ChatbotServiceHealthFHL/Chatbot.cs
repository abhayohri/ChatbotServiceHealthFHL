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
using System.Net.Http;
using System.Net.Http.Headers;
using System.Xml.Linq;
using System.Net;
using Microsoft.VisualBasic.FileIO;
using static System.Net.Mime.MediaTypeNames;
using System.Text;
using System.Reflection.Metadata;
using Newtonsoft.Json.Linq;
using System.Collections.Immutable;
using System.Collections.Generic;

namespace ChatbotServiceHealthFHL
{
    public static class Chatbot
    {
        [FunctionName("Chatbot")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");
            var openAiKey = "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"; //secret
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            string responseMessage = "";

            string type = req.Query["type"];
            type = type ?? data?.type;

            switch (type)
            {
                case "question":
                    string question = req.Query["question"];
                    question = question ?? data?.question;
                    if (string.IsNullOrEmpty(question))
                    {
                        responseMessage = "No Question Provided";
                    }
                    else
                    {   var engine = getlatestfinetune(openAiKey);
                        int tokens = 250;
                        double temperature = 0.2;
                        int topP = 1;
                        int frequencyPenalty = 0;
                        int presencePenalty = 0;
                        responseMessage = callOpenAI(tokens, question, engine, temperature, topP, frequencyPenalty, presencePenalty, openAiKey);
                    }
                    break;
                case "uploadfile":
                    responseMessage = upload(openAiKey).Result;                    
                    dynamic result = JObject.Parse(responseMessage);
                    string fileId = result.id;
                    responseMessage = responseMessage + "\n" + fileId + "\n" + finetune(openAiKey, fileId, "davinci");
                    break;
                case "finetunename":
                    responseMessage = getlatestfinetune(openAiKey);
                    break;
                default:
                    responseMessage = "No Type Provided";
                    break;
            }           

            return new OkObjectResult(responseMessage);
        }


        private static string getlatestfinetune(string openAiKey)
        {

            var apiCall = "https://api.openai.com/v1/fine-tunes";
            string latestFName = null;
            try
            {

                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("GET"), apiCall))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + openAiKey);
                        
                        //request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");


                        var response = httpClient.SendAsync(request).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        System.Console.Out.WriteLine(json);

                        dynamic dynObj = JsonConvert.DeserializeObject(json);

                        if (dynObj != null)
                        {
                            var latest = 0;
                            
                            foreach(var f in dynObj.data)
                            {
                                if (f.fine_tuned_model!= null && f.updated_at > latest)
                                {
                                    latest = f.updated_at;
                                    latestFName = f.fine_tuned_model;
                                }

                            }
                        }


                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }
            System.Console.Out.WriteLine(latestFName);
            return latestFName;


        }

        private static string finetune(string openAiKey, string trainingFile, string engine)
        {

            var apiCall = "https://api.openai.com/v1/fine-tunes";

            try
            {

                using (var httpClient = new HttpClient())
                {
                    using (var request = new HttpRequestMessage(new HttpMethod("POST"), apiCall))
                    {
                        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + openAiKey);
                        request.Content = new StringContent("{\"training_file\": \"" + trainingFile+"\",\"model\": \"" + engine + "\"}");
                        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                                               
                        var response = httpClient.SendAsync(request).Result;
                        var json = response.Content.ReadAsStringAsync().Result;
                        System.Console.Out.WriteLine(json);

                        dynamic dynObj = JsonConvert.DeserializeObject(json);

                        if (dynObj != null)
                        {
                            System.Console.Out.WriteLine(dynObj.id.ToString());
                            return dynObj.id.ToString();
                        }


                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }

            return null;


        }

        private static async Task<string> upload(string openAiKey)
        {

            try
            {
                string temp = "{\"prompt\": \"isa misa pila\", \"completion\": \"pila is true\"}";

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", openAiKey);
                    var multipartContent = new MultipartFormDataContent();
                    multipartContent.Add(new StringContent("fine-tune"), "purpose");
                    multipartContent.Add(new StringContent(temp, Encoding.UTF8, "application/json"), "file", "finetunefile1");                        
                    //request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
                    var response = await httpClient.PostAsync("https://api.openai.com/v1/files", multipartContent);
                    var responseString = await response.Content.ReadAsStringAsync();
                    return responseString;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);

            }

            return null;         

            
        }

        private static string callOpenAI(int tokens, string input, string engine,
          double temperature, int topP, int frequencyPenalty, int presencePenalty, string openAiKey)
        {
            //var apiCall = "https://api.openai.com/v1/engines/" + engine + "/completions";


            //var preprompt = "You will be supporting customers of Azure with their problems. For each problem, first try to see if the problem is one of those which I provide below. If it is, use the solution that I provide below. If not, refer the customer to the most relevant part of Azure Service Health documentation, be as specific as possible List of possible problems and their solutions: \nProblem 1: User can't find any health event posted in the Service Issues blade, but they think they're impacted. The user might not be able to access a VM or any other Azure resource. The user might also want to see all the outages or events that are affecting them.\nSolution 1: Help the user find service health events that are impacting their subscriptions. Start by offering to run the Azure Resource Graph query:\n'''\nServiceHealthResources\n| where type =~ 'Microsoft.ResourceHealth/events'\n| extend eventType = tostring(properties.EventType), status = properties.Status, description = properties.Title, trackingId = properties.TrackingId, summary = properties.Summary, priority = properties.Priority, impactStartTime = properties.ImpactStartTime, impactMitigationTime = todatetime(tolong(properties.ImpactMitigationTime))\n| where properties.Status == 'Active' and tolong(impactStartTime) > 1\r\n| summarize count(subscriptionId) by name, eventType\n'''\nIf that helped, notify the user that the subscription filter might have been set. If it didn't help, let the user know there might have been an outage that just ended, or that there is an outage which was not posted yet \nDo you understand?";
            var preprompt = "You will be supporting customers of Azure with their problems. For each problem, first try to see if the problem is one of those which I provide below. If it is, use the solution that I provide below. If not, refer the customer to the most relevant part of Azure Service Health documentation, be as specific as possible List of possible problems and their solutions: Problem 1: User can't find any health event posted in the Service Issues blade, but they think they're impacted. The user might not be able to access a VM or any other Azure resource. The user might also want to see all the outages or events that are affecting them. Solution 1: Help the user find service health events that are impacting their subscriptions. Start by offering to run the Azure Resource Graph query:'''ServiceHealthResources| where type =~ 'Microsoft.ResourceHealth/events' | extend eventType = tostring(properties.EventType), status = properties.Status, description = properties.Title, trackingId = properties.TrackingId, summary = properties.Summary, priority = properties.Priority, impactStartTime = properties.ImpactStartTime, impactMitigationTime = todatetime(tolong(properties.ImpactMitigationTime)) | where properties.Status == 'Active' and tolong(impactStartTime) > 1| summarize count(subscriptionId) by name, eventType ''' If that helped, notify the user that the subscription filter might have been set. If it didn't help, let the user know there might have been an outage that just ended, or that there is an outage which was not posted yet Do you understand?";

            //var preprompt = "You will be supporting customers of Azure with their problems. Do you understand?";
            var apiCall = "https://api.openai.com/v1/completions";

            var prompts = new List<string> { preprompt, input };
            string jsonString= "";
            dynamic dynObj = null;

            foreach (var prompt in prompts)
            {
                try
                {

                    using (var httpClient = new HttpClient())
                    {
                        //httpClient.Timeout = TimeSpan.FromMinutes(1);
                        using (var request = new HttpRequestMessage(new HttpMethod("POST"), apiCall))
                        {

                            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + openAiKey);
                            /*"{\n  \"prompt\": \"" + input + "\",\n  \"model\": " + engine + "\",\n  \"temperature\": " +
                                                                temperature.ToString(CultureInfo.InvariantCulture) + "\",\n  \"max_tokens\": " + tokens + ",\n  \"top_p\": " + topP +
                                                                ",\n  \"frequency_penalty\": " + frequencyPenalty + ",\n  \"presence_penalty\": " + presencePenalty + "\n}");
                            //request.Content = new StringContent("{\"prompt\": \"what is a monkey\"}");
                            */
                            string s = "{\"prompt\": \"" + prompt + "\","
                                + "\"model\": \"" + engine + "\","
                                + "\"temperature\": " + temperature.ToString(CultureInfo.InvariantCulture) + ","
                                + "\"max_tokens\": " + tokens + ","
                                + "\"top_p\": " + topP + ","
                                + "\"frequency_penalty\": " + frequencyPenalty + ","
                                + "\"presence_penalty\": " + presencePenalty + "}";

                            System.Console.Out.WriteLine(s);
                            request.Content = new StringContent(s);
                            request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

                            var response = httpClient.SendAsync(request).Result;
                            jsonString = response.Content.ReadAsStringAsync().Result;
                            dynObj = JsonConvert.DeserializeObject(jsonString);
                            if (dynObj != null)
                            {
                                System.Console.Out.WriteLine(dynObj.choices[0].text.ToString());
                            }

                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);

                }
            }            

            if (dynObj != null)
            {
                return dynObj.choices[0].text.ToString();
            }

            return null;


        }
    }

}