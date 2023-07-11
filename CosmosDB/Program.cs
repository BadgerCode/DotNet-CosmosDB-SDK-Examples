// See https://aka.ms/new-console-template for more information

// Cosmos sample project
// Required package: Microsoft.Azure.Cosmos
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;



// Create a Cosmos DB account in the Azure portal
// Then go to that Cosmos DB account in the portal, and go to the "Keys" tab
// Copy the "Primary connection string"
// E.g. AccountEndpoint=https://EXAMPLE.documents.azure.com:443/;AccountKey=ABCDEF123...ASDASD==;

// Config
var connectionString = "TODO";



// Setting up resources
// Cosmos DB Account -> Database -> Container -> Item
var databaseName = "ExampleDB";
var containerName = "ExampleContainer";

var cosmosClient = new CosmosClient(connectionString);

var database = (await cosmosClient.CreateDatabaseIfNotExistsAsync(databaseName, 400)).Database;
var container = (await database.CreateContainerIfNotExistsAsync(containerName, "/myPartitionKey", 400)).Container;

// Shortcut, if your database and container already exist
//cosmosClient.GetContainer(databaseName, containerName);


// TYPES
// To represent your documents, you can use classes
//      or use Newtonsoft "JObjects", to allow for anonymous types and JSON manipulation
// If you use your own types, the property "id" is required (case-sensitive; use [JsonProperty("id")] on the property
//      you will also need to provide a value for your specified partition key property



// Create an item
JObject newItem = await container.CreateItemAsync(new JObject
{
    { "id", Guid.NewGuid().ToString() },
    { "myPartitionKey", DateTime.UtcNow.ToString("yyyy-MM-dd") },
    { "name", "John Smith" },
    { "number", DateTime.UtcNow.Ticks },
    { "bool", true },
    { "someDateTime", DateTime.UtcNow.ToString("u") },
    {
        "childObject",
        new JObject {
            { "someProperty", "someValue" }
        }
    },
    {
        "childArray",
        new JArray { "apple", "banana" }
    }
});

var newItemID = newItem["id"].Value<string>();
var newItemPartitionKey = newItem["myPartitionKey"].Value<string>();

Console.WriteLine($"Created item {newItemID} in partition {newItemPartitionKey}");





// Retrieve a specific item, by partition key and ID
JObject retrievedItem = await container.ReadItemAsync<JObject>(
    id: newItemID,
    partitionKey: new PartitionKey(newItemPartitionKey)
);
// If an item doesn't exist, the SDK will throw a Microsoft.Azure.Cosmos.CosmosException
//      The StatusCode property will be "NotFound"

Console.WriteLine($"Retrieved item {retrievedItem["id"].Value<string>()} in partition {retrievedItem["myPartitionKey"].Value<string>()}");





// Query items (by partition and some other property)
// Cross-partition queries are expensive and should be avoided
var query = new QueryDefinition(query: "SELECT * FROM c WHERE c.myPartitionKey = @myPartitionKey AND c.name = @nameFilter")
    .WithParameter("@myPartitionKey", newItemPartitionKey)
    .WithParameter("@nameFilter", "John Smith");

using FeedIterator<JObject> feed = container.GetItemQueryIterator<JObject>(queryDefinition: query);
while (feed.HasMoreResults)
{
    FeedResponse<JObject> response = await feed.ReadNextAsync();
    foreach (JObject item in response)
    {
        var itemID = item["id"].Value<string>();
        Console.WriteLine($"Found item: {itemID}");
    }
}





// Update/Insert an item, with ETag matching

// ETag matching is used to prevent lost updates
//      when multiple systems/users are retrieving and updating the same document at the same time
// https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/database-transactions-optimistic-concurrency#implementing-optimistic-concurrency-control-using-etag-and-http-headers

retrievedItem["name"] = "Alex Turner";
var eTag = retrievedItem["_etag"].Value<string>(); // Get the ETag by retrieving the item first

JObject updatedItem = await container.UpsertItemAsync(
    item: retrievedItem,
    partitionKey: new PartitionKey(retrievedItem["myPartitionKey"].Value<string>()),
    requestOptions: new ItemRequestOptions { IfMatchEtag = eTag }
);

Console.WriteLine($"Updated item {updatedItem["id"].Value<string>()}. New value: {updatedItem["name"].Value<string>()}");




// Patch an item (update individual properties)
List<PatchOperation> operations = new()
{
    PatchOperation.Add("/color", "silver"),
    PatchOperation.Remove("/bool"),
    PatchOperation.Increment("/number", 50.00),
    PatchOperation.Add("/childArray/-", "strawberry")
};

ItemResponse<JObject> patchResponse = await container.PatchItemAsync<JObject>(
    id: newItemID,
    partitionKey: new PartitionKey(newItemPartitionKey),
    patchOperations: operations
);

Console.WriteLine($"Updated {newItemID} with a cost of {patchResponse.RequestCharge} RUs");
Console.WriteLine(patchResponse.Resource.ToString());






// Delete an item
var deleteResponse = await container.DeleteItemAsync<JObject>(
    id: newItemID,
    partitionKey: new PartitionKey(newItemPartitionKey)
);
// deleteResponse.Resource will always be empty
Console.WriteLine($"Deleted item {newItemID} under partition {newItemPartitionKey}");




Console.WriteLine("Done");
