using MongoDB.Driver;

string[] databases = { "diten_auth", "diten_identity", "diten_mdm", "diten_platform" };
var client = new MongoClient("mongodb://localhost:27017");

foreach (var dbName in databases)
{
    Console.WriteLine($"Dropping database: {dbName}");
    client.DropDatabase(dbName);
}

Console.WriteLine("All specified databases dropped. Restart services to re-seed.");
