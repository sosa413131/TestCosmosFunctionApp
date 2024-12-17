using System.Collections;
using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Functions.Worker.Http;

namespace TestCosmosFunctionApp
{
    public class CosmosDbQueryFunction
    {
        private static readonly string endpointUri = Environment.GetEnvironmentVariable("endpointUri");
        private static readonly string primaryKey = Environment.GetEnvironmentVariable("primaryKey");
        private static readonly string databaseName = Environment.GetEnvironmentVariable("databaseName");
        private static readonly string containerName = Environment.GetEnvironmentVariable("containerName");
        private static CosmosClient cosmosClient;

        // Initialize the CosmosClient
        static CosmosDbQueryFunction()
        {
            cosmosClient = new CosmosClient(endpointUri, primaryKey);
        }

        // Define the function and use the correct HttpTrigger attribute for the worker SDK
        [Function("QueryCosmosDb")]
        public static async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
            FunctionContext executionContext)
        {
            var log = executionContext.GetLogger("QueryCosmosDb");

            try
            {
                // Get the container reference
                Container container = cosmosClient.GetContainer(databaseName, containerName);

                // Define the SQL query
                string sqlQueryText = "SELECT * FROM c WHERE c.id IN ({0})";

                IList ids = new List<string> { "1", "2", "3" };

                List<string> paramNames = new List<string>();

                for (int i = 0; i < ids.Count; i++)
                {
                    paramNames.Add("@userInputId" + i);
                }

                var query = new QueryDefinition(string.Format(sqlQueryText, string.Join(",", paramNames)));
                for (int j = 0; j < ids.Count; j++)
                {
                    query.WithParameter(paramNames[j], ids[j]);
                }

                // Run the query
                FeedIterator<dynamic> queryResultSetIterator = container.GetItemQueryIterator<dynamic>(query);

                // Collect the results
                List<dynamic> results = new List<dynamic>();
                while (queryResultSetIterator.HasMoreResults)
                {
                    FeedResponse<dynamic> currentResultSet = await queryResultSetIterator.ReadNextAsync();
                    results.AddRange(currentResultSet);
                }

                // Log the results
                foreach (var item in results)
                {
                    log.LogInformation($"Id: {item.id}, Name: {item.name}, Category: {item.category}");
                }

                // Create and return the response
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(Newtonsoft.Json.JsonConvert.SerializeObject(results));
                return response;
            }
            catch (Exception ex)
            {
                log.LogError($"An error occurred: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"An error occurred: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
