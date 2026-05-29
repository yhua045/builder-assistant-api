using System.IO;

var path = "src/Infrastructure/DependencyInjection.cs";
var code = File.ReadAllText(path);

code = code.Replace(@"        // Auth options — validated at startup (app fails fast if JwtSigningKey is missing)
        services.AddOptions<AuthOptions>()
            .BindConfiguration(AuthOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();", "");

File.WriteAllText(path, code);
