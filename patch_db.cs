using System.IO;

var path = "src/Infrastructure/BuilderAssistantDbContext.cs";
var code = File.ReadAllText(path);

var index = code.IndexOf("// AuthorizationCode configuration");
if (index != -1) {
    var endIndex = code.LastIndexOf("}");
    // keep the last closing brace
    code = code.Substring(0, index) + "    }\n}\n";
    File.WriteAllText(path, code);
}
