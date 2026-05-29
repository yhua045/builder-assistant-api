using System.IO;
using System.Text.RegularExpressions;

var path = "src/Infrastructure/DependencyInjection.cs";
var code = File.ReadAllText(path);

var newStr = @"
        // Register OpenIddict Seed Worker
        services.AddHostedService<OpenIddictDataSeedWorker>();
        
        return services;";

code = code.Replace("return services;", newStr);
File.WriteAllText(path, code);
