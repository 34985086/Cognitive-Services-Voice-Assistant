using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System.Net.Http;
using System.Net;
using System.Text;

namespace VirtualRoomApp
{
    public static class RoomDemo
    {
        private static string connectionString = "STORAGE_CONNECTION_STRING";

        private static CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);

        private static CloudTableClient tableClient = storageAccount.CreateCloudTableClient();

        private static CloudTable table = tableClient.GetTableReference("virtualroomconfig");

        [FunctionName("RoomDemo")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            await table.CreateIfNotExistsAsync();

            var room = req.Headers["room"];

            if (string.IsNullOrEmpty(room))
            {
                room = req.Query["room"];
            }

            if (string.IsNullOrEmpty(room))
            {
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Please pass a room name on the query string or in the header")
                };
            }

            var partitionKey = "Demo";
            var rowKey = room;

            try
            {
                // get the room from the table
                var getRoom = TableOperation.Retrieve<VirtualRoomConfig>(partitionKey, rowKey);

                var query = await table.ExecuteAsync(getRoom);

                var currRoomConfig = (VirtualRoomConfig)query.Result;

                // if room not exist, create a record using default config
                if (currRoomConfig == null)
                {
                    var defaultRoom = new VirtualRoomConfig(partitionKey, rowKey);
                    var createRoom = TableOperation.Insert(defaultRoom);
                    await table.ExecuteAsync(createRoom);
                    currRoomConfig = (VirtualRoomConfig)(await table.ExecuteAsync(getRoom)).Result;
                }

                var operation = req.Query["operation"].ToString().ToLower();
                var updated = false;

                if (!string.IsNullOrEmpty(operation))
                {
                    if (operation.Equals("reset"))
                    {
                        currRoomConfig.LoadDefaultConfig();
                        updated = true;
                    }
                    else if (operation.Equals("turn"))
                    {
                        var item = req.Query["item"].ToString().ToLower();
                        var instance = req.Query["instance"].ToString().ToLower();
                        var value = req.Query["value"].ToString().ToLower();

                        bool? valueBool = (value.Equals("on") || value.Equals("open")) ? true : ((value.Equals("off") || value.Equals("close")) ? (bool?)false : null);

                        if (valueBool == null)
                        {
                            updated = false;
                        }
                        else if (item.Equals("lights"))
                        {
                            if (instance.Equals("all"))
                            {
                                currRoomConfig.Lights_bathroom = (bool)valueBool;
                                currRoomConfig.Lights_room = (bool)valueBool;
                                currRoomConfig.Message = "All lights " + value;
                                updated = true;
                            }
                            else if (instance.Equals("room"))
                            {
                                currRoomConfig.Lights_room = (bool)valueBool;
                                currRoomConfig.Message = "room light " + value;
                                updated = true;
                            }
                            else if (instance.Equals("bathroom"))
                            {
                                currRoomConfig.Lights_bathroom = (bool)valueBool;
                                currRoomConfig.Message = "bathroom light " + value;
                                updated = true;
                            }
                        }
                        else if (item.Equals("tv"))
                        {
                            currRoomConfig.Television = (bool)valueBool;
                            currRoomConfig.Message = "TV " + value;
                            updated = true;
                        }
                        else if (item.Equals("blinds"))
                        {
                            currRoomConfig.Blinds = (bool)valueBool;
                            currRoomConfig.Message = (bool)valueBool ? "blinds opened" : "blinds closed";
                            updated = true;
                        }
                        else if (item.Equals("ac"))
                        {
                            currRoomConfig.AC = (bool)valueBool;
                            currRoomConfig.Message = "AC " + value;
                            updated = true;
                        }
                    }
                    else if (operation.Equals("settemperature"))
                    {
                        currRoomConfig.Temperature = int.Parse(req.Query["value"]);
                        currRoomConfig.Message = "set temperature to " + req.Query["value"];
                        updated = true;
                    }
                    else if (operation.Equals("increasetemperature"))
                    {
                        currRoomConfig.Temperature += int.Parse(req.Query["value"]);
                        currRoomConfig.Message = "raised temperature by " + req.Query["value"] + " degrees";
                        updated = true;
                    }
                    else if (operation.Equals("decreasetemperature"))
                    {
                        currRoomConfig.Temperature -= int.Parse(req.Query["value"]);
                        currRoomConfig.Message = "decreased temperature by " + req.Query["value"] + " degrees";
                        updated = true;
                    }
                }

                if (updated)
                {
                    var updateRoom = TableOperation.Replace(currRoomConfig as VirtualRoomConfig);
                    await table.ExecuteAsync(updateRoom);
                    log.LogInformation("successfully updated the record");
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(currRoomConfig, Formatting.Indented), Encoding.UTF8, "application/json")
                };
            }
            catch(Exception e)
            {
                log.LogError(e.Message);
                return new HttpResponseMessage(HttpStatusCode.BadRequest)
                {
                    Content = new StringContent("Failed to process request")
                };
            }
        }
    }

    public class VirtualRoomConfig : TableEntity
    {
        public VirtualRoomConfig() { }

        public bool Lights_room { get; set; }
        public bool Lights_bathroom { get; set; }
        public bool Television { get; set; }
        public bool Blinds { get; set; }
        public bool AC { get; set; }
        public int Temperature { get; set; }
        public string Message { get; set; }

        public VirtualRoomConfig(string partitionKey, string rowKey)
        {
            this.PartitionKey = partitionKey;
            this.RowKey = rowKey;
            this.Lights_room = false;
            this.Lights_bathroom = false;
            this.Television = false;
            this.Blinds = true;
            this.AC = false;
            this.Temperature = 70;
            this.Message = "";
        }

        public void LoadDefaultConfig()
        {
            this.Lights_room = false;
            this.Lights_bathroom = false;
            this.Television = false;
            this.Blinds = true;
            this.AC = false;
            this.Temperature = 70;
            this.Message = "";
        }
    }
}
