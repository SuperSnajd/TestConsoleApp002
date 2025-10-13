using Marten;

var opts = new StoreOptions();
var typeInfo = opts.AutoCreateSchemaObjects.GetType();
Console.WriteLine($"Type: {typeInfo.FullName}");
Console.WriteLine($"Namespace: {typeInfo.Namespace}");
Console.WriteLine($"Assembly: {typeInfo.Assembly.GetName().Name}");

